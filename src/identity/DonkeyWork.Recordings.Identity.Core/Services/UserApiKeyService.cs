using System.Security.Cryptography;
using System.Text;
using DonkeyWork.Recordings.Identity.Contracts.Models;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Credentials;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Identity.Core.Services;

public sealed class UserApiKeyService : IUserApiKeyService
{
    private const string KeyPrefix = "dk_";

    private readonly RecordingsDbContext _dbContext;
    private readonly IIdentityContext _identityContext;
    private readonly byte[] _encryptionKey;

    public UserApiKeyService(
        RecordingsDbContext dbContext,
        IIdentityContext identityContext,
        IOptions<PersistenceOptions> persistenceOptions)
    {
        _dbContext = dbContext;
        _identityContext = identityContext;
        _encryptionKey = DeriveKey(persistenceOptions.Value.EncryptionKey);
    }

    public async Task<IReadOnlyList<UserApiKey>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.UserApiKeys
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => ToModel(e, masked: true)).ToList();
    }

    public async Task<UserApiKey> CreateAsync(string name, string? description, ApiKeyScope scope, CancellationToken cancellationToken = default)
    {
        var apiKey = GenerateApiKey();

        var entity = new UserApiKeyEntity
        {
            UserId = _identityContext.UserId,
            Name = name,
            Description = description ?? string.Empty,
            EncryptedKey = Encrypt(apiKey),
            KeyHash = ComputeLookupHash(apiKey),
            Scope = scope,
        };

        _dbContext.UserApiKeys.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserApiKey
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Name = entity.Name,
            Description = string.IsNullOrEmpty(entity.Description) ? null : entity.Description,
            Key = apiKey,
            CreatedAt = entity.CreatedAt,
            LastUsedAt = entity.LastUsedAt,
            Scope = scope,
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.UserApiKeys
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        _dbContext.UserApiKeys.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ApiKeyValidationResult?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        // The global query filter scopes UserApiKeys to the current user, but
        // at authentication time no user is known yet — bypass it. AsNoTracking
        // so the loaded entities don't pollute the change tracker for any
        // downstream controller queries on the same scoped DbContext.
        var hash = ComputeLookupHash(apiKey);

        // Fast path: indexed O(1) lookup by deterministic hash. The hash is a
        // SHA-256 of the (high-entropy) key, so a match authenticates without
        // decrypting anything.
        var entity = await _dbContext.UserApiKeys
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.KeyHash == hash, cancellationToken);

        // Slow path, transitional only: rows created before key_hash existed
        // have a NULL hash and can't be found by index. Scan just those, verify
        // by decrypt, and backfill the hash so the key uses the fast path next
        // time. The scan shrinks to nothing as keys age over.
        entity ??= await FindLegacyByDecryptAndBackfillAsync(apiKey, hash, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        // Stamp LastUsedAt via ExecuteUpdate so we don't drag the
        // AuditableInterceptor along (UpdatedAt should track real edits,
        // not every authenticated request).
        var now = DateTimeOffset.UtcNow;
        await _dbContext.UserApiKeys
            .IgnoreQueryFilters()
            .Where(e => e.Id == entity.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastUsedAt, now), cancellationToken);

        return new ApiKeyValidationResult(entity.UserId, entity.Scope);
    }

    private async Task<UserApiKeyEntity?> FindLegacyByDecryptAndBackfillAsync(
        string apiKey, string hash, CancellationToken cancellationToken)
    {
        var legacy = await _dbContext.UserApiKeys
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.KeyHash == null)
            .ToListAsync(cancellationToken);

        foreach (var entity in legacy)
        {
            string decrypted;
            try
            {
                decrypted = Decrypt(entity.EncryptedKey);
            }
            catch (CryptographicException)
            {
                // Skip rows that fail to decrypt (e.g. encryption key rotated).
                continue;
            }

            if (!FixedTimeEquals(decrypted, apiKey))
            {
                continue;
            }

            await _dbContext.UserApiKeys
                .IgnoreQueryFilters()
                .Where(e => e.Id == entity.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.KeyHash, hash), cancellationToken);

            return entity;
        }

        return null;
    }

    private static string ComputeLookupHash(string apiKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));

    private UserApiKey ToModel(UserApiKeyEntity entity, bool masked)
    {
        var key = Decrypt(entity.EncryptedKey);

        return new UserApiKey
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Name = entity.Name,
            Description = string.IsNullOrEmpty(entity.Description) ? null : entity.Description,
            Key = masked ? MaskKey(key) : key,
            CreatedAt = entity.CreatedAt,
            LastUsedAt = entity.LastUsedAt,
            Scope = entity.Scope,
        };
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[40];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var base64 = Convert.ToBase64String(bytes)
            .Replace("+", string.Empty)
            .Replace("/", string.Empty)
            .Replace("=", string.Empty);
        return $"{KeyPrefix}{base64[..40]}";
    }

    private static string MaskKey(string key)
    {
        if (key.Length <= 10)
        {
            return key;
        }

        var prefix = key[..7];
        var suffix = key[^3..];
        return $"{prefix}***{suffix}";
    }

    private byte[] Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.CopyTo(result, 0);
        cipherBytes.CopyTo(result, aes.IV.Length);
        return result;
    }

    private string Decrypt(byte[] cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;

        var iv = new byte[16];
        var cipher = new byte[cipherText.Length - 16];
        Array.Copy(cipherText, 0, iv, 0, 16);
        Array.Copy(cipherText, 16, cipher, 0, cipher.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] DeriveKey(string password)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    }
}
