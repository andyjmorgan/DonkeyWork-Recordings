using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DonkeyWork.Recordings.Audio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
[Produces("application/json")]
public class BacklogController : ControllerBase
{
    private readonly IBacklogService _service;

    public BacklogController(IBacklogService service)
    {
        _service = service;
    }

    [HttpGet("collections/{collectionId:guid}/backlog")]
    public async Task<ActionResult<ListBacklogItemsResponseV1>> List(
        Guid collectionId,
        [FromQuery] string? status = "Pending",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _service.ListAsync(collectionId, status, offset, limit, cancellationToken);
            return response is null ? NotFound() : response;
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("collections/{collectionId:guid}/backlog")]
    public async Task<ActionResult<BacklogItemV1>> Create(
        Guid collectionId,
        [FromBody] CreateBacklogItemRequestV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(collectionId, request, cancellationToken);
            return created is null ? NotFound() : CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("collections/{collectionId:guid}/backlog/consume")]
    public async Task<ActionResult<ConsumeBacklogItemsResponseV1>> Consume(
        Guid collectionId,
        [FromBody] ConsumeBacklogItemsRequestV1 request,
        CancellationToken cancellationToken)
    {
        var response = await _service.ConsumeAsync(collectionId, request, cancellationToken);
        return response is null ? NotFound() : response;
    }

    [HttpGet("backlog/{id:guid}")]
    public async Task<ActionResult<BacklogItemV1>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : item;
    }

    [HttpPut("backlog/{id:guid}")]
    public async Task<ActionResult<BacklogItemV1>> Update(
        Guid id,
        [FromBody] UpdateBacklogItemRequestV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("backlog/{id:guid}/dismiss")]
    public async Task<ActionResult<BacklogItemV1>> Dismiss(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.DismissAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("backlog/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
