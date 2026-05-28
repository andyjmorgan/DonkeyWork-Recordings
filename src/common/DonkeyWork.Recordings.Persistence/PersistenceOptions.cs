namespace DonkeyWork.Recordings.Persistence;

public class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string ConnectionString { get; set; } = string.Empty;

    public string EncryptionKey { get; set; } = string.Empty;
}
