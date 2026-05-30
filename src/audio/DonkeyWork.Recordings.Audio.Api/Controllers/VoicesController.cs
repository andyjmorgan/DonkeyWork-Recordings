using System.ComponentModel.DataAnnotations;
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
    private const string CacheKey = "tts.models.v1";
    private const string PreviewText = "Testing, one, two, three.";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ITtsProviderRegistry _ttsRegistry;
    private readonly ISsmlPreprocessor _ssml;
    private readonly IGptOssPreprocessor _preprocessor;
    private readonly IMemoryCache _cache;

    public VoicesController(
        ITtsProviderRegistry ttsRegistry,
        ISsmlPreprocessor ssml,
        IGptOssPreprocessor preprocessor,
        IMemoryCache cache)
    {
        _ttsRegistry = ttsRegistry;
        _ssml = ssml;
        _preprocessor = preprocessor;
        _cache = cache;
    }

    [HttpGet("models")]
    [Produces("application/json")]
    public async Task<ActionResult<IReadOnlyList<TtsModelResponseV1>>> Models(CancellationToken cancellationToken)
    {
        var models = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;

            var result = new List<TtsModelResponseV1>();
            foreach (var provider in _ttsRegistry.Providers)
            {
                IReadOnlyList<TtsVoice> voices;
                try
                {
                    voices = await provider.ListVoicesAsync(cancellationToken);
                }
                catch
                {
                    // A backend being down shouldn't blank the whole menu — surface it with no voices.
                    voices = [];
                }

                result.Add(new TtsModelResponseV1
                {
                    Key = provider.Key,
                    DisplayName = provider.DisplayName,
                    SupportsVoiceSelection = provider.SupportsVoiceSelection,
                    DefaultVoice = provider.DefaultVoice,
                    IsDefault = provider.Key == _ttsRegistry.Default.Key,
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
                });
            }

            return (IReadOnlyList<TtsModelResponseV1>)result;
        }) ?? [];

        return Ok(models);
    }

    // Synthesise a short "testing one two three" clip with the supplied model/voice
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

        var provider = _ttsRegistry.Resolve(request.Model);
        var wrapped = _ssml.Wrap(spoken);
        var clip = await provider.SynthesizeAsync(
            wrapped,
            new TtsProviderRequest(request.Voice, request.Language),
            cancellationToken);

        return File(clip.Audio, "audio/wav");
    }

    public sealed class TtsModelResponseV1
    {
        public required string Key { get; init; }

        public required string DisplayName { get; init; }

        public required bool SupportsVoiceSelection { get; init; }

        public required string DefaultVoice { get; init; }

        public required bool IsDefault { get; init; }

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
        public string? Model { get; init; }

        [Required]
        public required string Voice { get; init; }

        [Required]
        public required string Language { get; init; }

        public string? Tone { get; init; }
    }
}
