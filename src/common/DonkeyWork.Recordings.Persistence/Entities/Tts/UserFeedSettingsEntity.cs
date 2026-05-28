namespace DonkeyWork.Recordings.Persistence.Entities.Tts;

public class UserFeedSettingsEntity : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Author { get; set; }

    public string? AuthorEmail { get; set; }

    public string Language { get; set; } = "en-US";

    public string? CoverImagePath { get; set; }

    public string? Link { get; set; }

    public bool IsExplicit { get; set; }

    public string? ItunesCategory { get; set; }
}
