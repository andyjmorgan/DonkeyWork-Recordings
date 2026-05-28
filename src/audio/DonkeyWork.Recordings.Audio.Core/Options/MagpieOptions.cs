namespace DonkeyWork.Recordings.Audio.Core.Options;

public sealed class MagpieOptions
{
    public const string SectionName = "Magpie";

    public string BaseUrl { get; set; } = "http://magpie-tts.magpie-tts.svc.cluster.local:9000";

    public string DefaultLanguage { get; set; } = "en-US";

    public string DefaultVoice { get; set; } = "Magpie-Multilingual.EN-US.Aria";

    public int SampleRateHz { get; set; } = 22050;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
