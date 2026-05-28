namespace DonkeyWork.Recordings.Persistence.Entities.Tts;

public class TtsPlaybackEntity : BaseEntity
{
    public Guid RecordingId { get; set; }

    public double PositionSeconds { get; set; }

    public double DurationSeconds { get; set; }

    public bool Completed { get; set; }

    public double PlaybackSpeed { get; set; } = 1.0;

    public TtsRecordingEntity Recording { get; set; } = null!;
}
