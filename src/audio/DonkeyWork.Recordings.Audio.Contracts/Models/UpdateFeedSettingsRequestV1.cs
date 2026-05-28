namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class UpdateFeedSettingsRequestV1
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Author { get; init; }

    public string? AuthorEmail { get; init; }

    public string? Language { get; init; }

    public string? CoverImagePath { get; init; }

    public string? Link { get; init; }

    public bool? IsExplicit { get; init; }

    public string? ItunesCategory { get; init; }
}
