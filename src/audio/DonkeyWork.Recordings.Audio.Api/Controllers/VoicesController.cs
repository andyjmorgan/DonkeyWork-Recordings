using DonkeyWork.Recordings.Audio.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DonkeyWork.Recordings.Audio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/voices")]
public class VoicesController : ControllerBase
{
    private const string CacheKey = "tts.voices.v1";
    private const string PreviewText = "Testing, one, two, three.";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ITtsProvider _ttsProvider;
    private readonly ISsmlPreprocessor _ssml;
    private readonly IMemoryCache _cache;

    public VoicesController(
        ITtsProvider ttsProvider,
        ISsmlPreprocessor ssml,
        IMemoryCache cache)
    {
        _ttsProvider = ttsProvider;
        _ssml = ssml;
        _cache = cache;
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<ActionResult<VoicesResponseV1>> List(CancellationToken cancellationToken)
    {
        var response = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;

            IReadOnlyList<TtsVoice> voices;
            try
            {
                voices = await _ttsProvider.ListVoicesAsync(cancellationToken);
            }
            catch
            {
                // The backend being down shouldn't blank the whole picker — surface it with no voices.
                voices = [];
            }

            return new VoicesResponseV1
            {
                DefaultVoice = _ttsProvider.DefaultVoice,
                Voices = voices
                    .Select(v => new VoiceResponseV1
                    {
                        Id = v.Id,
                        Language = v.Language,
                        Name = v.Name,
                        Emotion = v.Emotion,
                        Rating = v.Rating,
                        SampleUrl = v.SampleUrl,
                    })
                    .ToList(),
            };
        }) ?? new VoicesResponseV1 { DefaultVoice = _ttsProvider.DefaultVoice, Voices = [] };

        return Ok(response);
    }

    // Synthesise a short "testing one two three" clip with the supplied voice so the user can
    // audition a channel's default before saving. The voice defaults to the provider default.
    [HttpPost("preview")]
    [Produces("audio/wav")]
    public async Task<IActionResult> Preview([FromBody] PreviewRequestV1 request, CancellationToken cancellationToken)
    {
        var voice = string.IsNullOrWhiteSpace(request.Voice) ? _ttsProvider.DefaultVoice : request.Voice;
        var language = string.IsNullOrWhiteSpace(request.Language) ? "en-US" : request.Language;

        var wrapped = _ssml.Wrap(PreviewText);
        var clip = await _ttsProvider.SynthesizeAsync(
            wrapped,
            new TtsProviderRequest(voice, language),
            cancellationToken);

        return File(clip.Audio, "audio/wav");
    }

    public sealed class VoicesResponseV1
    {
        public required string DefaultVoice { get; init; }

        public required IReadOnlyList<VoiceResponseV1> Voices { get; init; }
    }

    public sealed class VoiceResponseV1
    {
        public required string Id { get; init; }

        public required string Language { get; init; }

        public required string Name { get; init; }

        public string? Emotion { get; init; }

        public string? Rating { get; init; }

        public string? SampleUrl { get; init; }
    }

    public sealed class PreviewRequestV1
    {
        public string? Voice { get; init; }

        public string? Language { get; init; }
    }
}
