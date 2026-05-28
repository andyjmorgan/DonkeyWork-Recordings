using DonkeyWork.Recordings.Audio.Contracts.Models;

namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IAudioGenerationService
{
    Task<Guid> StartGenerationAsync(StartAudioGenerationRequestV1 request, CancellationToken cancellationToken = default);
}
