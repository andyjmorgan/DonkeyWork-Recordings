using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DonkeyWork.Recordings.Audio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/recordings")]
[Produces("application/json")]
public class RecordingsController : ControllerBase
{
    private readonly ITtsService _ttsService;
    private readonly IAudioGenerationService _generationService;
    private readonly IRecordingEventStream _eventStream;

    public RecordingsController(
        ITtsService ttsService,
        IAudioGenerationService generationService,
        IRecordingEventStream eventStream)
    {
        _ttsService = ttsService;
        _generationService = generationService;
        _eventStream = eventStream;
    }

    [HttpGet]
    public async Task<ActionResult<ListRecordingsResponseV1>> List(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] bool unfiledOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await _ttsService.ListRecordingsAsync(offset, limit, unfiledOnly, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TtsRecordingV1>> Get(Guid id, CancellationToken cancellationToken)
    {
        var recording = await _ttsService.GetRecordingAsync(id, cancellationToken);
        return recording is null ? NotFound() : recording;
    }

    // Server-Sent Events feed of a recording's generation lifecycle: chunk-ready / progress /
    // ready / failed. Replays current state on connect, then streams live events until the
    // recording settles or the client disconnects. Same auth as the rest of the surface
    // (X-Api-Key or bearer token). Clients without SSE can poll GET /api/v1/recordings/{id},
    // which carries chunks[] + playableUpTo.
    [HttpGet("{id:guid}/events")]
    [Produces("text/event-stream")]
    public async Task<IActionResult> Events(Guid id, CancellationToken cancellationToken)
    {
        var recording = await _ttsService.GetRecordingAsync(id, cancellationToken);
        if (recording is null)
        {
            return NotFound();
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        // The web pod's nginx sits in front of the API; without this it buffers the stream.
        Response.Headers["X-Accel-Buffering"] = "no";

        await _eventStream.StreamAsync(id, Response.Body, cancellationToken);
        return new EmptyResult();
    }

    [HttpPost("generate")]
    public async Task<ActionResult<TtsRecordingV1>> Generate(
        [FromBody] StartAudioGenerationRequestV1 request,
        CancellationToken cancellationToken)
    {
        var recordingId = await _generationService.StartGenerationAsync(request, cancellationToken);
        var recording = await _ttsService.GetRecordingAsync(recordingId, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id = recordingId }, recording);
    }

    [HttpPost("{id:guid}/regenerate")]
    public async Task<ActionResult<TtsRecordingV1>> Regenerate(
        Guid id,
        [FromBody] RegenerateRecordingRequestV1 request,
        CancellationToken cancellationToken)
    {
        var started = await _generationService.RegenerateAsync(id, request, cancellationToken);
        if (!started)
        {
            return NotFound();
        }

        var recording = await _ttsService.GetRecordingAsync(id, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id }, recording);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TtsRecordingV1>> Update(
        Guid id,
        [FromBody] UpdateRecordingRequestV1 request,
        CancellationToken cancellationToken)
    {
        var recording = await _ttsService.UpdateRecordingAsync(id, request, cancellationToken);
        return recording is null ? NotFound() : Ok(recording);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _ttsService.DeleteRecordingAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/collection")]
    public async Task<ActionResult<TtsRecordingV1>> Move(
        Guid id,
        [FromBody] MoveRecordingToCollectionRequestV1 request,
        CancellationToken cancellationToken)
    {
        var recording = await _ttsService.MoveRecordingAsync(id, request, cancellationToken);
        return recording is null ? NotFound() : Ok(recording);
    }

    [HttpGet("{id:guid}/playback")]
    public async Task<ActionResult<TtsPlaybackV1>> GetPlayback(Guid id, CancellationToken cancellationToken)
    {
        var playback = await _ttsService.GetPlaybackAsync(id, cancellationToken);
        return playback;
    }

    [HttpPut("{id:guid}/playback")]
    public async Task<ActionResult<TtsPlaybackV1>> UpdatePlayback(
        Guid id,
        [FromBody] UpdatePlaybackRequestV1 request,
        CancellationToken cancellationToken)
    {
        var playback = await _ttsService.UpdatePlaybackAsync(id, request, cancellationToken);
        return playback is null ? NotFound() : Ok(playback);
    }
}
