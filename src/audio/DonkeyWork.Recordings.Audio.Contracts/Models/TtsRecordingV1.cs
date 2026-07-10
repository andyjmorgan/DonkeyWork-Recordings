namespace DonkeyWork.Recordings.Audio.Contracts.Models;

public sealed class TtsRecordingV1
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string FilePath { get; init; }

    public required string Transcript { get; init; }

    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }

    public required double DurationSeconds { get; init; }

    public string? Voice { get; init; }

    public string? Language { get; init; }

    public Guid? CollectionId { get; init; }

    public int? SequenceNumber { get; init; }

    public string? ChapterTitle { get; init; }

    public required string Status { get; init; }

    public required double Progress { get; init; }

    public string? StatusDetail { get; init; }

    public string? ErrorMessage { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    // Chunk clips published while the recording is generating, ordered by index. Empty once the
    // chunks have been swept (shortly after the recording settles) or when none exist yet.
    public IReadOnlyList<TtsRecordingChunkV1> Chunks { get; init; } = [];

    // Highest index N such that chunks 0..N are all persisted (contiguous watermark); -1 if none.
    // Clients doing progressive playback should only fetch/play chunks up to this watermark.
    public int PlayableUpTo { get; init; } = -1;
}
