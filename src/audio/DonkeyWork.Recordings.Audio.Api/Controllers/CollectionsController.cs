using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DonkeyWork.Recordings.Audio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/collections")]
[Produces("application/json")]
public class CollectionsController : ControllerBase
{
    private readonly IAudioCollectionService _service;

    public CollectionsController(IAudioCollectionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ListAudioCollectionsResponseV1>> List(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _service.ListAsync(offset, limit, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AudioCollectionV1>> Get(Guid id, CancellationToken cancellationToken)
    {
        var collection = await _service.GetAsync(id, cancellationToken);
        return collection is null ? NotFound() : collection;
    }

    [HttpPost]
    public async Task<ActionResult<AudioCollectionV1>> Create(
        [FromBody] CreateAudioCollectionRequestV1 request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AudioCollectionV1>> Update(
        Guid id,
        [FromBody] UpdateAudioCollectionRequestV1 request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/recordings")]
    public async Task<ActionResult<ListRecordingsResponseV1>> ListRecordings(
        Guid id,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var response = await _service.ListRecordingsAsync(id, offset, limit, cancellationToken);
        return response is null ? NotFound() : response;
    }
}
