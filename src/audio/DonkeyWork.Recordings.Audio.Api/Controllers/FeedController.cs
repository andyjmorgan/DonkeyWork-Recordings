using DonkeyWork.Recordings.Audio.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DonkeyWork.Recordings.Audio.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("feeds")]
public class FeedController : ControllerBase
{
    private const string RssMediaType = "application/rss+xml";

    private readonly IFeedService _feedService;

    public FeedController(IFeedService feedService)
    {
        _feedService = feedService;
    }

    [HttpGet("{userId:guid}/all.xml")]
    [Produces(RssMediaType)]
    public async Task<IActionResult> GetMasterFeed(Guid userId, CancellationToken cancellationToken)
    {
        var origin = $"{Request.Scheme}://{Request.Host}";
        var xml = await _feedService.BuildMasterFeedAsync(userId, origin, cancellationToken);
        return xml is null ? NotFound() : Content(xml, RssMediaType);
    }

    [HttpGet("{userId:guid}/{collectionId:guid}.xml")]
    [Produces(RssMediaType)]
    public async Task<IActionResult> GetChannelFeed(Guid userId, Guid collectionId, CancellationToken cancellationToken)
    {
        var origin = $"{Request.Scheme}://{Request.Host}";
        var xml = await _feedService.BuildChannelFeedAsync(userId, collectionId, origin, cancellationToken);
        return xml is null ? NotFound() : Content(xml, RssMediaType);
    }
}
