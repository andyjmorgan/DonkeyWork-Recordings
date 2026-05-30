using DonkeyWork.Recordings.Api;
using DonkeyWork.Recordings.Audio.Api;
using DonkeyWork.Recordings.Identity.Api;
using DonkeyWork.Recordings.Mcp.Api;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Storage.Api;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

// Trust the reverse-proxy chain (nginx in the web pod → API). Without this
// Request.Scheme reports "http" inside the cluster and AuthController builds
// redirect_uri=http://... which Keycloak rejects against its https:// whitelist.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddIdentityApi(builder.Configuration);
builder.Services.AddStorageApi(builder.Configuration);
builder.Services.AddAudioApi(builder.Configuration);
builder.Services.AddMcpApi(typeof(DonkeyWork.Recordings.Audio.Api.McpTools.AudioTools).Assembly);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.UseMcpApi();

await MigrateDatabaseAsync(app);

app.Run();

static async Task MigrateDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed at startup");
    }
}

public partial class Program;
