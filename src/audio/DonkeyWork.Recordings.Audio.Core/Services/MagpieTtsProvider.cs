using System.Text.Json;
using System.Text.Json.Serialization;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class MagpieTtsProvider : ITtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly MagpieOptions _options;

    public MagpieTtsProvider(HttpClient httpClient, IOptions<MagpieOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<TtsClipResult> SynthesizeAsync(string text, TtsProviderRequest request, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(text), "text" },
            { new StringContent(request.Language), "language" },
            { new StringContent(request.Voice), "voice" },
            { new StringContent("LINEAR_PCM"), "encoding" },
            { new StringContent(request.SampleRateHz.ToString()), "sample_rate_hz" },
        };

        using var response = await _httpClient.PostAsync("v1/audio/synthesize", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        var wavBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/wav";

        return new TtsClipResult(wavBytes, contentType, request.SampleRateHz);
    }

    public async Task<IReadOnlyList<TtsVoice>> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("v1/audio/list_voices", cancellationToken);
        response.EnsureSuccessStatusCode();

        // Magpie shape: { "<csv-of-locales>": { "voices": [ "Magpie-Multilingual.EN-US.Mia", ... ] } }
        // The top-level key (the csv) doesn't map to individual voices — each voice
        // carries its own locale in its name (parts[1]), so we ignore the key.
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var map = await JsonSerializer.DeserializeAsync<Dictionary<string, VoicesPayload>>(stream, cancellationToken: cancellationToken)
            ?? new Dictionary<string, VoicesPayload>();

        return map.Values
            .SelectMany(p => p.Voices ?? Enumerable.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(ParseVoice)
            .ToList();
    }

    private static TtsVoice ParseVoice(string fullName)
    {
        var parts = fullName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var language = parts.Length >= 2 ? NormalizeLanguage(parts[1]) : "en-US";
        var name = parts.Length >= 3 ? parts[2] : fullName;
        var emotion = parts.Length >= 4 ? parts[3] : null;
        return new TtsVoice(fullName, language, name, emotion);
    }

    // Magpie reports locales as "EN-US" inside voice names but the synthesize
    // endpoint expects "en-US". Normalize to the lang-REGION convention.
    private static string NormalizeLanguage(string raw)
    {
        var hyphen = raw.IndexOf('-');
        if (hyphen <= 0)
        {
            return raw.ToLowerInvariant();
        }

        return $"{raw[..hyphen].ToLowerInvariant()}-{raw[(hyphen + 1)..].ToUpperInvariant()}";
    }

    private sealed record VoicesPayload(
        [property: JsonPropertyName("voices")] List<string>? Voices);
}
