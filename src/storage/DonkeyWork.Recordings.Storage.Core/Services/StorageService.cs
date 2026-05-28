using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Storage.Contracts.Models;
using DonkeyWork.Recordings.Storage.Contracts.Services;
using DonkeyWork.Recordings.Storage.Core.Options;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Storage.Core.Services;

public sealed class StorageService : IStorageService
{
    private readonly IS3ClientWrapper _s3;
    private readonly IIdentityContext _identityContext;
    private readonly StorageOptions _options;

    public StorageService(
        IS3ClientWrapper s3,
        IIdentityContext identityContext,
        IOptions<StorageOptions> options)
    {
        _s3 = s3;
        _identityContext = identityContext;
        _options = options.Value;
    }

    public async Task<StorageUploadResult> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default)
    {
        var objectKey = BuildObjectKey(request.FileName, request.KeyPrefix, request.AbsoluteKeyPrefix);

        var length = request.Content.CanSeek ? request.Content.Length : 0L;

        await _s3.UploadAsync(
            _options.DefaultBucket,
            objectKey,
            request.Content,
            request.ContentType,
            request.Metadata,
            cancellationToken);

        return new StorageUploadResult
        {
            ObjectKey = objectKey,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = length,
            PublicUrl = GetPublicUrl(objectKey),
        };
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        => _s3.DeleteAsync(_options.DefaultBucket, objectKey, cancellationToken);

    public Task DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => _s3.DeleteByPrefixAsync(_options.DefaultBucket, prefix, cancellationToken);

    public string GetPublicUrl(string objectKey)
    {
        var baseUrl = (_options.PublicServiceUrl ?? _options.ServiceUrl).TrimEnd('/');
        return $"{baseUrl}/{_options.DefaultBucket}/{objectKey}";
    }

    private string BuildObjectKey(string fileName, string? keyPrefix, bool absoluteKeyPrefix)
    {
        if (absoluteKeyPrefix)
        {
            return string.IsNullOrEmpty(keyPrefix)
                ? fileName
                : $"{keyPrefix.TrimEnd('/')}/{fileName}";
        }

        var userId = _identityContext.UserId;
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "UploadAsync requires an authenticated identity (or AbsoluteKeyPrefix=true).");
        }

        return string.IsNullOrEmpty(keyPrefix)
            ? $"{userId}/{fileName}"
            : $"{userId}/{keyPrefix.Trim('/')}/{fileName}";
    }
}
