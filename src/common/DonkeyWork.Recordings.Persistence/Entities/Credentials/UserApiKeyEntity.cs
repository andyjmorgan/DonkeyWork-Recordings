using DonkeyWork.Recordings.Identity.Contracts.Models;

namespace DonkeyWork.Recordings.Persistence.Entities.Credentials;

public class UserApiKeyEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public byte[] EncryptedKey { get; set; } = [];

    public DateTimeOffset? LastUsedAt { get; set; }

    public ApiKeyScope Scope { get; set; } = ApiKeyScope.RestAndMcp;
}
