namespace DonkeyWork.Recordings.Audio.Contracts.Models;

// A progressively published chunk clip of a recording that is still generating. Chunk WAVs are
// ephemeral: they are swept shortly after the recording settles, so clients should switch to the
// final mp3 (FilePath) once the recording is Ready.
public sealed class TtsRecordingChunkV1
{
    public required int Index { get; init; }

    public required string Url { get; init; }

    public required long SizeBytes { get; init; }

    public required double DurationSeconds { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
