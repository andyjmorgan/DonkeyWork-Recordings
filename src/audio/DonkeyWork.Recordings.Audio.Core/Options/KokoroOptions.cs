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
}
