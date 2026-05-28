namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class ListRecordingsResponseV1
{
    public required IReadOnlyList<TtsRecordingV1> Items { get; init; }

    public required int TotalCount { get; init; }
}
