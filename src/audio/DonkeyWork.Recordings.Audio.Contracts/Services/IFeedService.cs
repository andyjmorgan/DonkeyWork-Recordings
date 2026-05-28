namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface IFeedService
{
    Task<string?> BuildMasterFeedAsync(Guid userId, string requestOrigin, CancellationToken cancellationToken = default);

    Task<string?> BuildChannelFeedAsync(Guid userId, Guid collectionId, string requestOrigin, CancellationToken cancellationToken = default);
}
