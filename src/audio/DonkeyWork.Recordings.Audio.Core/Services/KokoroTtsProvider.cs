using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class KokoroTtsProvider : ITtsProvider
{
    public const string ProviderKey = "kokoro";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KokoroOptions _options;

    public KokoroTtsProvider(IHttpClientFactory httpClientFactory, IOptions<KokoroOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public string Key => ProviderKey;

    public string DisplayName => "Kokoro";

    public bool SupportsVoiceSelection => true;

    public string DefaultVoice => _options.DefaultVoice;

    public async Task<TtsClipResult> SynthesizeAsync(string text, TtsProviderRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new SpeechRequest
        {
            Input = text,
            Voice = string.IsNullOrWhiteSpace(request.Voice) ? _options.DefaultVoice : request.Voice,
            Speed = _options.Speed,
        };

        var client = _httpClientFactory.CreateClient(ProviderKey);
        using var response = await client.PostAsJsonAsync("v1/audio/speech", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var wavBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/wav";

        return new TtsClipResult(wavBytes, contentType, _options.SampleRateHz);
    }

    public async Task<IReadOnlyList<TtsVoice>> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(ProviderKey);
        var payload = await client.GetFromJsonAsync<VoicesPayload>("v1/audio/voices", cancellationToken);

        return (payload?.Voices ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(ParseVoice)
            .ToList();
    }

    // Kokoro voice ids are "{lang}{gender}_{name}", e.g. "af_heart" (American female Heart),
    // "bm_george" (British male George). Map the prefix to a language/display name for the picker.
    private static TtsVoice ParseVoice(string id)
    {
        var language = id.Length > 0 ? LanguageFor(id[0]) : "en-US";
        var underscore = id.IndexOf('_');
        var namePart = underscore >= 0 && underscore < id.Length - 1 ? id[(underscore + 1)..] : id;
        var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(namePart);

        return new TtsVoice(id, language, name, null);
    }

    private static string LanguageFor(char prefix) => char.ToLowerInvariant(prefix) switch
    {
        'a' => "en-US",
        'b' => "en-GB",
        'e' => "es",
        'f' => "fr",
        'h' => "hi",
        'i' => "it",
        'p' => "pt-BR",
        _ => "en-US",
    };

    private sealed record SpeechRequest
    {
        [JsonPropertyName("input")]
        public required string Input { get; init; }

        [JsonPropertyName("voice")]
        public required string Voice { get; init; }

        [JsonPropertyName("speed")]
        public double Speed { get; init; }
    }

    private sealed record VoicesPayload
    {
        [JsonPropertyName("voices")]
        public IReadOnlyList<string>? Voices { get; init; }
    }
}
