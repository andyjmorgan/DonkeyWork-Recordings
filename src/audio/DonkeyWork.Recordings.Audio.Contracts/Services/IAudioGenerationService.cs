using DonkeyWork.Recordings.Audio.Contracts.Models;

namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IAudioGenerationService
{
    Task<Guid> StartGenerationAsync(StartAudioGenerationRequestV1 request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-synthesise an existing recording from an edited transcript, replacing the stored mp3 in place.
    /// Returns false if the recording does not exist.
    /// </summary>
    Task<bool> RegenerateAsync(Guid recordingId, RegenerateRecordingRequestV1 request, CancellationToken cancellationToken = default);
}
