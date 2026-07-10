using DonkeyWork.Recordings.Audio.Core.Helpers;

namespace DonkeyWork.Recordings.Audio.Core.Tests;

public class ChunkWatermarkTests
{
    [Fact]
    public void Empty_Set_Has_No_Watermark()
    {
        Assert.Equal(-1, ChunkWatermark.Compute([]));
    }

    [Fact]
    public void Missing_Chunk_Zero_Has_No_Watermark()
    {
        // Chunk 0 hasn't landed yet — nothing is playable even though later chunks exist.
        Assert.Equal(-1, ChunkWatermark.Compute([1, 2, 3]));
    }

    [Fact]
    public void Single_Chunk_Zero_Is_Playable()
    {
        Assert.Equal(0, ChunkWatermark.Compute([0]));
    }

    [Fact]
    public void Contiguous_Prefix_Advances_To_Last_Index()
    {
        Assert.Equal(3, ChunkWatermark.Compute([0, 1, 2, 3]));
    }

    [Fact]
    public void Gap_Freezes_Watermark_Before_The_Hole()
    {
        // Chunks complete out of order under parallel synthesis: 0, 1 and 4 are persisted but
        // 2 is still in flight, so playback must stop at 1.
        Assert.Equal(1, ChunkWatermark.Compute([0, 1, 4, 3]));
    }

    [Fact]
    public void Out_Of_Order_Arrival_Does_Not_Matter()
    {
        Assert.Equal(2, ChunkWatermark.Compute([2, 0, 1]));
    }

    [Fact]
    public void Filling_A_Gap_Unlocks_The_Suffix()
    {
        var indexes = new HashSet<int> { 0, 2, 3 };
        Assert.Equal(0, ChunkWatermark.Compute(indexes));

        indexes.Add(1);
        Assert.Equal(3, ChunkWatermark.Compute(indexes));
    }
}
