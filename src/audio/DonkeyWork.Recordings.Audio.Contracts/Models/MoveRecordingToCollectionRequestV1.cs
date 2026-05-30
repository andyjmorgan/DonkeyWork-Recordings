namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class MoveRecordingToCollectionRequestV1
{
    // A recording always belongs to a channel — moving requires a target channel.
    public required Guid CollectionId { get; init; }

    public int? SequenceNumber { get; init; }

    public string? ChapterTitle { get; init; }
}
