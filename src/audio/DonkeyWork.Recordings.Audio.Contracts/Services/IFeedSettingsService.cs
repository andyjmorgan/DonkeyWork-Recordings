using DonkeyWork.Recordings.Audio.Contracts.Models;

namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IFeedSettingsService
{
    Task<FeedSettingsV1> GetAsync(string requestOrigin, CancellationToken cancellationToken = default);

    Task<FeedSettingsV1> UpdateAsync(UpdateFeedSettingsRequestV1 request, string requestOrigin, CancellationToken cancellationToken = default);
}
