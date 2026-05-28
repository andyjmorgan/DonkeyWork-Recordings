using DonkeyWork.Recordings.Storage.Contracts.Models;

namespace DonkeyWork.Recordings.Storage.Contracts.Services;

public interface IS3ClientWrapper
{
    Task UploadAsync(
        string bucketName,
        string objectKey,
        Stream content,
        string contentType,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default);

    Task DeleteByPrefixAsync(string bucketName, string prefix, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<S3ObjectInfo>> ListObjectsAsync(
        string bucketName,
        string prefix,
        CancellationToken cancellationToken = default);
}
