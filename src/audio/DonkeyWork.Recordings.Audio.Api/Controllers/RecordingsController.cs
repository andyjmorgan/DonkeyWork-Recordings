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

    public RecordingsController(ITtsService ttsService, IAudioGenerationService generationService)
    {
        _ttsService = ttsService;
        _generationService = generationService;
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

    [HttpPost("generate")]
    public async Task<ActionResult<TtsRecordingV1>> Generate(
        [FromBody] StartAudioGenerationRequestV1 request,
        CancellationToken cancellationToken)
    {
        var recordingId = await _generationService.StartGenerationAsync(request, cancellationToken);
        var recording = await _ttsService.GetRecordingAsync(recordingId, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id = recordingId }, recording);
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
