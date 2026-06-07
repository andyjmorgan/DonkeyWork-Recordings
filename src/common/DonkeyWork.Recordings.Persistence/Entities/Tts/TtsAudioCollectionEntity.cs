namespace DonkeyWork.Recordings.Persistence.Entities.Tts;

public class TtsAudioCollectionEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? CoverImagePath { get; set; }

    public string? DefaultVoice { get; set; }

    public string? DefaultLanguage { get; set; }

    public string? Author { get; set; }

    public string? AuthorEmail { get; set; }

    public string? ItunesCategory { get; set; }

    public bool IsExplicit { get; set; }

    public string? Link { get; set; }

    public ICollection<TtsRecordingEntity> Recordings { get; set; } = new List<TtsRecordingEntity>();
}
