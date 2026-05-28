namespace DonkeyWork.Recordings.Storage.Core.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string ServiceUrl { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string DefaultBucket { get; set; } = "recordings";

    public bool UsePathStyleAddressing { get; set; } = true;

    public string? PublicServiceUrl { get; set; }
}
