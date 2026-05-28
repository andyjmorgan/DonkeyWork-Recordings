using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class FeedSettingsService : IFeedSettingsService
{
    private readonly RecordingsDbContext _dbContext;
    private readonly IIdentityContext _identityContext;
    private readonly RecordingsOptions _options;

    public FeedSettingsService(
        RecordingsDbContext dbContext,
        IIdentityContext identityContext,
        IOptions<RecordingsOptions> options)
    {
        _dbContext = dbContext;
        _identityContext = identityContext;
        _options = options.Value;
    }

    public async Task<FeedSettingsV1> GetAsync(string requestOrigin, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var settings = await _dbContext.UserFeedSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == _identityContext.UserId, cancellationToken);

        return BuildV1(settings, requestOrigin);
    }

    public async Task<FeedSettingsV1> UpdateAsync(UpdateFeedSettingsRequestV1 request, string requestOrigin, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var settings = await _dbContext.UserFeedSettings
            .FirstOrDefaultAsync(s => s.UserId == _identityContext.UserId, cancellationToken);

        if (settings is null)
        {
            settings = new UserFeedSettingsEntity
            {
                UserId = _identityContext.UserId,
                Title = _options.DefaultFeedTitle,
                Description = _options.DefaultFeedDescription,
                Language = _options.DefaultLanguage,
            };
            _dbContext.UserFeedSettings.Add(settings);
        }

        if (request.Title is not null) settings.Title = request.Title;
        if (request.Description is not null) settings.Description = request.Description;
        if (request.Author is not null) settings.Author = request.Author;
        if (request.AuthorEmail is not null) settings.AuthorEmail = request.AuthorEmail;
        if (request.Language is not null) settings.Language = request.Language;
        if (request.CoverImagePath is not null) settings.CoverImagePath = request.CoverImagePath;
        if (request.Link is not null) settings.Link = request.Link;
        if (request.IsExplicit.HasValue) settings.IsExplicit = request.IsExplicit.Value;
        if (request.ItunesCategory is not null) settings.ItunesCategory = request.ItunesCategory;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildV1(settings, requestOrigin);
    }

    private void EnsureAuthenticated()
    {
        if (!_identityContext.IsAuthenticated)
        {
            throw new InvalidOperationException("Feed settings require an authenticated identity.");
        }
    }

    private FeedSettingsV1 BuildV1(UserFeedSettingsEntity? entity, string requestOrigin)
    {
        var origin = requestOrigin.TrimEnd('/');
        var userId = _identityContext.UserId;

        return new FeedSettingsV1
        {
            Title = entity?.Title ?? _options.DefaultFeedTitle,
            Description = entity?.Description ?? _options.DefaultFeedDescription,
            Author = entity?.Author,
            AuthorEmail = entity?.AuthorEmail,
            Language = entity?.Language ?? _options.DefaultLanguage,
            CoverImagePath = entity?.CoverImagePath,
            Link = entity?.Link,
            IsExplicit = entity?.IsExplicit ?? false,
            ItunesCategory = entity?.ItunesCategory,
            MasterFeedUrl = $"{origin}/feeds/{userId}/all.xml",
        };
    }
}
