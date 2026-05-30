namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class StartAudioGenerationRequestV1
{
    public required string Text { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? TtsModel { get; init; }

    public string? Voice { get; init; }

    public string? Language { get; init; }

    public Guid? CollectionId { get; init; }

    public int? SequenceNumber { get; init; }

    public string? ChapterTitle { get; init; }

    public int TargetCharCount { get; init; } = 1500;

    public int MaxCharCount { get; init; } = 2500;
}
