using System.Security.Claims;
using System.Text.Encodings.Web;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Identity.Api.Authentication;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = headerValue.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var service = Context.RequestServices.GetRequiredService<IUserApiKeyService>();
        var userId = await service.ValidateAsync(apiKey);

        if (userId is null)
        {
            Logger.LogWarning("Invalid API key presented on {Path}", Request.Path);
            return AuthenticateResult.Fail("Invalid API key");
        }

        var identityContext = Context.RequestServices.GetRequiredService<IIdentityContext>();
        identityContext.SetIdentity(userId.Value);

        var claims = new[]
        {
            new Claim("sub", userId.Value.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
