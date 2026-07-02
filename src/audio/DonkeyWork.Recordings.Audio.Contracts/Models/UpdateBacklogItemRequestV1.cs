namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class UpdateBacklogItemRequestV1
{
    public string? Title { get; init; }

    public string? Content { get; init; }

    public string? SourceUrl { get; init; }

    public string? Notes { get; init; }
}
