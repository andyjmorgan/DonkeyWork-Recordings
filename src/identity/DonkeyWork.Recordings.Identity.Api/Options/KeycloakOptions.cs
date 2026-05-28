using System.ComponentModel.DataAnnotations;

namespace DonkeyWork.Recordings.Identity.Api.Options;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    [Required]
    [Url]
    public string Authority { get; set; } = string.Empty;

    [Url]
    public string? InternalAuthority { get; set; }

    [Required]
    public string Audience { get; set; } = string.Empty;

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public bool RequireHttpsMetadata { get; set; } = true;

    [Url]
    public string? FrontendUrl { get; set; }
}
