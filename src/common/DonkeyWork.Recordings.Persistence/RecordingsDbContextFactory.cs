using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DonkeyWork.Recordings.Persistence;

public class RecordingsDbContextFactory : IDesignTimeDbContextFactory<RecordingsDbContext>
{
    public RecordingsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetSection(PersistenceOptions.SectionName)
            .Get<PersistenceOptions>()?.ConnectionString
            ?? "Host=localhost;Database=donkeywork_recordings;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<RecordingsDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
        });

        return new RecordingsDbContext(optionsBuilder.Options);
    }
}
