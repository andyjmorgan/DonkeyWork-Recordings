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

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var map = await JsonSerializer.DeserializeAsync<Dictionary<string, List<string>>>(stream, cancellationToken: cancellationToken)
            ?? new Dictionary<string, List<string>>();

        var voices = new List<TtsVoice>();
        foreach (var (language, names) in map)
        {
            foreach (var name in names)
            {
                voices.Add(ParseVoice(language, name));
            }
        }

        return voices;
    }

    private static TtsVoice ParseVoice(string language, string fullName)
    {
        var parts = fullName.Split('.', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            >= 4 => new TtsVoice(fullName, language, parts[2], parts[3]),
            3 => new TtsVoice(fullName, language, parts[2], null),
            _ => new TtsVoice(fullName, language, fullName, null),
        };
    }
}
