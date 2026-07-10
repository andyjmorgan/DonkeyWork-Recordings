namespace DonkeyWork.Recordings.Persistence.Entities.Tts;

// An ephemeral per-chunk WAV clip published while a recording is generating so clients can
// start playback before the final mp3 is stitched. Rows (and their storage objects) are
// swept once the recording settles plus a grace period — see ChunkSweeper.
public class TtsRecordingChunkEntity : BaseEntity
{
    public Guid RecordingId { get; set; }

    public int Index { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public double DurationSeconds { get; set; }

    public TtsRecordingEntity? Recording { get; set; }
}
