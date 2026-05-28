using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Authentication;

namespace DonkeyWork.Recordings.Mcp.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddMcpApi(this IServiceCollection services, params Assembly[] toolAssemblies)
    {
        if (toolAssemblies.Length == 0)
        {
            return services;
        }

        var builder = services
            .AddMcpServer()
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
        app.MapMcp().RequireAuthorization(new AuthorizeAttribute
        {
            AuthenticationSchemes = McpAuthenticationDefaults.AuthenticationScheme,
        });

        return app;
    }
}
