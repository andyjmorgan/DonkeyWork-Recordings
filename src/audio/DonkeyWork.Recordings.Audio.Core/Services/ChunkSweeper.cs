using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using DonkeyWork.Recordings.Storage.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class ChunkSweeper : IChunkSweeper
{
    private readonly RecordingsDbContext _dbContext;
    private readonly IStorageService _storage;
    private readonly TtsOptions _options;
    private readonly ILogger<ChunkSweeper> _logger;

    public ChunkSweeper(
        RecordingsDbContext dbContext,
        IStorageService storage,
        IOptions<TtsOptions> options,
        ILogger<ChunkSweeper> logger)
    {
        _dbContext = dbContext;
        _storage = storage;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - _options.ChunkSweepGracePeriod;

        // Runs without a request identity (background worker), so bypass the per-user query
        // filter — the sweep covers every user's settled recordings.
        var expired = await _dbContext.RecordingChunks
            .IgnoreQueryFilters()
            .Where(c => c.Recording != null
                && (c.Recording.Status == TtsRecordingStatus.Ready || c.Recording.Status == TtsRecordingStatus.Failed)
                && (c.Recording.UpdatedAt ?? c.Recording.CreatedAt) <= cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        foreach (var group in expired.GroupBy(c => new { c.UserId, c.RecordingId }))
        {
            try
            {
                await _storage.DeleteByPrefixAsync(
                    $"{group.Key.UserId}/{group.Key.RecordingId}/chunks/",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort blob cleanup — DB rows are the source of truth (matches
                // TtsService.DeleteRecordingAsync). An orphaned wav costs pennies; a sweep loop
                // wedged on a storage hiccup costs the feature.
                _logger.LogWarning(ex,
                    "Failed to delete chunk objects for recording {RecordingId}; removing rows anyway",
                    group.Key.RecordingId);
            }

            _dbContext.RecordingChunks.RemoveRange(group);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Swept {ChunkCount} expired recording chunks", expired.Count);
        return expired.Count;
    }
}
