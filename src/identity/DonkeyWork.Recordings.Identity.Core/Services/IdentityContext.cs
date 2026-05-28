using DonkeyWork.Recordings.Identity.Contracts.Services;

namespace DonkeyWork.Recordings.Identity.Core.Services;

public sealed class IdentityContext : IIdentityContext
{
    public Guid UserId { get; private set; }

    public string? Email { get; private set; }

    public string? Name { get; private set; }

    public string? Username { get; private set; }

    public bool IsAuthenticated => UserId != Guid.Empty;

    public void SetIdentity(Guid userId, string? email = null, string? name = null, string? username = null)
    {
        UserId = userId;
        Email = email;
        Name = name;
        Username = username;
    }
}
