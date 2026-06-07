namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class AudioCollectionV1
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public string? CoverImagePath { get; init; }

    public string? DefaultVoice { get; init; }

    public string? DefaultLanguage { get; init; }

    public string? Author { get; init; }

    public string? AuthorEmail { get; init; }

    public string? ItunesCategory { get; init; }

    public bool IsExplicit { get; init; }

    public string? Link { get; init; }

    public required int RecordingCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
