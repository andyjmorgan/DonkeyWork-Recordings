using System.Security.Claims;
using System.Text.Encodings.Web;
using DonkeyWork.Recordings.Identity.Contracts.Models;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
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
        var result = await service.ValidateAsync(apiKey);

        if (result is null)
        {
            Logger.LogWarning("Invalid API key presented on {Path}", Request.Path);
            return AuthenticateResult.Fail("Invalid API key");
        }

        if (!IsScopeAllowed(result.Scope))
        {
            Logger.LogWarning(
                "API key scope {Scope} rejected on {Method} {Path}",
                result.Scope, Request.Method, Request.Path);
            return AuthenticateResult.Fail($"API key is restricted to {result.Scope}");
        }

        var identityContext = Context.RequestServices.GetRequiredService<IIdentityContext>();
        identityContext.SetIdentity(result.UserId);

        var claims = new[]
        {
            new Claim("sub", result.UserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    // McpOnly: only the root path (MCP is mounted at "/" and receives POSTs).
    // RestOnly: only /api/* (regular controllers). RestAndMcp: no restriction.
    private bool IsScopeAllowed(ApiKeyScope scope)
    {
        if (scope == ApiKeyScope.RestAndMcp)
        {
            return true;
        }

        var path = Request.Path.Value ?? string.Empty;
        var isMcp = HttpMethods.IsPost(Request.Method)
            && (path == "/" || string.IsNullOrEmpty(path));
        var isRest = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

        return scope switch
        {
            ApiKeyScope.McpOnly => isMcp,
            ApiKeyScope.RestOnly => isRest,
            _ => true,
        };
    }
}
