using DonkeyWork.Recordings.Audio.Api.Models.OpenAi;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Helpers;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Api.Controllers;

// OpenAI-compatible surface: an unmodified OpenAI TTS client pointed at `{base}/openai/v1` with
// `Authorization: Bearer <DonkeyWork api key>` works against this service. The response shapes,
// status codes, and error envelopes mirror api.openai.com (captured live); the one model exposed
// is `kokoro`.
[ApiController]
[Authorize]
[Route("openai/v1")]
public class OpenAiCompatController : ControllerBase
{
    private const string VoiceIdsCacheKey = "openai.compat.voice-ids.v1";
    private static readonly TimeSpan VoiceIdsCacheTtl = TimeSpan.FromMinutes(5);

    private readonly ITtsProvider _ttsProvider;
    private readonly ISsmlPreprocessor _ssml;
    private readonly ITtsChunker _chunker;
    private readonly IMemoryCache _cache;
    private readonly TtsOptions _ttsOptions;
    private readonly ILogger<OpenAiCompatController> _logger;

    public OpenAiCompatController(
        ITtsProvider ttsProvider,
        ISsmlPreprocessor ssml,
        ITtsChunker chunker,
        IMemoryCache cache,
        IOptions<TtsOptions> ttsOptions,
        ILogger<OpenAiCompatController> logger)
    {
        _ttsProvider = ttsProvider;
        _ssml = ssml;
        _chunker = chunker;
        _cache = cache;
        _ttsOptions = ttsOptions.Value;
        _logger = logger;
    }

    private static OpenAiModelObject KokoroModel => new()
    {
        Id = OpenAiCompatibility.ModelId,
        Created = OpenAiCompatibility.ModelCreatedUnixSeconds,
        OwnedBy = OpenAiCompatibility.ModelOwner,
    };

    [HttpGet("models")]
    [Produces("application/json")]
    public ActionResult<OpenAiModelList> ListModels()
        => Ok(new OpenAiModelList { Data = [KokoroModel] });

    [HttpGet("models/{id}")]
    [Produces("application/json")]
    public ActionResult<OpenAiModelObject> GetModel(string id)
        => string.Equals(id, OpenAiCompatibility.ModelId, StringComparison.Ordinal)
            ? Ok(KokoroModel)
            : NotFound(OpenAiErrorEnvelope.ModelNotFound(id));

    [HttpPost("audio/speech")]
    public async Task<IActionResult> CreateSpeech([FromBody] OpenAiSpeechRequest request, CancellationToken cancellationToken)
    {
        // Validation mirrors OpenAI's behaviour: body-level (enum/range/length) failures are 400
        // invalid_request_error; an unknown model is 404 model_not_found.
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return BadRequestEnvelope("you must provide a model parameter");
        }

        var responseFormat = string.IsNullOrWhiteSpace(request.ResponseFormat) ? "mp3" : request.ResponseFormat;
        if (!OpenAiCompatibility.TryGetContentType(responseFormat, out var contentType))
        {
            return BadRequestEnvelope(
                $"[{{'type': 'enum', 'loc': ('body', 'response_format'), 'msg': \"{OpenAiCompatibility.ResponseFormatEnumMessage}\"}}]");
        }

        if (string.Equals(request.StreamFormat, "sse", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequestEnvelope("Streaming (stream_format 'sse') is not supported by this endpoint. Omit stream_format for a plain audio response.");
        }

        if (request.Speed is { } speed && (speed < OpenAiCompatibility.MinSpeed || speed > OpenAiCompatibility.MaxSpeed))
        {
            return BadRequestEnvelope(
                $"[{{'type': 'range', 'loc': ('body', 'speed'), 'msg': 'Input should be between {OpenAiCompatibility.MinSpeed} and {OpenAiCompatibility.MaxSpeed}'}}]");
        }

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequestEnvelope("[{'type': 'missing', 'loc': ('body', 'input'), 'msg': 'Field required'}]");
        }

        if (request.Input.Length > OpenAiCompatibility.MaxInputChars)
        {
            return BadRequestEnvelope(
                $"[{{'type': 'string_too_long', 'loc': ('body', 'input'), 'msg': 'String should have at most {OpenAiCompatibility.MaxInputChars} characters'}}]");
        }

        if (!string.Equals(request.Model, OpenAiCompatibility.ModelId, StringComparison.Ordinal))
        {
            return NotFound(OpenAiErrorEnvelope.ModelNotFound(request.Model));
        }

        var (voice, voiceError) = await ResolveVoiceAsync(request.Voice, cancellationToken);
        if (voiceError is not null)
        {
            return voiceError;
        }

        byte[] payload;
        try
        {
            // Same pipeline as recording generation: strip stray inline tokens, split to the
            // backend's per-request limits, synthesize each chunk, and stitch the WAVs back
            // together before converting to the requested container.
            var wrapped = _ssml.Wrap(request.Input);
            var chunks = _chunker.Chunk(wrapped, new ChunkerOptions())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (chunks.Count == 0)
            {
                return BadRequestEnvelope("[{'type': 'missing', 'loc': ('body', 'input'), 'msg': 'Field required'}]");
            }

            var clips = new List<byte[]>(chunks.Count);
            foreach (var chunk in chunks)
            {
                var clip = await _ttsProvider.SynthesizeAsync(
                    chunk,
                    new TtsProviderRequest(voice, _ttsOptions.DefaultLanguage, request.Speed),
                    cancellationToken);
                clips.Add(clip.Audio);
            }

            var wav = AudioConverter.ConcatWav(clips);
            payload = OpenAiCompatibility.ConvertWav(wav, responseFormat);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI-compat speech synthesis failed for voice {Voice}", voice);
            return StatusCode(StatusCodes.Status500InternalServerError, OpenAiErrorEnvelope.ServerError());
        }

        return File(payload, contentType);
    }

    // Resolve the request voice: empty → provider default; an OpenAI voice name → its Kokoro
    // mapping; otherwise it must be a native Kokoro voice id (validated against the backend's
    // cached voice list — skipped when the list is unavailable so an outage doesn't reject valid
    // ids). Unknown voices produce OpenAI's enum-style 400.
    private async Task<(string Voice, IActionResult? Error)> ResolveVoiceAsync(string? requested, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return (_ttsProvider.DefaultVoice, null);
        }

        if (OpenAiCompatibility.TryResolveVoiceAlias(requested, out var mapped))
        {
            return (mapped, null);
        }

        var knownIds = await _cache.GetOrCreateAsync(VoiceIdsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = VoiceIdsCacheTtl;

            try
            {
                var voices = await _ttsProvider.ListVoicesAsync(cancellationToken);
                return voices
                    .Select(v => v.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // Backend down — don't reject possibly-valid native ids on a stale surface.
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }) ?? [];

        if (knownIds.Count == 0 || knownIds.Contains(requested))
        {
            return (requested, null);
        }

        var aliasList = string.Join(", ", OpenAiCompatibility.VoiceAliases.Keys.Order().Select(a => $"'{a}'"));
        return (string.Empty, BadRequestEnvelope(
            $"[{{'type': 'enum', 'loc': ('body', 'voice'), 'msg': \"Input should be one of {aliasList}, or a native Kokoro voice id such as 'af_heart'\"}}]"));
    }

    private ObjectResult BadRequestEnvelope(string message)
        => BadRequest(OpenAiErrorEnvelope.InvalidRequest(message));
}
