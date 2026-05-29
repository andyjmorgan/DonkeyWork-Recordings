namespace DonkeyWork.Recordings.Identity.Contracts.Models;

public sealed class UserApiKey
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    // Either the full secret (returned exactly once on create) or a masked
    // form (`dk_abc***xyz`) when listed afterwards.
    public required string Key { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    public required ApiKeyScope Scope { get; init; }
}

public enum ApiKeyScope
{
    RestAndMcp = 0,
    McpOnly = 1,
    RestOnly = 2,
}

public sealed record ApiKeyValidationResult(Guid UserId, ApiKeyScope Scope);
