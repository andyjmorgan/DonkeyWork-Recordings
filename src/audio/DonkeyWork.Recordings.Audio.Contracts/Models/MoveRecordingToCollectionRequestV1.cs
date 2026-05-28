namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class MoveRecordingToCollectionRequestV1
{
    public Guid? CollectionId { get; init; }

    public int? SequenceNumber { get; init; }

    public string? ChapterTitle { get; init; }
}
