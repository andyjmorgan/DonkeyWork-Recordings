namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface ITtsChunker
{
    IReadOnlyList<string> Chunk(string text, ChunkerOptions options);
}

public sealed class ChunkerOptions
{
    public int TargetCharCount { get; init; } = 1500;

    public int MaxCharCount { get; init; } = 2500;
}
