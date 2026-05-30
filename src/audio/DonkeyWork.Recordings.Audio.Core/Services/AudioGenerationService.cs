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
    private readonly RecordingsDbContext _dbContext;
    private readonly IIdentityContext _identityContext;
    private readonly IAudioGenerationDispatcher _dispatcher;
    private readonly ITtsProviderRegistry _ttsRegistry;
    private readonly TtsOptions _ttsOptions;

    public AudioGenerationService(
        RecordingsDbContext dbContext,
        IIdentityContext identityContext,
        IAudioGenerationDispatcher dispatcher,
        ITtsProviderRegistry ttsRegistry,
        IOptions<TtsOptions> ttsOptions)
    {
        _dbContext = dbContext;
        _identityContext = identityContext;
        _dispatcher = dispatcher;
        _ttsRegistry = ttsRegistry;
        _ttsOptions = ttsOptions.Value;
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

        if (request.CollectionId == Guid.Empty)
        {
            throw new ArgumentException("A channel (CollectionId) is required to create a recording.", nameof(request));
        }

        var userId = _identityContext.UserId;

        var collection = await _dbContext.Collections.FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Collection {request.CollectionId} not found.");

        // Voice/model come from the channel by default; the request can override either.
        var provider = _ttsRegistry.Resolve(
            request.TtsModel
            ?? collection.DefaultTtsModel
            ?? _ttsOptions.DefaultModel);
        var ttsModel = provider.Key;

        var voice = request.Voice
            ?? collection.DefaultVoice
            ?? provider.DefaultVoice;

        var language = request.Language
            ?? collection.DefaultLanguage
            ?? _ttsOptions.DefaultLanguage;

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
            TtsModel = ttsModel,
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
            TtsModel: ttsModel,
            Voice: voice,
            Language: language,
            TargetCharCount: request.TargetCharCount,
            MaxCharCount: request.MaxCharCount);

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
