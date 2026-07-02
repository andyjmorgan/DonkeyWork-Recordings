using DonkeyWork.Recordings.Audio.Contracts.Models;

namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IBacklogService
{
    /// <summary>Lists backlog items for a collection. Returns null when the collection does not exist.
    /// Status filters to a single <c>BacklogItemStatus</c>; null or "all" returns every item.</summary>
    Task<ListBacklogItemsResponseV1?> ListAsync(Guid collectionId, string? status, int offset, int limit, CancellationToken cancellationToken = default);

    Task<BacklogItemV1?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a pending backlog item. Returns null when the collection does not exist.</summary>
    Task<BacklogItemV1?> CreateAsync(Guid collectionId, CreateBacklogItemRequestV1 request, CancellationToken cancellationToken = default);

    Task<BacklogItemV1?> UpdateAsync(Guid id, UpdateBacklogItemRequestV1 request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marks pending items consumed by a recording. Returns null when the collection or recording does not exist.
    /// Idempotent: items that are not pending are skipped and keep their original consumption link.</summary>
    Task<ConsumeBacklogItemsResponseV1?> ConsumeAsync(Guid collectionId, ConsumeBacklogItemsRequestV1 request, CancellationToken cancellationToken = default);

    /// <summary>Dismisses a pending item without consuming it. Idempotent; non-pending items are returned unchanged.</summary>
    Task<BacklogItemV1?> DismissAsync(Guid id, CancellationToken cancellationToken = default);
}
