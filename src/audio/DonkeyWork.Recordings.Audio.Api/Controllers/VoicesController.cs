using System.ComponentModel.DataAnnotations;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/voices")]
public class VoicesController : ControllerBase
{
    private const string CacheKey = "magpie.voices.v1";
    private const string PreviewText = "Testing, one, two, three.";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ITtsProvider _ttsProvider;
    private readonly ISsmlPreprocessor _ssml;
    private readonly IGptOssPreprocessor _preprocessor;
    private readonly IMemoryCache _cache;
    private readonly IOptions<MagpieOptions> _magpieOptions;

    public VoicesController(
        ITtsProvider ttsProvider,
        ISsmlPreprocessor ssml,
        IGptOssPreprocessor preprocessor,
        IMemoryCache cache,
        IOptions<MagpieOptions> magpieOptions)
    {
        _ttsProvider = ttsProvider;
        _ssml = ssml;
        _preprocessor = preprocessor;
        _cache = cache;
        _magpieOptions = magpieOptions;
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<ActionResult<IReadOnlyList<VoiceResponseV1>>> List(CancellationToken cancellationToken)
    {
        var voices = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await _ttsProvider.ListVoicesAsync(cancellationToken);
        }) ?? Array.Empty<TtsVoice>();

        return Ok(voices
            .Select(v => new VoiceResponseV1
            {
                Id = v.Id,
                Language = v.Language,
                Name = v.Name,
                Emotion = v.Emotion,
            })
            .ToList());
    }

    // Synthesise a short "testing one two three" clip with the supplied voice
    // and (optional) tone so the user can audition a channel's defaults before
    // saving. We push the phrase through gpt-oss with the tone applied so the
    // tone choice is reflected in the spoken text, then take the first
    // returned paragraph (capped) to keep the clip <~5s.
    [HttpPost("preview")]
    [Produces("audio/wav")]
    public async Task<IActionResult> Preview([FromBody] PreviewRequestV1 request, CancellationToken cancellationToken)
    {
        string spoken;
        try
        {
            var paragraphs = await _preprocessor.PreprocessAsync(
                new GptOssPreprocessRequest(PreviewText, request.Tone, request.Language),
                cancellationToken);

            spoken = paragraphs
                .Select(p => p?.Trim() ?? string.Empty)
                .FirstOrDefault(p => !string.IsNullOrEmpty(p))
                ?? PreviewText;
        }
        catch
        {
            spoken = PreviewText;
        }

        if (spoken.Length > 280)
        {
            spoken = spoken[..280];
        }

        var wrapped = _ssml.Wrap(spoken);
        var clip = await _ttsProvider.SynthesizeAsync(
            wrapped,
            new TtsProviderRequest(request.Voice, request.Language, _magpieOptions.Value.SampleRateHz),
            cancellationToken);

        return File(clip.Audio, "audio/wav");
    }

    public sealed class VoiceResponseV1
    {
        public required string Id { get; init; }

        public required string Language { get; init; }

        public required string Name { get; init; }

        public string? Emotion { get; init; }
    }

    public sealed class PreviewRequestV1
    {
        [Required]
        public required string Voice { get; init; }

        [Required]
        public required string Language { get; init; }

        public string? Tone { get; init; }
    }
}
