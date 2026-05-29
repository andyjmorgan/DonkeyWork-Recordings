using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DonkeyWork.Recordings.Identity.Contracts.Models;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DonkeyWork.Recordings.Identity.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/apikeys")]
[Produces("application/json")]
public class ApiKeysController : ControllerBase
{
    private readonly IUserApiKeyService _service;

    public ApiKeysController(IUserApiKeyService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApiKeyItemV1>>> List(CancellationToken cancellationToken)
    {
        var items = await _service.ListAsync(cancellationToken);
        return Ok(items
            .Select(k => new ApiKeyItemV1
            {
                Id = k.Id,
                Name = k.Name,
                Description = k.Description,
                MaskedKey = k.Key,
                CreatedAt = k.CreatedAt,
                LastUsedAt = k.LastUsedAt,
                Scope = k.Scope,
            })
            .ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CreateApiKeyResponseV1>> Create([FromBody] CreateApiKeyRequestV1 request, CancellationToken cancellationToken)
    {
        var key = await _service.CreateAsync(
            request.Name.Trim(),
            request.Description?.Trim(),
            request.Scope ?? ApiKeyScope.RestAndMcp,
            cancellationToken);

        return Ok(new CreateApiKeyResponseV1
        {
            Id = key.Id,
            Name = key.Name,
            Description = key.Description,
            Key = key.Key,
            CreatedAt = key.CreatedAt,
            Scope = key.Scope,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    public sealed class ApiKeyItemV1
    {
        public required Guid Id { get; init; }

        public required string Name { get; init; }

        public string? Description { get; init; }

        public required string MaskedKey { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? LastUsedAt { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public required ApiKeyScope Scope { get; init; }
    }

    public sealed class CreateApiKeyRequestV1
    {
        [Required]
        [MinLength(1)]
        [MaxLength(255)]
        public required string Name { get; init; }

        [MaxLength(1000)]
        public string? Description { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ApiKeyScope? Scope { get; init; }
    }

    public sealed class CreateApiKeyResponseV1
    {
        public required Guid Id { get; init; }

        public required string Name { get; init; }

        public string? Description { get; init; }

        // Full secret. Only returned once at creation — subsequent reads
        // expose the masked form.
        public required string Key { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public required ApiKeyScope Scope { get; init; }
    }
}
