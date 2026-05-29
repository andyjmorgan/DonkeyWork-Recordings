using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class ChatterboxTtsProvider : ITtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly ChatterboxOptions _options;

    public ChatterboxTtsProvider(HttpClient httpClient, IOptions<ChatterboxOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<TtsClipResult> SynthesizeAsync(string text, TtsProviderRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new SpeechRequest
        {
            Input = text,
            Exaggeration = _options.Exaggeration,
            CfgWeight = _options.CfgWeight,
        };

        using var response = await _httpClient.PostAsJsonAsync("v1/audio/speech", payload, cancellationToken);
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
            new TtsVoice("default", _options.DefaultLanguage, "Default", null),
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
