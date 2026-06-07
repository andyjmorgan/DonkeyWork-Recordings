namespace DonkeyWork.Recordings.Smoke.Tests;

// Live smoke tier. Hits the REAL Kokoro TTS (port-forward) and the REAL attic SeaweedFS
// at s3.donkeywork.dev. Every test carries [Trait("Category", "LiveSmoke")]. Excluded
// from CI; run manually:
//
//   kubectl --context=office port-forward -n kokoro-tts svc/kokoro-tts 8000:8000 &
//   dotnet test --filter "Category=LiveSmoke"
//
// First test to add: confirm Kokoro /v1/audio/speech returns WAV for a known voice id.
public class SkeletonTests
{
    [Fact]
    [Trait("Category", "LiveSmoke")]
    public void Placeholder_Live_Smoke()
    {
        Assert.True(true);
    }
}
