using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using DonkeyWork.Recordings.Identity.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private const string CodeVerifierCookieName = "pkce_code_verifier";

    private readonly KeycloakOptions _keycloakOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOptions<KeycloakOptions> keycloakOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<AuthController> logger)
    {
        _keycloakOptions = keycloakOptions.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string EffectiveClientId => !string.IsNullOrEmpty(_keycloakOptions.ClientId)
        ? _keycloakOptions.ClientId
        : _keycloakOptions.Audience;

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? idpHint = null)
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        Response.Cookies.Append(CodeVerifierCookieName, codeVerifier, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
        });

        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/v1/auth/callback";

        var authorizationUrl = $"{_keycloakOptions.Authority}/protocol/openid-connect/auth?" +
            $"client_id={Uri.EscapeDataString(EffectiveClientId)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString("openid profile email")}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            $"&code_challenge_method=S256";

        if (!string.IsNullOrEmpty(idpHint))
        {
            authorizationUrl += $"&kc_idp_hint={Uri.EscapeDataString(idpHint)}";
        }

        return Redirect(authorizationUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription)
    {
        var frontendBaseUrl = !string.IsNullOrEmpty(_keycloakOptions.FrontendUrl)
            ? _keycloakOptions.FrontendUrl.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host}";
        var frontendCallbackUrl = $"{frontendBaseUrl}/login/callback";

        if (!string.IsNullOrEmpty(error))
        {
            return Redirect($"{frontendCallbackUrl}#error={Uri.EscapeDataString(error)}&error_description={Uri.EscapeDataString(errorDescription ?? string.Empty)}");
        }

        if (string.IsNullOrEmpty(code))
        {
            return Redirect($"{frontendCallbackUrl}#error=missing_code&error_description={Uri.EscapeDataString("Authorization code is required.")}");
        }

        if (!Request.Cookies.TryGetValue(CodeVerifierCookieName, out var codeVerifier) || string.IsNullOrEmpty(codeVerifier))
        {
            return Redirect($"{frontendCallbackUrl}#error=missing_verifier&error_description={Uri.EscapeDataString("PKCE code verifier not found. Restart the login flow.")}");
        }

        Response.Cookies.Delete(CodeVerifierCookieName);

        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/v1/auth/callback";
        var tokenEndpoint = ResolveTokenEndpoint();

        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = EffectiveClientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
        };

        if (!string.IsNullOrEmpty(_keycloakOptions.ClientSecret))
        {
            tokenRequest["client_secret"] = _keycloakOptions.ClientSecret;
        }

        var httpClient = _httpClientFactory.CreateClient();
        var tokenResponse = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(tokenRequest));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorContent = await tokenResponse.Content.ReadAsStringAsync();
            return Redirect($"{frontendCallbackUrl}#error=token_exchange_failed&error_description={Uri.EscapeDataString(errorContent)}");
        }

        var tokens = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            return Redirect($"{frontendCallbackUrl}#error=invalid_token_response&error_description={Uri.EscapeDataString("Failed to parse token response.")}");
        }

        var fragmentParams = new List<string>
        {
            $"access_token={Uri.EscapeDataString(tokens.AccessToken)}",
            $"expires_in={tokens.ExpiresIn}",
            $"token_type={Uri.EscapeDataString(tokens.TokenType ?? "Bearer")}",
        };

        if (!string.IsNullOrEmpty(tokens.RefreshToken))
        {
            fragmentParams.Add($"refresh_token={Uri.EscapeDataString(tokens.RefreshToken)}");
        }

        if (!string.IsNullOrEmpty(tokens.IdToken))
        {
            fragmentParams.Add($"id_token={Uri.EscapeDataString(tokens.IdToken)}");
        }

        return Redirect($"{frontendCallbackUrl}#{string.Join("&", fragmentParams)}");
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new { error = "refresh_token_required", error_description = "Refresh token is required." });
        }

        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = EffectiveClientId,
            ["refresh_token"] = request.RefreshToken,
        };

        if (!string.IsNullOrEmpty(_keycloakOptions.ClientSecret))
        {
            tokenRequest["client_secret"] = _keycloakOptions.ClientSecret;
        }

        var httpClient = _httpClientFactory.CreateClient();
        var tokenResponse = await httpClient.PostAsync(ResolveTokenEndpoint(), new FormUrlEncodedContent(tokenRequest));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorContent = await tokenResponse.Content.ReadAsStringAsync();
            _logger.LogWarning("Token refresh failed (status {Status}): {Body}", (int)tokenResponse.StatusCode, errorContent);
            return BadRequest(new { error = "refresh_failed", error_description = errorContent });
        }

        var tokens = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            return BadRequest(new { error = "invalid_response", error_description = "Failed to parse token response." });
        }

        return Ok(new RefreshTokenResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            IdToken = tokens.IdToken,
            ExpiresIn = tokens.ExpiresIn,
            TokenType = tokens.TokenType ?? "Bearer",
        });
    }

    [HttpGet("logout")]
    public IActionResult Logout([FromQuery(Name = "id_token_hint")] string? idTokenHint = null)
    {
        var frontendBaseUrl = !string.IsNullOrEmpty(_keycloakOptions.FrontendUrl)
            ? _keycloakOptions.FrontendUrl.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host}";

        var postLogoutRedirectUri = $"{frontendBaseUrl}/login";

        var logoutUrl = $"{_keycloakOptions.Authority}/protocol/openid-connect/logout?" +
            $"client_id={Uri.EscapeDataString(EffectiveClientId)}" +
            $"&post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}";

        if (!string.IsNullOrEmpty(idTokenHint))
        {
            logoutUrl += $"&id_token_hint={Uri.EscapeDataString(idTokenHint)}";
        }

        return Redirect(logoutUrl);
    }

    private string ResolveTokenEndpoint()
    {
        var backchannelAuthority = _keycloakOptions.InternalAuthority ?? _keycloakOptions.Authority;
        return $"{backchannelAuthority}/protocol/openid-connect/token";
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    public sealed class RefreshTokenRequest
    {
        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }
    }

    public sealed class RefreshTokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("idToken")]
        public string? IdToken { get; set; }

        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("tokenType")]
        public string? TokenType { get; set; }
    }
}
