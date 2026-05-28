namespace DonkeyWork.Recordings.Identity.Contracts.Services;

public interface IIdentityContext
{
    Guid UserId { get; }

    string? Email { get; }

    string? Name { get; }

    string? Username { get; }

    bool IsAuthenticated { get; }

    void SetIdentity(Guid userId, string? email = null, string? name = null, string? username = null);
}
