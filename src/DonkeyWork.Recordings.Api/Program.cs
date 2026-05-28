using DonkeyWork.Recordings.Audio.Api;
using DonkeyWork.Recordings.Identity.Api;
using DonkeyWork.Recordings.Mcp.Api;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Storage.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddIdentityApi(builder.Configuration);
builder.Services.AddStorageApi(builder.Configuration);
builder.Services.AddAudioApi(builder.Configuration);
builder.Services.AddMcpApi(typeof(DonkeyWork.Recordings.Audio.Api.McpTools.AudioTools).Assembly);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.UseMcpApi();

app.Run();

public partial class Program;
