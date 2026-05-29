using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Protocol;

namespace DonkeyWork.Recordings.Mcp.Api;

public static class DependencyInjection
{
    private const string IconBaseUrl = "https://s3.donkeywork.dev/mcp-icons";

    private static readonly int[] IconSizes = [16, 32, 64, 128, 256, 512];

    public static IServiceCollection AddMcpApi(this IServiceCollection services, params Assembly[] toolAssemblies)
    {
        if (toolAssemblies.Length == 0)
        {
            return services;
        }

        var builder = services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "DonkeyWork Recordings",
                    Title = "DonkeyWork Recordings",
                    Version = "1.0.0",
                    Description = "Text-to-speech and audio recording tools for DonkeyWork.",
                    WebsiteUrl = "https://recordings.donkeywork.dev",
                    Icons = IconSizes
                        .Select(size => new Icon
                        {
                            Source = $"{IconBaseUrl}/donkeywork-{size}.png",
                            MimeType = "image/png",
                            Sizes = [$"{size}x{size}"],
                        })
                        .ToList(),
                };
            })
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            });

        foreach (var assembly in toolAssemblies)
        {
            builder.WithToolsFromAssembly(assembly);
        }

        return services;
    }

    public static WebApplication UseMcpApi(this WebApplication app)
    {
        app.MapMcp("/mcp").RequireAuthorization(new AuthorizeAttribute
        {
            AuthenticationSchemes = McpAuthenticationDefaults.AuthenticationScheme,
        });

        return app;
    }
}
