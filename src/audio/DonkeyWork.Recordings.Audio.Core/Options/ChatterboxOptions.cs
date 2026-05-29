namespace DonkeyWork.Recordings.Audio.Core.Options;

public sealed class ChatterboxOptions
{
    public const string SectionName = "Chatterbox";

    public string BaseUrl { get; set; } = "http://192.168.69.27:30880";

    public string DefaultLanguage { get; set; } = "en-US";

    public string DefaultVoice { get; set; } = "default";

    public int SampleRateHz { get; set; } = 24000;

    public double Exaggeration { get; set; } = 0.5;

    public double CfgWeight { get; set; } = 0.5;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
