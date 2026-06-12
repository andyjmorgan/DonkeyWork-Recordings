using DonkeyWork.Recordings.Identity.Contracts.Models;

namespace DonkeyWork.Recordings.Persistence.Entities.Credentials;

public class UserApiKeyEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public byte[] EncryptedKey { get; set; } = [];

    /// <summary>
    /// Deterministic SHA-256 (hex) of the plaintext key, used for an O(1) indexed
    /// lookup at validation time instead of decrypting every row. Nullable: rows
    /// created before this column existed carry NULL until backfilled on first use.
    /// </summary>
    public string? KeyHash { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public ApiKeyScope Scope { get; set; } = ApiKeyScope.RestAndMcp;
}
