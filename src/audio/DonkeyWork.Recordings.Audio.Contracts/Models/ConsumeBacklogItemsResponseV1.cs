namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class ConsumeBacklogItemsResponseV1
{
    public required IReadOnlyList<Guid> ConsumedIds { get; init; }

    /// <summary>Ids requested but not transitioned (already consumed/dismissed, or not pending in this collection).</summary>
    public required IReadOnlyList<Guid> SkippedIds { get; init; }
}
