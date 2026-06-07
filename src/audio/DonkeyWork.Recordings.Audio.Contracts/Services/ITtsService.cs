using DonkeyWork.Recordings.Audio.Contracts.Models;

namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface ITtsService
{
    Task<ListRecordingsResponseV1> ListRecordingsAsync(int offset, int limit, bool unfiledOnly = false, CancellationToken cancellationToken = default);

    Task<TtsRecordingV1?> GetRecordingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteRecordingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TtsRecordingV1?> UpdateRecordingAsync(Guid recordingId, UpdateRecordingRequestV1 request, CancellationToken cancellationToken = default);

    Task<TtsRecordingV1?> MoveRecordingAsync(Guid recordingId, MoveRecordingToCollectionRequestV1 request, CancellationToken cancellationToken = default);

    Task<TtsPlaybackV1> GetPlaybackAsync(Guid recordingId, CancellationToken cancellationToken = default);

    Task<TtsPlaybackV1?> UpdatePlaybackAsync(Guid recordingId, UpdatePlaybackRequestV1 request, CancellationToken cancellationToken = default);
}
