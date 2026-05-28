namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class FeedSettingsV1
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public string? Author { get; init; }

    public string? AuthorEmail { get; init; }

    public required string Language { get; init; }

    public string? CoverImagePath { get; init; }

    public string? Link { get; init; }

    public required bool IsExplicit { get; init; }

    public string? ItunesCategory { get; init; }

    public required string MasterFeedUrl { get; init; }
}
