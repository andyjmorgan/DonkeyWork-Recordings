using Amazon.S3;
using Amazon.S3.Model;
using DonkeyWork.Recordings.Storage.Contracts.Models;
using DonkeyWork.Recordings.Storage.Contracts.Services;
using DonkeyWork.Recordings.Storage.Core.Options;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Storage.Core.Services;

public sealed class S3ClientWrapper : IS3ClientWrapper, IDisposable
{
    private readonly IAmazonS3 _s3Client;

    public S3ClientWrapper(IOptions<StorageOptions> options)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.Value.ServiceUrl,
            ForcePathStyle = options.Value.UsePathStyleAddressing,
            AuthenticationRegion = "us-east-1",
        };

        _s3Client = new AmazonS3Client(
            options.Value.AccessKey,
            options.Value.SecretKey,
            config);
    }

    public async Task UploadAsync(
        string bucketName,
        string objectKey,
        Stream content,
        string contentType,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        };

        if (metadata is not null)
        {
            foreach (var (key, value) in metadata)
            {
                request.Metadata.Add(key, value);
            }
        }

        await _s3Client.PutObjectAsync(request, cancellationToken);
    }

    public async Task DeleteAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        await _s3Client.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = bucketName, Key = objectKey },
            cancellationToken);
    }

    public async Task<IReadOnlyList<S3ObjectInfo>> ListObjectsAsync(
        string bucketName,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var results = new List<S3ObjectInfo>();
        string? continuationToken = null;

        do
        {
            var response = await _s3Client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken,
                },
                cancellationToken);

            if (response.S3Objects is not null)
            {
                foreach (var obj in response.S3Objects)
                {
                    results.Add(new S3ObjectInfo
                    {
                        Key = obj.Key,
                        SizeBytes = obj.Size,
                        LastModified = new DateTimeOffset(obj.LastModified, TimeSpan.Zero),
                    });
                }
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return results;
    }

    public async Task DeleteByPrefixAsync(string bucketName, string prefix, CancellationToken cancellationToken = default)
    {
        var objects = await ListObjectsAsync(bucketName, prefix, cancellationToken);

        if (objects.Count == 0)
        {
            return;
        }

        foreach (var batch in objects.Chunk(1000))
        {
            await _s3Client.DeleteObjectsAsync(
                new DeleteObjectsRequest
                {
                    BucketName = bucketName,
                    Objects = batch.Select(o => new KeyVersion { Key = o.Key }).ToList(),
                },
                cancellationToken);
        }
    }

    public void Dispose()
    {
        _s3Client.Dispose();
    }
}
