namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class RegenerateRecordingRequestV1
{
    // The edited transcript, already split into ordered spoken paragraphs by the caller.
    // The audio is re-synthesised and the existing mp3 is replaced in place.
    public required IReadOnlyList<string> Paragraphs { get; init; }

    public int TargetCharCount { get; init; } = 1500;

    public int MaxCharCount { get; init; } = 2500;
}
