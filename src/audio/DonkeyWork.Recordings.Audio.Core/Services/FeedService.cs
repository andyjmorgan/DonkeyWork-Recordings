using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Feed;
using DonkeyWork.Recordings.Audio.Core.Options;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class FeedService : IFeedService
{
    private readonly RecordingsDbContext _dbContext;
    private readonly RecordingsOptions _options;

    public FeedService(RecordingsDbContext dbContext, IOptions<RecordingsOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<string?> BuildMasterFeedAsync(Guid userId, string requestOrigin, CancellationToken cancellationToken = default)
    {
        var recordings = await _dbContext.Recordings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.Status == TtsRecordingStatus.Ready)
            .OrderByDescending(r => r.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (recordings.Count == 0)
        {
            var hasAnyRow = await _dbContext.Recordings
                .IgnoreQueryFilters()
                .AnyAsync(r => r.UserId == userId, cancellationToken);
            if (!hasAnyRow)
            {
                return null;
            }
        }

        var settings = await _dbContext.UserFeedSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        var channel = ResolveMasterChannelMetadata(userId, settings, requestOrigin);
        return FeedXmlBuilder.Build(channel, recordings);
    }

    public async Task<string?> BuildChannelFeedAsync(Guid userId, Guid collectionId, string requestOrigin, CancellationToken cancellationToken = default)
    {
        var collection = await _dbContext.Collections
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId, cancellationToken);

        if (collection is null)
        {
            return null;
        }

        var recordings = await _dbContext.Recordings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.CollectionId == collectionId && r.Status == TtsRecordingStatus.Ready)
            .OrderBy(r => r.SequenceNumber ?? int.MaxValue)
            .ThenBy(r => r.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var channel = ResolveChannelMetadata(userId, collection, requestOrigin);
        return FeedXmlBuilder.Build(channel, recordings);
    }

    private FeedChannelMetadata ResolveMasterChannelMetadata(Guid userId, UserFeedSettingsEntity? settings, string requestOrigin)
    {
        var origin = requestOrigin.TrimEnd('/');
        return new FeedChannelMetadata
        {
            Title = settings?.Title ?? _options.DefaultFeedTitle,
            Description = settings?.Description ?? _options.DefaultFeedDescription,
            Language = settings?.Language ?? _options.DefaultLanguage,
            SelfUrl = $"{origin}/feeds/{userId}/all.xml",
            HomepageLink = settings?.Link,
            Author = settings?.Author,
            AuthorEmail = settings?.AuthorEmail,
            ItunesCategory = settings?.ItunesCategory,
            IsExplicit = settings?.IsExplicit ?? false,
            ImageUrl = settings?.CoverImagePath,
        };
    }

    private FeedChannelMetadata ResolveChannelMetadata(Guid userId, TtsAudioCollectionEntity collection, string requestOrigin)
    {
        var origin = requestOrigin.TrimEnd('/');
        return new FeedChannelMetadata
        {
            Title = string.IsNullOrWhiteSpace(collection.Name) ? _options.DefaultFeedTitle : collection.Name,
            Description = string.IsNullOrWhiteSpace(collection.Description) ? _options.DefaultFeedDescription : collection.Description,
            Language = collection.DefaultLanguage ?? _options.DefaultLanguage,
            SelfUrl = $"{origin}/feeds/{userId}/{collection.Id}.xml",
            HomepageLink = collection.Link,
            Author = collection.Author,
            AuthorEmail = collection.AuthorEmail,
            ItunesCategory = collection.ItunesCategory,
            IsExplicit = collection.IsExplicit,
            ImageUrl = collection.CoverImagePath,
        };
    }
}
