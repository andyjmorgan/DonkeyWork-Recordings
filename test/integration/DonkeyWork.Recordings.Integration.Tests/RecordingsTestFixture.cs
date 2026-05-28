using DonkeyWork.Recordings.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DonkeyWork.Recordings.Integration.Tests;

public sealed class RecordingsTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialised");

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("donkeywork_recordings_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Persistence:ConnectionString"] = _postgres.GetConnectionString(),
                        ["Persistence:EncryptionKey"] = "test-encryption-key-not-used-in-this-test",
                        ["Storage:ServiceUrl"] = "http://localhost:9999",
                        ["Storage:AccessKey"] = "dummy",
                        ["Storage:SecretKey"] = "dummy",
                        ["Storage:DefaultBucket"] = "recordings",
                        ["Storage:UsePathStyleAddressing"] = "true",
                        ["Storage:PublicServiceUrl"] = "http://localhost:9999",
                        ["Keycloak:Authority"] = "https://auth.test.local/realms/test",
                        ["Keycloak:Audience"] = "donkeywork-recordings-api-test",
                        ["Keycloak:RequireHttpsMetadata"] = "false",
                    });
                });
            });

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}
