namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class CreateAudioCollectionRequestV1
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? CoverImagePath { get; init; }

    public string? DefaultTtsModel { get; init; }

    public string? DefaultVoice { get; init; }

    public string? DefaultLanguage { get; init; }

    public string? Tone { get; init; }

    public string? Author { get; init; }

    public string? AuthorEmail { get; init; }

    public string? ItunesCategory { get; init; }

    public bool IsExplicit { get; init; }

    public string? Link { get; init; }
}
