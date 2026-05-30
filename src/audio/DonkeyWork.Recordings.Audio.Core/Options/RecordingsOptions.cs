namespace DonkeyWork.Recordings.Audio.Core.Options;

public sealed class RecordingsOptions
{
    public const string SectionName = "Recordings";

    public string PublicBaseUrl { get; set; } = "https://recordings.donkeywork.dev";

    public string DefaultCoverImageUrl { get; set; } = "https://s3.donkeywork.dev/images/podcast-covers/donkeywork.jpg";

    public string DefaultFeedTitle { get; set; } = "DonkeyWork Recordings";

    public string DefaultFeedDescription { get; set; } = "Audio recordings generated with DonkeyWork.";

    public string DefaultLanguage { get; set; } = "en-US";
}
