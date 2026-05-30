namespace DonkeyWork.Recordings.Audio.Core.Options;

public sealed class TtsOptions
{
    public const string SectionName = "Tts";

    public string DefaultModel { get; set; } = "chatterbox";

    public string DefaultLanguage { get; set; } = "en-US";
}
