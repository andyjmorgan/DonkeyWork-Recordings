namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class ConsumeBacklogItemsRequestV1
{
    public required Guid RecordingId { get; init; }

    /// <summary>Item ids to consume. Null or empty consumes every pending item in the collection.</summary>
    public IReadOnlyList<Guid>? ItemIds { get; init; }
}
