using DonkeyWork.Recordings.Audio.Contracts.Messages;
using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class AudioGenerationService : IAudioGenerationService
{
    private const int DefaultMaxParallelism = 2;

    private readonly RecordingsDbContext _dbContext;
    private readonly IIdentityContext _identityContext;
    private readonly IAudioGenerationDispatcher _dispatcher;
    private readonly MagpieOptions _magpieOptions;

    public AudioGenerationService(
        RecordingsDbContext dbContext,
        IIdentityContext identityContext,
        IAudioGenerationDispatcher dispatcher,
        IOptions<MagpieOptions> magpieOptions)
    {
        _dbContext = dbContext;
        _identityContext = identityContext;
        _dispatcher = dispatcher;
        _magpieOptions = magpieOptions.Value;
    }

    public async Task<Guid> StartGenerationAsync(StartAudioGenerationRequestV1 request, CancellationToken cancellationToken = default)
    {
        if (!_identityContext.IsAuthenticated)
        {
            throw new InvalidOperationException("StartGenerationAsync requires an authenticated identity.");
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ArgumentException("Text is required.", nameof(request));
        }

        if (request.MaxCharCount < request.TargetCharCount)
        {
            throw new ArgumentException("MaxCharCount must be >= TargetCharCount.", nameof(request));
        }

        var userId = _identityContext.UserId;

        var collection = request.CollectionId is { } collectionId
            ? await _dbContext.Collections.FirstOrDefaultAsync(c => c.Id == collectionId, cancellationToken)
                ?? throw new InvalidOperationException($"Collection {collectionId} not found.")
            : null;

        var voice = request.Voice
            ?? collection?.DefaultVoice
            ?? _magpieOptions.DefaultVoice;

        var language = request.Language
            ?? collection?.DefaultLanguage
            ?? _magpieOptions.DefaultLanguage;

        var sequenceNumber = request.SequenceNumber ?? await NextSequenceNumberAsync(request.CollectionId, cancellationToken);

        var recording = new TtsRecordingEntity
        {
            UserId = userId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            FilePath = string.Empty,
            Transcript = request.Text,
            ContentType = "audio/mpeg",
            SizeBytes = 0,
            DurationSeconds = 0,
            Voice = voice,
            Language = language,
            CollectionId = request.CollectionId,
            SequenceNumber = sequenceNumber,
            ChapterTitle = request.ChapterTitle,
            Status = TtsRecordingStatus.Pending,
            Progress = 0,
        };

        _dbContext.Recordings.Add(recording);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var command = new GenerateAudioRecordingCommand(
            RecordingId: recording.Id,
            UserId: userId,
            Text: request.Text,
            Voice: voice,
            Language: language,
            TargetCharCount: request.TargetCharCount,
            MaxCharCount: request.MaxCharCount,
            MaxParallelism: DefaultMaxParallelism);

        await _dispatcher.DispatchAsync(command, cancellationToken);

        return recording.Id;
    }

    private async Task<int?> NextSequenceNumberAsync(Guid? collectionId, CancellationToken cancellationToken)
    {
        if (collectionId is null)
        {
            return null;
        }

        var max = await _dbContext.Recordings
            .Where(r => r.CollectionId == collectionId)
            .MaxAsync(r => (int?)r.SequenceNumber, cancellationToken);

        return (max ?? 0) + 1;
    }
}
