using DonkeyWork.Recordings.Audio.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DonkeyWork.Recordings.Audio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/voices")]
[Produces("application/json")]
public class VoicesController : ControllerBase
{
    private const string CacheKey = "magpie.voices.v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ITtsProvider _ttsProvider;
    private readonly IMemoryCache _cache;

    public VoicesController(ITtsProvider ttsProvider, IMemoryCache cache)
    {
        _ttsProvider = ttsProvider;
        _cache = cache;
    }

    [HttpGet]
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

    public sealed class VoiceResponseV1
    {
        public required string Id { get; init; }

        public required string Language { get; init; }

        public required string Name { get; init; }

        public string? Emotion { get; init; }
    }
}
