using DonkeyWork.Recordings.Audio.Contracts.Models;

namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IAudioCollectionService
{
    Task<ListAudioCollectionsResponseV1> ListAsync(int offset, int limit, CancellationToken cancellationToken = default);

    Task<AudioCollectionV1?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AudioCollectionV1> CreateAsync(CreateAudioCollectionRequestV1 request, CancellationToken cancellationToken = default);

    Task<AudioCollectionV1?> UpdateAsync(Guid id, UpdateAudioCollectionRequestV1 request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ListRecordingsResponseV1?> ListRecordingsAsync(Guid id, int offset, int limit, CancellationToken cancellationToken = default);
}
