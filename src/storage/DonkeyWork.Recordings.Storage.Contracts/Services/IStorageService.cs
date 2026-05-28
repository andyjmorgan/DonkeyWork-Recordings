using DonkeyWork.Recordings.Storage.Contracts.Models;

namespace DonkeyWork.Recordings.Storage.Contracts.Services;

public interface IStorageService
{
    Task<StorageUploadResult> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    Task DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    string GetPublicUrl(string objectKey);
}
