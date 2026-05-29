using DonkeyWork.Recordings.Identity.Contracts.Models;

namespace DonkeyWork.Recordings.Identity.Contracts.Services;

public interface IUserApiKeyService
{
    Task<IReadOnlyList<UserApiKey>> ListAsync(CancellationToken cancellationToken = default);

    Task<UserApiKey> CreateAsync(string name, string? description, ApiKeyScope scope, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Used by the ApiKey authentication handler. Returns the owning user id
    // and the key's scope if the supplied secret matches a stored key, else
    // null. Stamps LastUsedAt as a side effect.
    Task<ApiKeyValidationResult?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);
}
