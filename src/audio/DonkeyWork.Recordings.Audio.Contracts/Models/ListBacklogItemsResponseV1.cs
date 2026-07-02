namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class ListBacklogItemsResponseV1
{
    public required IReadOnlyList<BacklogItemV1> Items { get; init; }

    public required int TotalCount { get; init; }
}
