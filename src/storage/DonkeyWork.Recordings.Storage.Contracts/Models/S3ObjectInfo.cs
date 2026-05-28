namespace DonkeyWork.Recordings.Storage.Contracts.Models;

public sealed class S3ObjectInfo
{
    public required string Key { get; init; }

    public required long SizeBytes { get; init; }

    public required DateTimeOffset LastModified { get; init; }
}
