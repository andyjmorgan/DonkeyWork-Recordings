using DonkeyWork.Recordings.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PersistenceOptions>(
            configuration.GetSection(PersistenceOptions.SectionName));

        services.AddSingleton<AuditableInterceptor>();

        services.AddDbContext<RecordingsDbContext>((serviceProvider, dbContextOptions) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            var auditableInterceptor = serviceProvider.GetRequiredService<AuditableInterceptor>();

            dbContextOptions
                .UseNpgsql(options.ConnectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                })
                .AddInterceptors(auditableInterceptor);
        });

        return services;
    }
}
