namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class CreateBacklogItemRequestV1
{
    public required string Title { get; init; }

    public string? Content { get; init; }

    public string? SourceUrl { get; init; }

    public string? Notes { get; init; }
}
