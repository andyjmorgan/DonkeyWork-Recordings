using DonkeyWork.Recordings.Identity.Contracts.Models;

namespace DonkeyWork.Recordings.Identity.Contracts.Services;

public interface IUserApiKeyService
{
    Task<IReadOnlyList<UserApiKey>> ListAsync(CancellationToken cancellationToken = default);

    Task<UserApiKey> CreateAsync(string name, string? description, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Used by the ApiKey authentication handler. Returns the owning user id
    // if the supplied secret matches a stored (encrypted) key, else null.
    Task<Guid?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);
}
