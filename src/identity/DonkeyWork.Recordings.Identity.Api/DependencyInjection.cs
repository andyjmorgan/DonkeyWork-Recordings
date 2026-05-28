using System.Security.Claims;
using DonkeyWork.Recordings.Identity.Api.Options;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Identity.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

namespace DonkeyWork.Recordings.Identity.Api;

public static class DependencyInjection
{
    public const string MultiAuthScheme = "MultiAuth";

    public static IServiceCollection AddIdentityApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<KeycloakOptions>()
            .Bind(configuration.GetSection(KeycloakOptions.SectionName))
            .ValidateDataAnnotations();

        var keycloakOptions = configuration
            .GetSection(KeycloakOptions.SectionName)
            .Get<KeycloakOptions>() ?? new KeycloakOptions();

        services.AddScoped<IdentityContext>();
        services.AddScoped<IIdentityContext>(sp => sp.GetRequiredService<IdentityContext>());

        services.AddHttpClient();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = MultiAuthScheme;
                options.DefaultChallengeScheme = MultiAuthScheme;
            })
            .AddPolicyScheme(MultiAuthScheme, "JWT", options =>
            {
                options.ForwardDefaultSelector = _ => JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var metadataAuthority = keycloakOptions.InternalAuthority ?? keycloakOptions.Authority;
                options.Authority = metadataAuthority;
                options.Audience = keycloakOptions.Audience;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakOptions.Authority,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = ClaimTypes.NameIdentifier,
                    AudienceValidator = (audiences, securityToken, _) =>
                    {
                        var validAudience = keycloakOptions.Audience;

                        if (audiences.Contains(validAudience))
                        {
                            return true;
                        }

                        if (securityToken is JsonWebToken jwt
                            && jwt.TryGetPayloadValue<string>("azp", out var azp)
                            && azp == validAudience)
                        {
                            return true;
                        }

                        return false;
                    },
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        var identityContext = context.HttpContext.RequestServices
                            .GetRequiredService<IdentityContext>();

                        var principal = context.Principal;
                        if (principal?.Identity?.IsAuthenticated != true)
                        {
                            return Task.CompletedTask;
                        }

                        var subClaim = principal.FindFirst("sub")?.Value
                            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var userId))
                        {
                            logger.LogError("JWT 'sub' claim missing or not a GUID: {SubClaim}", subClaim);
                            context.Fail("Invalid or missing 'sub' claim");
                            return Task.CompletedTask;
                        }

                        var email = principal.FindFirst("email")?.Value
                            ?? principal.FindFirst(ClaimTypes.Email)?.Value;
                        var name = principal.FindFirst("name")?.Value;
                        var username = principal.FindFirst("preferred_username")?.Value;

                        identityContext.SetIdentity(userId, email, name, username);

                        return Task.CompletedTask;
                    },
                };
            })
            .AddMcp(options =>
            {
                options.ForwardAuthenticate = MultiAuthScheme;
                options.ResourceMetadataUri = new Uri("/.well-known/oauth-protected-resource", UriKind.Relative);
                options.ResourceMetadata = new ProtectedResourceMetadata
                {
                    AuthorizationServers = { keycloakOptions.Authority },
                    ScopesSupported = { "openid", "profile", "email", "offline_access" },
                };
                options.Events.OnResourceMetadataRequest = context =>
                {
                    context.ResourceMetadata ??= new ProtectedResourceMetadata
                    {
                        AuthorizationServers = { keycloakOptions.Authority },
                        ScopesSupported = { "openid", "profile", "email", "offline_access" },
                    };
                    context.ResourceMetadata.Resource ??= $"{context.Request.Scheme}://{context.Request.Host}";
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();

        services.AddControllers().AddApplicationPart(typeof(DependencyInjection).Assembly);

        return services;
    }
}
