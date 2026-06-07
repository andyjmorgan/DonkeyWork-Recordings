using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Mapping;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using DonkeyWork.Recordings.Storage.Contracts.Services;
using Microsoft.EntityFrameworkCore;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class TtsService : ITtsService
{
    private readonly RecordingsDbContext _dbContext;
    private readonly IIdentityContext _identityContext;
    private readonly IStorageService _storage;

    public TtsService(
        RecordingsDbContext dbContext,
        IIdentityContext identityContext,
        IStorageService storage)
    {
        _dbContext = dbContext;
        _identityContext = identityContext;
        _storage = storage;
    }

    public async Task<ListRecordingsResponseV1> ListRecordingsAsync(int offset, int limit, bool unfiledOnly = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Recordings.AsNoTracking();

        if (unfiledOnly)
        {
            query = query.Where(r => r.CollectionId == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, 200))
            .Select(r => r.ToV1())
            .ToListAsync(cancellationToken);

        return new ListRecordingsResponseV1 { Items = items, TotalCount = totalCount };
    }

    public async Task<TtsRecordingV1?> GetRecordingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recording = await _dbContext.Recordings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return recording?.ToV1();
    }

    public async Task<bool> DeleteRecordingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recording = await _dbContext.Recordings
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (recording is null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(recording.FilePath))
        {
            try
            {
                var objectKey = $"{recording.UserId}/{recording.Id}.mp3";
                await _storage.DeleteAsync(objectKey, cancellationToken);
            }
            catch
            {
                // Best-effort blob cleanup — DB row deletion is the source of truth.
            }
        }

        _dbContext.Recordings.Remove(recording);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TtsRecordingV1?> UpdateRecordingAsync(Guid recordingId, UpdateRecordingRequestV1 request, CancellationToken cancellationToken = default)
    {
        var recording = await _dbContext.Recordings
            .FirstOrDefaultAsync(r => r.Id == recordingId, cancellationToken);

        if (recording is null)
        {
            return null;
        }

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Name cannot be blank.", nameof(request));
            }

            recording.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            recording.Description = request.Description;
        }

        if (request.ChapterTitle is not null)
        {
            recording.ChapterTitle = string.IsNullOrWhiteSpace(request.ChapterTitle) ? null : request.ChapterTitle.Trim();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return recording.ToV1();
    }

    public async Task<TtsRecordingV1?> MoveRecordingAsync(Guid recordingId, MoveRecordingToCollectionRequestV1 request, CancellationToken cancellationToken = default)
    {
        var recording = await _dbContext.Recordings
            .FirstOrDefaultAsync(r => r.Id == recordingId, cancellationToken);

        if (recording is null)
        {
            return null;
        }

        if (request.CollectionId == Guid.Empty)
        {
            throw new ArgumentException("A target channel (CollectionId) is required.", nameof(request));
        }

        var exists = await _dbContext.Collections.AnyAsync(c => c.Id == request.CollectionId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"Collection {request.CollectionId} not found.");
        }

        recording.CollectionId = request.CollectionId;

        if (request.SequenceNumber.HasValue)
        {
            recording.SequenceNumber = request.SequenceNumber;
        }
        else
        {
            var max = await _dbContext.Recordings
                .Where(r => r.CollectionId == request.CollectionId && r.Id != recordingId)
                .MaxAsync(r => (int?)r.SequenceNumber, cancellationToken);
            recording.SequenceNumber = (max ?? 0) + 1;
        }

        if (request.ChapterTitle is not null)
        {
            recording.ChapterTitle = request.ChapterTitle;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return recording.ToV1();
    }

    public async Task<TtsPlaybackV1> GetPlaybackAsync(Guid recordingId, CancellationToken cancellationToken = default)
    {
        var playback = await _dbContext.Playback
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.RecordingId == recordingId && p.UserId == _identityContext.UserId, cancellationToken);

        return playback?.ToV1() ?? new TtsPlaybackV1
        {
            PositionSeconds = 0,
            DurationSeconds = 0,
            Completed = false,
            PlaybackSpeed = 1.0,
        };
    }

    public async Task<TtsPlaybackV1?> UpdatePlaybackAsync(Guid recordingId, UpdatePlaybackRequestV1 request, CancellationToken cancellationToken = default)
    {
        var recordingExists = await _dbContext.Recordings.AnyAsync(r => r.Id == recordingId, cancellationToken);
        if (!recordingExists)
        {
            return null;
        }

        var playback = await _dbContext.Playback
            .FirstOrDefaultAsync(p => p.RecordingId == recordingId && p.UserId == _identityContext.UserId, cancellationToken);

        if (playback is null)
        {
            playback = new TtsPlaybackEntity
            {
                UserId = _identityContext.UserId,
                RecordingId = recordingId,
                PositionSeconds = request.PositionSeconds,
                DurationSeconds = request.DurationSeconds ?? 0,
                Completed = request.Completed ?? false,
                PlaybackSpeed = request.PlaybackSpeed ?? 1.0,
            };
            _dbContext.Playback.Add(playback);
        }
        else
        {
            playback.PositionSeconds = request.PositionSeconds;
            if (request.DurationSeconds.HasValue) playback.DurationSeconds = request.DurationSeconds.Value;
            if (request.Completed.HasValue) playback.Completed = request.Completed.Value;
            if (request.PlaybackSpeed.HasValue) playback.PlaybackSpeed = request.PlaybackSpeed.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return playback.ToV1();
    }
}
