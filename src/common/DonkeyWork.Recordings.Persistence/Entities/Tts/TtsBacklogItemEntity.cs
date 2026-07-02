namespace DonkeyWork.Recordings.Persistence.Entities.Tts;

public class TtsBacklogItemEntity : BaseEntity
{
    public Guid CollectionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? SourceUrl { get; set; }

    public string? Notes { get; set; }

    public BacklogItemStatus Status { get; set; } = BacklogItemStatus.Pending;

    public DateTimeOffset? ConsumedAt { get; set; }

    public Guid? ConsumedByRecordingId { get; set; }

    public TtsAudioCollectionEntity? Collection { get; set; }

    public TtsRecordingEntity? ConsumedByRecording { get; set; }
}
