namespace DonkeyWork.Recordings.Audio.Core.Tests;

// Unit test tier. No DB, no HTTP, no filesystem. Pure-function coverage:
//   - TtsChunker (ported verbatim from the extraction)
//   - SsmlPreprocessor (token → SSML, malformed-input handling)
//   - MagpieTtsProvider with HttpMessageHandler mock
//   - GptOssPreprocessor with mocked client (incl. reasoning-field stripping)
//   - FeedXmlBuilder snapshot tests
public class SkeletonTests
{
    [Fact]
    public void Project_Builds_And_Discovers_Tests()
    {
        Assert.True(true);
    }
}
