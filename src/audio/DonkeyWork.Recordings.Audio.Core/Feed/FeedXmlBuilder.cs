using System.Globalization;
using System.Xml.Linq;
using DonkeyWork.Recordings.Persistence.Entities.Tts;

namespace DonkeyWork.Recordings.Audio.Core.Feed;

public static class FeedXmlBuilder
{
    private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Podcast = "https://podcastindex.org/namespace/1.0";

    public static string Build(FeedChannelMetadata channel, IReadOnlyList<TtsRecordingEntity> recordings)
    {
        var rss = new XElement(
            "rss",
            new XAttribute("version", "2.0"),
            new XAttribute(XNamespace.Xmlns + "itunes", Itunes.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "atom", Atom.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "podcast", Podcast.NamespaceName),
            new XElement(
                "channel",
                BuildChannelHeader(channel),
                recordings.Select(r => BuildItem(r, channel))));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), rss);
        return doc.Declaration + Environment.NewLine + doc.ToString(SaveOptions.None);
    }

    private static IEnumerable<XObject> BuildChannelHeader(FeedChannelMetadata channel)
    {
        yield return new XElement("title", channel.Title);
        yield return new XElement("link", channel.HomepageLink ?? channel.SelfUrl);
        yield return new XElement("description", channel.Description);
        yield return new XElement("language", channel.Language);
        yield return new XElement("generator", "DonkeyWork Recordings");
        yield return new XElement("lastBuildDate", DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture));
        yield return new XElement(Itunes + "type", "episodic");
        yield return new XElement(Atom + "link",
            new XAttribute("href", channel.SelfUrl),
            new XAttribute("rel", "self"),
            new XAttribute("type", "application/rss+xml"));

        if (!string.IsNullOrEmpty(channel.Author))
        {
            yield return new XElement(Itunes + "author", channel.Author);
        }

        yield return new XElement(Itunes + "summary", channel.Description);
        yield return new XElement(Itunes + "explicit", channel.IsExplicit ? "true" : "false");

        if (!string.IsNullOrEmpty(channel.ItunesCategory))
        {
            yield return new XElement(Itunes + "category", new XAttribute("text", channel.ItunesCategory));
        }

        if (!string.IsNullOrEmpty(channel.AuthorEmail))
        {
            yield return new XElement(Itunes + "owner",
                new XElement(Itunes + "name", channel.Author ?? string.Empty),
                new XElement(Itunes + "email", channel.AuthorEmail));
        }

        if (!string.IsNullOrEmpty(channel.ImageUrl))
        {
            yield return new XElement(Itunes + "image", new XAttribute("href", channel.ImageUrl));
            yield return new XElement(
                "image",
                new XElement("url", channel.ImageUrl),
                new XElement("title", channel.Title),
                new XElement("link", channel.HomepageLink ?? channel.SelfUrl));
        }
    }

    private static XElement BuildItem(TtsRecordingEntity r, FeedChannelMetadata channel)
    {
        var title = r.ChapterTitle ?? r.Name;
        var pubDate = r.CreatedAt.ToString("R", CultureInfo.InvariantCulture);
        var duration = FormatDuration(r.DurationSeconds);
        var description = string.IsNullOrEmpty(r.Description) ? title : r.Description;

        var item = new XElement(
            "item",
            new XElement("title", title),
            new XElement("guid", new XAttribute("isPermaLink", "false"), r.Id.ToString()),
            new XElement("description", description),
            new XElement("pubDate", pubDate),
            new XElement("enclosure",
                new XAttribute("url", r.FilePath),
                new XAttribute("length", r.SizeBytes.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("type", r.ContentType)),
            new XElement(Itunes + "title", title),
            new XElement(Itunes + "duration", duration),
            new XElement(Itunes + "summary", description),
            new XElement(Itunes + "episodeType", "full"),
            new XElement(Itunes + "explicit", channel.IsExplicit ? "true" : "false"));

        // No per-episode artwork yet, so fall back to the channel cover (itself defaulted) so every
        // episode shows art. When recordings carry their own image, prefer it here.
        if (!string.IsNullOrEmpty(channel.ImageUrl))
        {
            item.Add(new XElement(Itunes + "image", new XAttribute("href", channel.ImageUrl)));
        }

        if (r.SequenceNumber.HasValue)
        {
            item.Add(new XElement(Itunes + "episode", r.SequenceNumber.Value));
        }

        if (!string.IsNullOrWhiteSpace(r.Transcript))
        {
            // VTT first — Apple Podcasts only renders VTT/SRT. text/plain stays for other readers.
            item.Add(new XElement(Podcast + "transcript",
                new XAttribute("url", $"{channel.FeedBaseUrl}/{r.Id}.vtt"),
                new XAttribute("type", "text/vtt"),
                new XAttribute("language", channel.Language)));
            item.Add(new XElement(Podcast + "transcript",
                new XAttribute("url", $"{channel.FeedBaseUrl}/{r.Id}.txt"),
                new XAttribute("type", "text/plain"),
                new XAttribute("language", channel.Language)));
        }

        return item;
    }

    private static string FormatDuration(double totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return "00:00:00";
        }

        var ts = TimeSpan.FromSeconds(Math.Round(totalSeconds));
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}

public sealed record FeedChannelMetadata
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string Language { get; init; }

    public required string SelfUrl { get; init; }

    public string? HomepageLink { get; init; }

    public string? Author { get; init; }

    public string? AuthorEmail { get; init; }

    public string? ItunesCategory { get; init; }

    public bool IsExplicit { get; init; }

    public string? ImageUrl { get; init; }

    public required string FeedBaseUrl { get; init; }
}
