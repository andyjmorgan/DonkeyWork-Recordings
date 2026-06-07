namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class StartAudioGenerationRequestV1
{
    // The text already split into ordered spoken paragraphs by the caller.
    public required IReadOnlyList<string> Paragraphs { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    // A recording always belongs to a channel; the channel supplies the default voice.
    public required Guid CollectionId { get; init; }

    // Optional override — defaults to the channel's voice when omitted.
    public string? Voice { get; init; }

    public string? Language { get; init; }

    public int? SequenceNumber { get; init; }

    public string? ChapterTitle { get; init; }

    public int TargetCharCount { get; init; } = 1500;

    public int MaxCharCount { get; init; } = 2500;
}
