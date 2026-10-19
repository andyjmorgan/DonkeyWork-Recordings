namespace DonkeyWork.Recordings.Audio.Core.Options;

public sealed class KokoroOptions
{
    public const string SectionName = "Kokoro";

    public string BaseUrl { get; set; } = "http://kokoro-tts.kokoro-tts.svc.cluster.local:8000";

    public string DefaultVoice { get; set; } = "af_heart";

    public string SamplesBaseUrl { get; set; } = "https://s3.donkeywork.dev/kokoro-samples";

    public int SampleRateHz { get; set; } = 24000;

    public double Speed { get; set; } = 1.0;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    // Retry for transient synth failures — a replica dying mid-request (OOMKill, rollout, node drain)
    // surfaces as a connection reset; the retry opens a new connection and lands on a healthy replica.
    // Without it one dropped request fails a whole multi-chunk recording.
    public int MaxAttempts { get; set; } = 4;

    // Backoff before attempt n (n >= 2) is RetryBaseDelay * 2^(n-2): 2s, 4s, 8s by default.
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);
}
