namespace DonkeyWork.Recordings.Audio.Core.Tests;

// Unit test tier. No DB, no HTTP, no filesystem. Pure-function coverage:
//   - TtsChunker (ported verbatim from the extraction)
//   - SsmlPreprocessor (stray token stripping, malformed-input handling)
//   - KokoroTtsProvider with HttpMessageHandler mock
//   - FeedXmlBuilder snapshot tests
public class SkeletonTests
{
    [Fact]
    public void Project_Builds_And_Discovers_Tests()
    {
        Assert.True(true);
    }
}
