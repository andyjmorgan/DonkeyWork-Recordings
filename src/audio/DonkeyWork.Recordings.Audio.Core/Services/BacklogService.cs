using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Mapping;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class BacklogService : IBacklogService
{
    private readonly RecordingsDbContext _dbContext;
    private readonly IIdentityContext _identityContext;

    public BacklogService(RecordingsDbContext dbContext, IIdentityContext identityContext)
    {
        _dbContext = dbContext;
        _identityContext = identityContext;
    }

    public async Task<ListBacklogItemsResponseV1?> ListAsync(Guid collectionId, string? status, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Collections.AnyAsync(c => c.Id == collectionId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var query = _dbContext.BacklogItems.AsNoTracking().Where(b => b.CollectionId == collectionId);

        BacklogItemStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<BacklogItemStatus>(status, ignoreCase: true, out var parsed))
            {
                throw new ArgumentException($"Unknown status '{status}'. Expected Pending, Consumed, Dismissed, or 'all'.", nameof(status));
            }

            statusFilter = parsed;
            query = query.Where(b => b.Status == parsed);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Pending items list FIFO so the oldest queued item leads the next episode; history reads newest-first.
        query = statusFilter == BacklogItemStatus.Pending
            ? query.OrderBy(b => b.CreatedAt)
            : query.OrderByDescending(b => b.CreatedAt);

        var items = await query
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, 200))
            .Select(b => new
            {
                Item = b,
                RecordingName = b.ConsumedByRecording != null ? b.ConsumedByRecording.Name : null,
            })
            .ToListAsync(cancellationToken);

        return new ListBacklogItemsResponseV1
        {
            Items = items.Select(x => x.Item.ToV1(x.RecordingName)).ToList(),
            TotalCount = totalCount,
        };
    }

    public async Task<BacklogItemV1?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.BacklogItems
            .AsNoTracking()
            .Include(b => b.ConsumedByRecording)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        return item?.ToV1(item.ConsumedByRecording?.Name);
    }

    public async Task<BacklogItemV1?> CreateAsync(Guid collectionId, CreateBacklogItemRequestV1 request, CancellationToken cancellationToken = default)
    {
        if (!_identityContext.IsAuthenticated)
        {
            throw new InvalidOperationException("CreateAsync requires an authenticated identity.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Title is required.", nameof(request));
        }

        var exists = await _dbContext.Collections.AnyAsync(c => c.Id == collectionId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var entity = new TtsBacklogItemEntity
        {
            UserId = _identityContext.UserId,
            CollectionId = collectionId,
            Title = request.Title,
            Content = request.Content ?? string.Empty,
            SourceUrl = request.SourceUrl,
            Notes = request.Notes,
        };

        _dbContext.BacklogItems.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToV1();
    }

    public async Task<BacklogItemV1?> UpdateAsync(Guid id, UpdateBacklogItemRequestV1 request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BacklogItems.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new ArgumentException("Title cannot be blank.", nameof(request));
            }

            entity.Title = request.Title;
        }

        if (request.Content is not null) entity.Content = request.Content;
        if (request.SourceUrl is not null) entity.SourceUrl = request.SourceUrl;
        if (request.Notes is not null) entity.Notes = request.Notes;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToV1();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BacklogItems.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _dbContext.BacklogItems.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ConsumeBacklogItemsResponseV1?> ConsumeAsync(Guid collectionId, ConsumeBacklogItemsRequestV1 request, CancellationToken cancellationToken = default)
    {
        var collectionExists = await _dbContext.Collections.AnyAsync(c => c.Id == collectionId, cancellationToken);
        if (!collectionExists)
        {
            return null;
        }

        var recordingExists = await _dbContext.Recordings.AnyAsync(r => r.Id == request.RecordingId, cancellationToken);
        if (!recordingExists)
        {
            return null;
        }

        var requestedIds = request.ItemIds is { Count: > 0 } ? request.ItemIds.Distinct().ToList() : null;

        var candidates = _dbContext.BacklogItems.Where(b => b.CollectionId == collectionId);
        if (requestedIds is not null)
        {
            candidates = candidates.Where(b => requestedIds.Contains(b.Id));
        }
        else
        {
            candidates = candidates.Where(b => b.Status == BacklogItemStatus.Pending);
        }

        var items = await candidates.ToListAsync(cancellationToken);

        var consumedIds = new List<Guid>();
        var now = DateTimeOffset.UtcNow;

        foreach (var item in items.Where(i => i.Status == BacklogItemStatus.Pending))
        {
            item.Status = BacklogItemStatus.Consumed;
            item.ConsumedAt = now;
            item.ConsumedByRecordingId = request.RecordingId;
            consumedIds.Add(item.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var skippedIds = requestedIds?.Where(id => !consumedIds.Contains(id)).ToList() ?? [];

        return new ConsumeBacklogItemsResponseV1
        {
            ConsumedIds = consumedIds,
            SkippedIds = skippedIds,
        };
    }

    public async Task<BacklogItemV1?> DismissAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BacklogItems
            .Include(b => b.ConsumedByRecording)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (entity.Status == BacklogItemStatus.Pending)
        {
            entity.Status = BacklogItemStatus.Dismissed;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return entity.ToV1(entity.ConsumedByRecording?.Name);
    }
}
