namespace DonkeyWork.Recordings.Audio.Core.Helpers;

public static class ChunkWatermark
{
    // Chunk synthesis can complete out of order, so persisted indexes may have gaps. The
    // watermark is the highest index N such that chunks 0..N are ALL persisted — clients only
    // fetch/play up to it so playback never skips a hole. Returns -1 when chunk 0 is missing.
    public static int Compute(IEnumerable<int> persistedIndexes)
    {
        var indexes = persistedIndexes as IReadOnlySet<int> ?? new HashSet<int>(persistedIndexes);

        var watermark = -1;
        while (indexes.Contains(watermark + 1))
        {
            watermark++;
        }

        return watermark;
    }
}
