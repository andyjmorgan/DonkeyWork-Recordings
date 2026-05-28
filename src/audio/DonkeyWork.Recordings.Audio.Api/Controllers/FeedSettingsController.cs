using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DonkeyWork.Recordings.Audio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/feed-settings")]
[Produces("application/json")]
public class FeedSettingsController : ControllerBase
{
    private readonly IFeedSettingsService _service;

    public FeedSettingsController(IFeedSettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<FeedSettingsV1> Get(CancellationToken cancellationToken)
    {
        return _service.GetAsync(BuildOrigin(), cancellationToken);
    }

    [HttpPut]
    public Task<FeedSettingsV1> Update(
        [FromBody] UpdateFeedSettingsRequestV1 request,
        CancellationToken cancellationToken)
    {
        return _service.UpdateAsync(request, BuildOrigin(), cancellationToken);
    }

    private string BuildOrigin() => $"{Request.Scheme}://{Request.Host}";
}
