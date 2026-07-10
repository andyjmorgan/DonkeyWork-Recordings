namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IRecordingEventStream
{
    // Streams Server-Sent Events for a recording to <paramref name="output"/> until the recording
    // settles (ready/failed) or the caller cancels. Replays current state on connect: already
    // persisted chunks are emitted first (as chunk-ready events), then live events follow. If the
    // recording is already Ready/Failed the terminal event is emitted immediately and the stream
    // completes. The caller owns the output stream and the SSE response headers.
    Task StreamAsync(Guid recordingId, Stream output, CancellationToken cancellationToken = default);
}
