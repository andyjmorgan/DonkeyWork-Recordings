namespace DonkeyWork.Recordings.Persistence.Entities.Tts;

public class TtsRecordingEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string Transcript { get; set; } = string.Empty;

    public string ContentType { get; set; } = "audio/mpeg";

    public long SizeBytes { get; set; }

    public double DurationSeconds { get; set; }

    public string? Voice { get; set; }

    public string? Language { get; set; }

    public Guid? CollectionId { get; set; }

    public int? SequenceNumber { get; set; }

    public string? ChapterTitle { get; set; }

    public TtsRecordingStatus Status { get; set; } = TtsRecordingStatus.Pending;

    public double Progress { get; set; }

    public string? StatusDetail { get; set; }

    public string? ErrorMessage { get; set; }

    public TtsAudioCollectionEntity? Collection { get; set; }

    public TtsPlaybackEntity? Playback { get; set; }
}
