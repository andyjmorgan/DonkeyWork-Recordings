namespace DonkeyWork.Recordings.Audio.Core.Options;

public sealed class RecordingsOptions
{
    public const string SectionName = "Recordings";

    public string PublicBaseUrl { get; set; } = "https://recordings.donkeywork.dev";

    public string DefaultFeedTitle { get; set; } = "DonkeyWork Recordings";

    public string DefaultFeedDescription { get; set; } = "Audio recordings synthesised via Magpie TTS.";

    public string DefaultLanguage { get; set; } = "en-US";
}
