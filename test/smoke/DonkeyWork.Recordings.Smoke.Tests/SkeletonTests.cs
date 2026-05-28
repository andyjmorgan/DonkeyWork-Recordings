namespace DonkeyWork.Recordings.Smoke.Tests;

// Live smoke tier. Hits the REAL Magpie (port-forward), REAL gpt-oss (port-forward),
// REAL attic SeaweedFS at s3.donkeywork.dev. Every test carries
// [Trait("Category", "LiveSmoke")]. Excluded from CI; run manually:
//
//   kubectl --context=office port-forward -n magpie-tts svc/magpie-tts 9000:9000 &
//   kubectl --context=office port-forward -n ollama-gpt-oss svc/ollama 11434:11434 &
//   dotnet test --filter "Category=LiveSmoke"
//
// First test to add: confirm Magpie /v1/audio/synthesize accepts SSML inline in the
// `text` form field (the answer determines whether SsmlPreprocessor is real or a no-op).
public class SkeletonTests
{
    [Fact]
    [Trait("Category", "LiveSmoke")]
    public void Placeholder_Live_Smoke()
    {
        Assert.True(true);
    }
}
