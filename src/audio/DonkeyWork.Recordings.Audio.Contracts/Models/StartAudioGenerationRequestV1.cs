namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class StartAudioGenerationRequestV1
{
    public required string Text { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    // A recording always belongs to a channel; the channel supplies the default voice/model.
    public required Guid CollectionId { get; init; }

    // Optional overrides — default to the channel's settings when omitted.
    public string? TtsModel { get; init; }

    public string? Voice { get; init; }

    public string? Language { get; init; }

    public int? SequenceNumber { get; init; }

    public string? ChapterTitle { get; init; }

    public int TargetCharCount { get; init; } = 1500;

    public int MaxCharCount { get; init; } = 2500;
}
