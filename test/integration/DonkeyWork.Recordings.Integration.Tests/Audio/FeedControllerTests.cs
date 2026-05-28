using System.Net;
using DonkeyWork.Recordings.Identity.Core.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.Extensions.DependencyInjection;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

public class FeedControllerTests : IClassFixture<RecordingsTestFixture>
{
    private readonly RecordingsTestFixture _fixture;

    public FeedControllerTests(RecordingsTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Master_Feed_Is_Public_And_Lists_Ready_Recordings()
    {
        var userId = Guid.NewGuid();

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            db.Recordings.Add(BuildRecording(userId, "Ready episode", TtsRecordingStatus.Ready));
            db.Recordings.Add(BuildRecording(userId, "Still pending", TtsRecordingStatus.Pending));
            await db.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient();
        using var response = await client.GetAsync($"/feeds/{userId}/all.xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/rss+xml", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<rss", body);
        Assert.Contains("xmlns:itunes", body);
        Assert.Contains("Ready episode", body);
        Assert.DoesNotContain("Still pending", body);
    }

    [Fact]
    public async Task Random_User_Returns_404_Without_Leaking_Existence()
    {
        using var client = _fixture.Factory.CreateClient();
        using var response = await client.GetAsync($"/feeds/{Guid.NewGuid()}/all.xml");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Channel_Feed_Returns_Ordered_Recordings_For_That_Channel_Only()
    {
        var userId = Guid.NewGuid();
        Guid collectionId, otherCollectionId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var collection = new TtsAudioCollectionEntity
            {
                UserId = userId,
                Name = "Channel under test",
                Description = "Description here",
                DefaultLanguage = "en-US",
            };
            var otherCollection = new TtsAudioCollectionEntity
            {
                UserId = userId,
                Name = "Other channel",
                Description = "Should not appear",
            };
            db.Collections.Add(collection);
            db.Collections.Add(otherCollection);
            await db.SaveChangesAsync();
            collectionId = collection.Id;
            otherCollectionId = otherCollection.Id;

            var second = BuildRecording(userId, "B is the second", TtsRecordingStatus.Ready);
            second.CollectionId = collectionId;
            second.SequenceNumber = 2;
            var first = BuildRecording(userId, "A is the first", TtsRecordingStatus.Ready);
            first.CollectionId = collectionId;
            first.SequenceNumber = 1;
            var outsider = BuildRecording(userId, "Outsider episode", TtsRecordingStatus.Ready);
            outsider.CollectionId = otherCollectionId;
            outsider.SequenceNumber = 1;

            db.Recordings.AddRange(second, first, outsider);
            await db.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient();
        using var response = await client.GetAsync($"/feeds/{userId}/{collectionId}.xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("A is the first", body);
        Assert.Contains("B is the second", body);
        Assert.DoesNotContain("Outsider episode", body);
        Assert.Contains("Channel under test", body);

        var firstIdx = body.IndexOf("A is the first", StringComparison.Ordinal);
        var secondIdx = body.IndexOf("B is the second", StringComparison.Ordinal);
        Assert.True(firstIdx < secondIdx, "Expected SequenceNumber 1 before 2 in the feed XML.");
    }

    private static TtsRecordingEntity BuildRecording(Guid userId, string name, TtsRecordingStatus status)
    {
        var id = Guid.NewGuid();
        return new TtsRecordingEntity
        {
            Id = id,
            UserId = userId,
            Name = name,
            Description = name,
            FilePath = $"https://s3.donkeywork.dev/recordings/{userId}/{id}.mp3",
            Transcript = "...",
            ContentType = "audio/mpeg",
            SizeBytes = 100_000,
            DurationSeconds = 30,
            Status = status,
            Progress = status == TtsRecordingStatus.Ready ? 1.0 : 0.0,
        };
    }
}
