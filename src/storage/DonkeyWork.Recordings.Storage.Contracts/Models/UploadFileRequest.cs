namespace DonkeyWork.Recordings.Storage.Contracts.Models;

public sealed class UploadFileRequest
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required Stream Content { get; init; }

    public string? KeyPrefix { get; init; }

    public bool AbsoluteKeyPrefix { get; init; }

    public IDictionary<string, string>? Metadata { get; init; }
}
