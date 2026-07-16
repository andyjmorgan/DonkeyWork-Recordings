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

    // The OpenAI-compatible surface. OpenAI SDKs send the api key as `Authorization: Bearer …`,
    // so on these paths (and only these paths) a Bearer token is treated as a DonkeyWork API key.
    // X-Api-Key keeps working there too. MultiAuth routes every /openai request to this handler.
    public const string OpenAiCompatPathPrefix = "/openai";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = ExtractApiKey();
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

    // Missing/invalid credentials on the OpenAI-compatible surface must produce OpenAI's error
    // envelope (401, code "invalid_api_key") rather than the default empty challenge, so
    // unmodified OpenAI SDKs surface a sensible error. Other paths keep the standard behaviour.
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (IsOpenAiCompatRequest())
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.ContentType = "application/json";
            await Response.WriteAsync(
                """{"error":{"message":"Incorrect API key provided. You can create an API key in the web app under Profile → API Keys.","type":"invalid_request_error","param":null,"code":"invalid_api_key"}}""");
            return;
        }

        await base.HandleChallengeAsync(properties);
    }

    private bool IsOpenAiCompatRequest()
        => Request.Path.StartsWithSegments(OpenAiCompatPathPrefix, StringComparison.OrdinalIgnoreCase);

    private string? ExtractApiKey()
    {
        if (Request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            return headerValue.ToString();
        }

        // Bearer-as-api-key applies strictly to the OpenAI-compatible surface; elsewhere a Bearer
        // token stays a Keycloak JWT handled by the JwtBearer scheme.
        if (IsOpenAiCompatRequest()
            && Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            var value = authorization.ToString();
            const string bearerPrefix = "Bearer ";
            if (value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[bearerPrefix.Length..].Trim();
            }
        }

        return null;
    }

    // McpOnly: only the MCP endpoint (mounted at "/mcp" — see Mcp.Api MapMcp).
    // RestOnly: /api/* (regular controllers) plus the OpenAI-compatible REST surface (/openai/*).
    // RestAndMcp: no restriction.
    private bool IsScopeAllowed(ApiKeyScope scope)
    {
        if (scope == ApiKeyScope.RestAndMcp)
        {
            return true;
        }

        var path = Request.Path.Value ?? string.Empty;
        var isMcp = path.Equals("/mcp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/mcp/", StringComparison.OrdinalIgnoreCase);
        var isRest = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || IsOpenAiCompatRequest();

        return scope switch
        {
            ApiKeyScope.McpOnly => isMcp,
            ApiKeyScope.RestOnly => isRest,
            _ => true,
        };
    }
}
