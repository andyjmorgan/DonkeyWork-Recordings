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

    public async Task<UserApiKey> CreateAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        var apiKey = GenerateApiKey();

        var entity = new UserApiKeyEntity
        {
            UserId = _identityContext.UserId,
            Name = name,
            Description = description ?? string.Empty,
            EncryptedKey = Encrypt(apiKey),
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

    public async Task<Guid?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        // The query filter scopes UserApiKeys to the current user, but at
        // authentication time no user is known yet — bypass it to scan all
        // rows, then return the owning user id.
        var entities = await _dbContext.UserApiKeys
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        foreach (var entity in entities)
        {
            try
            {
                if (Decrypt(entity.EncryptedKey) == apiKey)
                {
                    return entity.UserId;
                }
            }
            catch (CryptographicException)
            {
                // Skip rows that fail to decrypt (e.g. encryption key rotated)
                // rather than failing the entire lookup.
            }
        }

        return null;
    }

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
