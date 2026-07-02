namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class BacklogItemV1
{
    public required Guid Id { get; init; }

    public required Guid CollectionId { get; init; }

    public required string Title { get; init; }

    public required string Content { get; init; }

    public string? SourceUrl { get; init; }

    public string? Notes { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset? ConsumedAt { get; init; }

    public Guid? ConsumedByRecordingId { get; init; }

    public string? ConsumedByRecordingName { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
