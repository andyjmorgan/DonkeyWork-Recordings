using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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

    public async Task<string?> GetTranscriptTextAsync(Guid userId, Guid recordingId, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadTranscriptAsync(userId, recordingId, cancellationToken);
        return loaded?.Text;
    }

    public async Task<string?> GetTranscriptVttAsync(Guid userId, Guid recordingId, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadTranscriptAsync(userId, recordingId, cancellationToken);
        return loaded is null ? null : BuildVtt(loaded.Value.Text, loaded.Value.Duration);
    }

    private async Task<(string Text, double Duration)?> LoadTranscriptAsync(Guid userId, Guid recordingId, CancellationToken cancellationToken)
    {
        var recording = await _dbContext.Recordings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == recordingId && r.UserId == userId, cancellationToken);

        if (recording is null)
        {
            return null;
        }

        // The processed (spoken) text is what the audio actually says; fall back to the raw input.
        var text = !string.IsNullOrWhiteSpace(recording.ProcessedTranscript)
            ? recording.ProcessedTranscript
            : recording.Transcript;

        return string.IsNullOrWhiteSpace(text) ? null : (text, recording.DurationSeconds);
    }

    // Apple Podcasts only renders VTT/SRT transcripts (not text/plain). We don't capture per-cue
    // timings yet, so spread cues across the episode duration proportionally to their length — an
    // approximation that's close enough for synced highlighting at a roughly constant speech rate.
    private static string BuildVtt(string text, double durationSeconds)
    {
        var cues = Regex.Split(text.Trim(), @"(?<=[.!?])\s+")
            .Select(s => s.Replace('\n', ' ').Replace('\r', ' ').Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (cues.Count == 0)
        {
            return "WEBVTT\n";
        }

        var totalChars = cues.Sum(c => c.Length);
        var duration = durationSeconds > 0 ? durationSeconds : Math.Max(1.0, totalChars / 15.0);

        var sb = new StringBuilder("WEBVTT\n\n");
        double acc = 0;
        foreach (var cue in cues)
        {
            var start = duration * (acc / totalChars);
            acc += cue.Length;
            var end = duration * (acc / totalChars);
            sb.Append(FormatVttTimestamp(start)).Append(" --> ").Append(FormatVttTimestamp(end)).Append('\n');
            sb.Append(cue).Append("\n\n");
        }

        return sb.ToString();
    }

    private static string FormatVttTimestamp(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}.{3:D3}",
            (int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds);
    }

    private FeedChannelMetadata ResolveMasterChannelMetadata(Guid userId, UserFeedSettingsEntity? settings, string requestOrigin)
    {
        var origin = requestOrigin.TrimEnd('/');
        return new FeedChannelMetadata
        {
            FeedBaseUrl = $"{origin}/feeds/{userId}",
            Title = settings?.Title ?? _options.DefaultFeedTitle,
            Description = settings?.Description ?? _options.DefaultFeedDescription,
            Language = settings?.Language ?? _options.DefaultLanguage,
            SelfUrl = $"{origin}/feeds/{userId}/all.xml",
            HomepageLink = settings?.Link ?? _options.PublicBaseUrl,
            Author = settings?.Author,
            AuthorEmail = settings?.AuthorEmail,
            ItunesCategory = settings?.ItunesCategory,
            IsExplicit = settings?.IsExplicit ?? false,
            ImageUrl = string.IsNullOrWhiteSpace(settings?.CoverImagePath) ? _options.DefaultCoverImageUrl : settings.CoverImagePath,
        };
    }

    private FeedChannelMetadata ResolveChannelMetadata(Guid userId, TtsAudioCollectionEntity collection, string requestOrigin)
    {
        var origin = requestOrigin.TrimEnd('/');
        return new FeedChannelMetadata
        {
            FeedBaseUrl = $"{origin}/feeds/{userId}",
            Title = string.IsNullOrWhiteSpace(collection.Name) ? _options.DefaultFeedTitle : collection.Name,
            Description = string.IsNullOrWhiteSpace(collection.Description) ? _options.DefaultFeedDescription : collection.Description,
            Language = collection.DefaultLanguage ?? _options.DefaultLanguage,
            SelfUrl = $"{origin}/feeds/{userId}/{collection.Id}.xml",
            HomepageLink = collection.Link ?? _options.PublicBaseUrl,
            Author = collection.Author,
            AuthorEmail = collection.AuthorEmail,
            ItunesCategory = collection.ItunesCategory,
            IsExplicit = collection.IsExplicit,
            ImageUrl = string.IsNullOrWhiteSpace(collection.CoverImagePath) ? _options.DefaultCoverImageUrl : collection.CoverImagePath,
        };
    }
}
