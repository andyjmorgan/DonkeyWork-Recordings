using System.Xml.Linq;
using DonkeyWork.Recordings.Audio.Core.Feed;
using DonkeyWork.Recordings.Persistence.Entities.Tts;

namespace DonkeyWork.Recordings.Audio.Core.Tests.Feed;

public class FeedXmlBuilderTests
{
    private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    [Fact]
    public void Build_Emits_Rss2_With_Itunes_Namespace_And_All_Items()
    {
        var userId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();

        var channel = new FeedChannelMetadata
        {
            Title = "Daily Roundup",
            Description = "Smoke test feed",
            Language = "en-US",
            SelfUrl = $"https://recordings.donkeywork.dev/feeds/{userId}/{collectionId}.xml",
            Author = "Tester",
            AuthorEmail = "tester@example.com",
            ItunesCategory = "News",
            IsExplicit = false,
            ImageUrl = "https://s3.donkeywork.dev/recordings/cover.jpg",
        };

        var recordings = new List<TtsRecordingEntity>
        {
            BuildRecording(userId, collectionId, name: "Episode 1", sequence: 1, durationSeconds: 65, sizeBytes: 100_000),
            BuildRecording(userId, collectionId, name: "Episode 2", sequence: 2, durationSeconds: 3725, sizeBytes: 200_000),
        };

        var xml = FeedXmlBuilder.Build(channel, recordings);
        var doc = XDocument.Parse(xml);
        var rss = doc.Root!;

        Assert.Equal("rss", rss.Name.LocalName);
        Assert.Equal("2.0", rss.Attribute("version")?.Value);
        Assert.Equal(Itunes.NamespaceName, rss.GetNamespaceOfPrefix("itunes")?.NamespaceName);

        var channelEl = rss.Element("channel")!;
        Assert.Equal("Daily Roundup", channelEl.Element("title")?.Value);
        Assert.Equal("en-US", channelEl.Element("language")?.Value);
        Assert.Equal("Tester", channelEl.Element(Itunes + "author")?.Value);
        Assert.Equal("false", channelEl.Element(Itunes + "explicit")?.Value);
        Assert.Equal("News", channelEl.Element(Itunes + "category")?.Attribute("text")?.Value);
        Assert.Equal("https://s3.donkeywork.dev/recordings/cover.jpg", channelEl.Element(Itunes + "image")?.Attribute("href")?.Value);

        var atomSelf = channelEl.Element(Atom + "link");
        Assert.Equal($"https://recordings.donkeywork.dev/feeds/{userId}/{collectionId}.xml", atomSelf?.Attribute("href")?.Value);
        Assert.Equal("self", atomSelf?.Attribute("rel")?.Value);

        var items = channelEl.Elements("item").ToList();
        Assert.Equal(2, items.Count);

        var first = items[0];
        Assert.Equal("Episode 1", first.Element("title")?.Value);
        Assert.Equal("audio/mpeg", first.Element("enclosure")?.Attribute("type")?.Value);
        Assert.Equal("100000", first.Element("enclosure")?.Attribute("length")?.Value);
        Assert.Equal("01:05", first.Element(Itunes + "duration")?.Value);
        Assert.Equal("1", first.Element(Itunes + "episode")?.Value);
        Assert.Equal("false", first.Element("guid")?.Attribute("isPermaLink")?.Value);

        var second = items[1];
        Assert.Equal("01:02:05", second.Element(Itunes + "duration")?.Value);
        Assert.Equal("2", second.Element(Itunes + "episode")?.Value);
    }

    [Fact]
    public void Build_Uses_ChapterTitle_When_Present()
    {
        var rec = BuildRecording(Guid.NewGuid(), null, name: "Internal name", sequence: 1, durationSeconds: 10, sizeBytes: 1000);
        rec.ChapterTitle = "Public chapter title";

        var xml = FeedXmlBuilder.Build(
            new FeedChannelMetadata
            {
                Title = "Test",
                Description = "Test",
                Language = "en-US",
                SelfUrl = "https://example.com/feed",
            },
            new[] { rec });

        var doc = XDocument.Parse(xml);
        var item = doc.Root!.Element("channel")!.Element("item")!;
        Assert.Equal("Public chapter title", item.Element("title")?.Value);
        Assert.Equal("Public chapter title", item.Element(Itunes + "title")?.Value);
    }

    private static TtsRecordingEntity BuildRecording(Guid userId, Guid? collectionId, string name, int sequence, double durationSeconds, long sizeBytes)
    {
        var id = Guid.NewGuid();
        return new TtsRecordingEntity
        {
            Id = id,
            UserId = userId,
            CollectionId = collectionId,
            Name = name,
            Description = $"Description for {name}",
            FilePath = $"https://s3.donkeywork.dev/recordings/{userId}/{id}.mp3",
            Transcript = "...",
            ContentType = "audio/mpeg",
            SizeBytes = sizeBytes,
            DurationSeconds = durationSeconds,
            Status = TtsRecordingStatus.Ready,
            Progress = 1.0,
            SequenceNumber = sequence,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
