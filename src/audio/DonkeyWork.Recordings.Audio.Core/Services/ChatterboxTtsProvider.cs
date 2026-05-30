using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class ChatterboxTtsProvider : ITtsProvider
{
    public const string ProviderKey = "chatterbox";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ChatterboxOptions _options;

    public ChatterboxTtsProvider(IHttpClientFactory httpClientFactory, IOptions<ChatterboxOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public string Key => ProviderKey;

    public string DisplayName => "Chatterbox";

    public bool SupportsVoiceSelection => false;

    public string DefaultVoice => _options.DefaultVoice;

    public async Task<TtsClipResult> SynthesizeAsync(string text, TtsProviderRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new SpeechRequest
        {
            Input = text,
            Exaggeration = _options.Exaggeration,
            CfgWeight = _options.CfgWeight,
        };

        var client = _httpClientFactory.CreateClient(ProviderKey);
        using var response = await client.PostAsJsonAsync("v1/audio/speech", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var wavBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/wav";

        return new TtsClipResult(wavBytes, contentType, _options.SampleRateHz);
    }

    // Chatterbox ignores voice/model/response_format — a voice is only realised by cloning from a
    // reference_audio clip, and there is no list endpoint. Expose a single default so the channel
    // voice picker and tester still have something to select.
    public Task<IReadOnlyList<TtsVoice>> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TtsVoice> voices =
        [
            new TtsVoice(_options.DefaultVoice, _options.DefaultLanguage, "Default", null),
        ];

        return Task.FromResult(voices);
    }

    private sealed record SpeechRequest
    {
        [JsonPropertyName("input")]
        public required string Input { get; init; }

        [JsonPropertyName("exaggeration")]
        public double Exaggeration { get; init; }

        [JsonPropertyName("cfg_weight")]
        public double CfgWeight { get; init; }
    }
}
