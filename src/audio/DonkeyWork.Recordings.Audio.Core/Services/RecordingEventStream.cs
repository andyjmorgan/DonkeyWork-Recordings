using System.Text;
using System.Text.Json;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Helpers;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;

namespace DonkeyWork.Recordings.Audio.Core.Services;

// Streams the lifecycle of a recording as Server-Sent Events. FIXED CONTRACT (a Flutter client
// is built against these event names and payload shapes):
//   event: chunk-ready  data: {"index": int, "url": string, "playableUpTo": int}
//   event: progress     data: {"progress": double 0-1, "statusDetail": string}
//   event: ready        data: {"url": string}   (final mp3 url; stream then completes)
//   event: failed       data: {"error": string} (stream then completes)
// State lives in the DB (written by the generation worker), so the stream is a poll-and-diff
// loop: replay whatever is already persisted on connect, then emit deltas. A comment heartbeat
// keeps reverse proxies from idling the connection out.
public sealed class RecordingEventStream : IRecordingEventStream
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RecordingsDbContext _dbContext;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _heartbeatInterval;

    public RecordingEventStream(RecordingsDbContext dbContext)
        : this(dbContext, pollInterval: TimeSpan.FromSeconds(1), heartbeatInterval: TimeSpan.FromSeconds(15))
    {
    }

    // Intervals are injectable so tests don't have to wait wall-clock seconds per assertion.
    public RecordingEventStream(RecordingsDbContext dbContext, TimeSpan pollInterval, TimeSpan heartbeatInterval)
    {
        _dbContext = dbContext;
        _pollInterval = pollInterval;
        _heartbeatInterval = heartbeatInterval;
    }

    public async Task StreamAsync(Guid recordingId, Stream output, CancellationToken cancellationToken = default)
    {
        var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            NewLine = "\n",
        };

        var emittedChunks = new HashSet<int>();
        double? lastProgress = null;
        string? lastStatusDetail = null;
        var lastWriteAt = DateTimeOffset.UtcNow;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var recording = await _dbContext.Recordings
                    .AsNoTracking()
                    .Select(r => new { r.Id, r.Status, r.Progress, r.StatusDetail, r.FilePath, r.ErrorMessage })
                    .FirstOrDefaultAsync(r => r.Id == recordingId, cancellationToken);

                if (recording is null)
                {
                    // Deleted mid-stream (the controller 404s before the stream ever starts when
                    // the id never existed / belongs to another user).
                    await WriteEventAsync(writer, "failed", new FailedPayload("Recording not found."), cancellationToken);
                    return;
                }

                var chunks = await _dbContext.RecordingChunks
                    .AsNoTracking()
                    .Where(c => c.RecordingId == recordingId)
                    .OrderBy(c => c.Index)
                    .Select(c => new { c.Index, c.FilePath })
                    .ToListAsync(cancellationToken);

                foreach (var chunk in chunks)
                {
                    if (!emittedChunks.Add(chunk.Index))
                    {
                        continue;
                    }

                    var playableUpTo = ChunkWatermark.Compute(emittedChunks);
                    await WriteEventAsync(
                        writer,
                        "chunk-ready",
                        new ChunkReadyPayload(chunk.Index, chunk.FilePath, playableUpTo),
                        cancellationToken);
                    lastWriteAt = DateTimeOffset.UtcNow;
                }

                if (recording.Status == TtsRecordingStatus.Ready)
                {
                    await WriteEventAsync(writer, "ready", new ReadyPayload(recording.FilePath), cancellationToken);
                    return;
                }

                if (recording.Status == TtsRecordingStatus.Failed)
                {
                    await WriteEventAsync(
                        writer,
                        "failed",
                        new FailedPayload(recording.ErrorMessage ?? "Audio generation failed."),
                        cancellationToken);
                    return;
                }

                if (recording.Progress != lastProgress || recording.StatusDetail != lastStatusDetail)
                {
                    lastProgress = recording.Progress;
                    lastStatusDetail = recording.StatusDetail;
                    await WriteEventAsync(
                        writer,
                        "progress",
                        new ProgressPayload(recording.Progress, recording.StatusDetail ?? string.Empty),
                        cancellationToken);
                    lastWriteAt = DateTimeOffset.UtcNow;
                }

                if (DateTimeOffset.UtcNow - lastWriteAt >= _heartbeatInterval)
                {
                    await writer.WriteAsync(": keep-alive\n\n");
                    await writer.FlushAsync(cancellationToken);
                    lastWriteAt = DateTimeOffset.UtcNow;
                }

                await Task.Delay(_pollInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — nothing to flush to.
        }
    }

    private static async Task WriteEventAsync<TPayload>(
        StreamWriter writer,
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        await writer.WriteAsync($"event: {eventName}\n");
        await writer.WriteAsync($"data: {JsonSerializer.Serialize(payload, JsonOptions)}\n\n");
        await writer.FlushAsync(cancellationToken);
    }

    private sealed record ChunkReadyPayload(int Index, string Url, int PlayableUpTo);

    private sealed record ProgressPayload(double Progress, string StatusDetail);

    private sealed record ReadyPayload(string Url);

    private sealed record FailedPayload(string Error);
}
