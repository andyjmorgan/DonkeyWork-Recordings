using System.Diagnostics;
using DonkeyWork.Recordings.Audio.Contracts.Messages;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Diagnostics;
using DonkeyWork.Recordings.Audio.Core.Helpers;
using DonkeyWork.Recordings.Audio.Core.Options;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using DonkeyWork.Recordings.Storage.Contracts.Models;
using DonkeyWork.Recordings.Storage.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Handlers;

public static class GenerateAudioRecordingHandler
{
    public static async Task Handle(
        GenerateAudioRecordingCommand command,
        RecordingsDbContext dbContext,
        IIdentityContext identityContext,
        IGptOssPreprocessor preprocessor,
        ISsmlPreprocessor ssml,
        ITtsChunker chunker,
        ITtsProviderRegistry ttsRegistry,
        IStorageService storage,
        ILogger<GenerateAudioRecordingCommand> logger,
        CancellationToken cancellationToken)
    {
        identityContext.SetIdentity(command.UserId);

        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["recordingId"] = command.RecordingId,
            ["ttsModel"] = command.TtsModel,
        });

        var recording = await dbContext.Recordings
            .FirstOrDefaultAsync(r => r.Id == command.RecordingId, cancellationToken);

        if (recording is null)
        {
            logger.LogError("Recording {RecordingId} not found; cannot generate", command.RecordingId);
            return;
        }

        using var generationActivity = AudioDiagnostics.ActivitySource.StartActivity("audio.generate");
        generationActivity?.SetTag("recording.id", command.RecordingId.ToString());
        generationActivity?.SetTag("tts.model", command.TtsModel);
        generationActivity?.SetTag("tts.voice", command.Voice);
        generationActivity?.SetTag("recording.input_chars", command.Text.Length);
        var generationStopwatch = Stopwatch.StartNew();

        try
        {
            recording.Status = TtsRecordingStatus.Generating;
            recording.Progress = 0.05;
            recording.StatusDetail = "Preparing the script…";
            await dbContext.SaveChangesAsync(cancellationToken);

            var channelTone = recording.CollectionId is null
                ? null
                : await dbContext.Collections
                    .Where(c => c.Id == recording.CollectionId)
                    .Select(c => c.Tone)
                    .FirstOrDefaultAsync(cancellationToken);

            var paragraphs = await preprocessor.PreprocessAsync(
                new GptOssPreprocessRequest(command.Text, channelTone, command.Language),
                cancellationToken);

            if (paragraphs.Count == 0)
            {
                throw new InvalidOperationException("gpt-oss returned zero paragraphs.");
            }

            var chunkerOpts = new ChunkerOptions
            {
                TargetCharCount = command.TargetCharCount,
                MaxCharCount = command.MaxCharCount,
            };

            var chunks = paragraphs
                .SelectMany(p => chunker.Chunk(p, chunkerOpts))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (chunks.Count == 0)
            {
                throw new InvalidOperationException("Chunker produced zero chunks from preprocessed paragraphs.");
            }

            logger.LogInformation(
                "Generating audio for recording {RecordingId}: {ParagraphCount} paragraphs → {ChunkCount} chunks",
                recording.Id, paragraphs.Count, chunks.Count);

            var ttsProvider = ttsRegistry.Resolve(command.TtsModel);
            generationActivity?.SetTag("tts.model.resolved", ttsProvider.Key);
            generationActivity?.SetTag("tts.chunk_count", chunks.Count);
            var wavBytes = new byte[chunks.Count][];

            // The TTS backends serialise on a single instance — concurrent requests just queue —
            // so synthesise the chunks one at a time in order.
            for (var index = 0; index < chunks.Count; index++)
            {
                using var chunkActivity = AudioDiagnostics.ActivitySource.StartActivity("tts.synthesize.chunk");
                chunkActivity?.SetTag("chunk.index", index);
                chunkActivity?.SetTag("chunk.count", chunks.Count);
                chunkActivity?.SetTag("chunk.chars", chunks[index].Length);
                chunkActivity?.SetTag("tts.model", ttsProvider.Key);

                var chunkStopwatch = Stopwatch.StartNew();
                var wrapped = ssml.Wrap(chunks[index]);
                var clip = await ttsProvider.SynthesizeAsync(
                    wrapped,
                    new TtsProviderRequest(command.Voice, command.Language),
                    cancellationToken);
                wavBytes[index] = clip.Audio;
                chunkStopwatch.Stop();

                chunkActivity?.SetTag("chunk.bytes", clip.Audio.LongLength);
                var chunkTags = new TagList { { "model", ttsProvider.Key } };
                AudioDiagnostics.ChunkSynthDuration.Record(chunkStopwatch.Elapsed.TotalSeconds, chunkTags);
                AudioDiagnostics.ChunksSynthesized.Add(1, chunkTags);

                recording.Progress = 0.1 + 0.8 * ((index + 1.0) / chunks.Count);
                recording.StatusDetail = $"Generating audio — segment {index + 1} of {chunks.Count}";
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            recording.Progress = 0.92;
            recording.StatusDetail = "Stitching & encoding…";
            await dbContext.SaveChangesAsync(cancellationToken);

            var stitchActivity = AudioDiagnostics.ActivitySource.StartActivity("tts.stitch");
            var stitchedWav = AudioConverter.ConcatWav(wavBytes);
            var mp3Bytes = AudioConverter.WavToMp3(stitchedWav);
            var duration = AudioConverter.ProbeDurationSeconds(mp3Bytes);
            stitchActivity?.SetTag("audio.bytes", mp3Bytes.LongLength);
            stitchActivity?.SetTag("audio.duration_s", duration);
            stitchActivity?.Dispose();

            recording.Progress = 0.97;
            recording.StatusDetail = "Uploading…";
            await dbContext.SaveChangesAsync(cancellationToken);

            using var uploadActivity = AudioDiagnostics.ActivitySource.StartActivity("tts.upload");
            await using var uploadStream = new MemoryStream(mp3Bytes);
            var uploadResult = await storage.UploadAsync(
                new UploadFileRequest
                {
                    FileName = $"{recording.Id}.mp3",
                    ContentType = "audio/mpeg",
                    Content = uploadStream,
                    Metadata = new Dictionary<string, string>
                    {
                        ["user-id"] = command.UserId.ToString(),
                        ["recording-id"] = recording.Id.ToString(),
                        ["collection-id"] = recording.CollectionId?.ToString() ?? string.Empty,
                        ["language"] = command.Language,
                        ["voice"] = command.Voice,
                    },
                },
                cancellationToken);

            recording.FilePath = uploadResult.PublicUrl;
            recording.ContentType = "audio/mpeg";
            recording.SizeBytes = mp3Bytes.LongLength;
            recording.DurationSeconds = duration;
            recording.ProcessedTranscript = string.Join("\n\n", paragraphs);
            recording.Status = TtsRecordingStatus.Ready;
            recording.Progress = 1.0;
            recording.StatusDetail = null;
            recording.ErrorMessage = null;

            await dbContext.SaveChangesAsync(cancellationToken);

            generationStopwatch.Stop();
            generationActivity?.SetTag("audio.duration_s", duration);
            generationActivity?.SetStatus(ActivityStatusCode.Ok);
            AudioDiagnostics.GenerationDuration.Record(
                generationStopwatch.Elapsed.TotalSeconds,
                new TagList { { "model", ttsProvider.Key }, { "outcome", "ready" } });
            AudioDiagnostics.RecordingsGenerated.Add(
                1, new TagList { { "model", ttsProvider.Key }, { "outcome", "ready" } });

            logger.LogInformation(
                "Recording {RecordingId} ready: {SizeBytes} bytes, {DurationSeconds:F1}s, {ChunkCount} chunks",
                recording.Id, recording.SizeBytes, duration, chunks.Count);
        }
        catch (Exception ex)
        {
            generationStopwatch.Stop();
            generationActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            AudioDiagnostics.GenerationDuration.Record(
                generationStopwatch.Elapsed.TotalSeconds,
                new TagList { { "outcome", "failed" } });
            AudioDiagnostics.RecordingsGenerated.Add(1, new TagList { { "outcome", "failed" } });

            logger.LogError(ex, "Audio generation failed for recording {RecordingId}", command.RecordingId);

            try
            {
                recording.Status = TtsRecordingStatus.Failed;
                recording.Progress = 0.0;
                recording.StatusDetail = null;
                recording.ErrorMessage = ex.Message;
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception persistEx)
            {
                logger.LogCritical(persistEx,
                    "Failed to persist Failed status for recording {RecordingId}", command.RecordingId);
            }
        }
    }
}
