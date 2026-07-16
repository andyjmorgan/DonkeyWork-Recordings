using System.Buffers.Binary;
using System.Text;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Identity.Contracts.Models;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

// Same shape as RecordingsTestFixture, but with the TTS backend and the api-key store faked so the
// OpenAI-compatible surface can be exercised end-to-end (auth → controller → chunker → provider →
// ffmpeg) without a live Kokoro or a provisioned key.
public sealed class OpenAiCompatTestFixture : IAsyncLifetime
{
    public const string ValidApiKey = "dk_openai_compat_test_key";

    private PostgreSqlContainer _postgres = null!;
    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialised");

    public FakeTtsProvider TtsProvider { get; } = new();

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
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ITtsProvider>();
                    services.AddSingleton<ITtsProvider>(TtsProvider);

                    services.RemoveAll<IUserApiKeyService>();
                    services.AddSingleton<IUserApiKeyService, FakeUserApiKeyService>();
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

    // Accepts exactly one REST-scoped key so the Bearer-as-api-key rewrite can be asserted.
    private sealed class FakeUserApiKeyService : IUserApiKeyService
    {
        private static readonly Guid UserId = Guid.Parse("6b2f4a41-9df7-4f60-9e2b-3f6a0a3e7d10");

        public Task<IReadOnlyList<UserApiKey>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserApiKey>>([]);

        public Task<UserApiKey> CreateAsync(string name, string? description, ApiKeyScope scope, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<ApiKeyValidationResult?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(apiKey == ValidApiKey
                ? new ApiKeyValidationResult(UserId, ApiKeyScope.RestOnly)
                : null);
    }
}

// Returns a tiny generated PCM WAV per synthesis call and records every request so tests can
// assert voice resolution and speed forwarding.
public sealed class FakeTtsProvider : ITtsProvider
{
    private readonly List<TtsProviderRequest> _requests = [];

    public string Key => "kokoro";

    public string DefaultVoice => "af_heart";

    public IReadOnlyList<TtsProviderRequest> Requests
    {
        get { lock (_requests) { return _requests.ToList(); } }
    }

    public void ClearRequests()
    {
        lock (_requests) { _requests.Clear(); }
    }

    public Task<TtsClipResult> SynthesizeAsync(string text, TtsProviderRequest request, CancellationToken cancellationToken = default)
    {
        lock (_requests) { _requests.Add(request); }
        return Task.FromResult(new TtsClipResult(GenerateWav(), "audio/wav", 24000));
    }

    public Task<IReadOnlyList<TtsVoice>> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TtsVoice> voices =
        [
            new("af_heart", "en-US", "Heart", null),
            new("af_alloy", "en-US", "Alloy", null),
            new("af_bella", "en-US", "Bella", null),
            new("af_nova", "en-US", "Nova", null),
            new("af_sarah", "en-US", "Sarah", null),
            new("am_echo", "en-US", "Echo", null),
            new("am_michael", "en-US", "Michael", null),
            new("am_onyx", "en-US", "Onyx", null),
            new("bm_fable", "en-GB", "Fable", null),
        ];

        return Task.FromResult(voices);
    }

    // 0.15s of a 440Hz sine at 24kHz mono s16le — small but real enough for ffmpeg to transcode.
    private static byte[] GenerateWav()
    {
        const int sampleRate = 24000;
        const int sampleCount = sampleRate * 15 / 100;
        const short bitsPerSample = 16;
        var dataSize = sampleCount * (bitsPerSample / 8);

        var buffer = new byte[44 + dataSize];
        var span = buffer.AsSpan();

        Encoding.ASCII.GetBytes("RIFF", span[..4]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..8], (uint)(36 + dataSize));
        Encoding.ASCII.GetBytes("WAVE", span[8..12]);
        Encoding.ASCII.GetBytes("fmt ", span[12..16]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..20], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..22], 1); // PCM
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..24], 1); // mono
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..28], sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..32], sampleRate * (uint)(bitsPerSample / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..34], bitsPerSample / 8);
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..36], (ushort)bitsPerSample);
        Encoding.ASCII.GetBytes("data", span[36..40]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..44], (uint)dataSize);

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 8000);
            BinaryPrimitives.WriteInt16LittleEndian(span.Slice(44 + i * 2, 2), sample);
        }

        return buffer;
    }
}
