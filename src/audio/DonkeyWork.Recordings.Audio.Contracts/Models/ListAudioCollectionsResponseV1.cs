namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class ListAudioCollectionsResponseV1
{
    public required IReadOnlyList<AudioCollectionV1> Items { get; init; }

    public required int TotalCount { get; init; }
}
