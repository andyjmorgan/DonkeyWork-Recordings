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
    // "bm_george" (British male George). Map the prefix to a language/display name for the picker,
    // attach Kokoro's own quality grade, and point at a pre-rendered sample so the UI can audition
    // a voice without hitting the synth endpoint.
    private TtsVoice ParseVoice(string id)
    {
        var language = id.Length > 0 ? LanguageFor(id[0]) : "en-US";
        var underscore = id.IndexOf('_');
        var namePart = underscore >= 0 && underscore < id.Length - 1 ? id[(underscore + 1)..] : id;
        var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(namePart);
        var sampleUrl = $"{_options.SamplesBaseUrl.TrimEnd('/')}/{id}.wav";

        return new TtsVoice(id, language, name, null, Ratings.GetValueOrDefault(id), sampleUrl);
    }

    // Kokoro's published quality grades. Voices without an entry (e.g. Spanish/Portuguese) are ungraded.
    private static readonly IReadOnlyDictionary<string, string> Ratings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["af_heart"] = "A",
        ["af_bella"] = "A-",
        ["af_nicole"] = "B-",
        ["bf_emma"] = "B-",
        ["ff_siwis"] = "B-",
        ["af_aoede"] = "C+",
        ["af_kore"] = "C+",
        ["af_sarah"] = "C+",
        ["am_michael"] = "C+",
        ["am_fenrir"] = "C+",
        ["am_puck"] = "C+",
        ["af_alloy"] = "C",
        ["af_nova"] = "C",
        ["bf_isabella"] = "C",
        ["bm_fable"] = "C",
        ["bm_george"] = "C",
        ["hf_alpha"] = "C",
        ["hf_beta"] = "C",
        ["hm_omega"] = "C",
        ["hm_psi"] = "C",
        ["if_sara"] = "C",
        ["im_nicola"] = "C",
        ["af_sky"] = "C-",
        ["bm_lewis"] = "D+",
        ["af_jessica"] = "D",
        ["af_river"] = "D",
        ["am_echo"] = "D",
        ["am_eric"] = "D",
        ["am_liam"] = "D",
        ["am_onyx"] = "D",
        ["bf_alice"] = "D",
        ["bf_lily"] = "D",
        ["bm_daniel"] = "D",
        ["am_santa"] = "D-",
        ["am_adam"] = "F+",
    };

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
