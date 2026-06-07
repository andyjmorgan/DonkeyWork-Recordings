using DonkeyWork.Recordings.Identity.Core.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonkeyWork.Recordings.Integration.Tests.Persistence;

public class PersistenceRoundTripTests : IClassFixture<RecordingsTestFixture>
{
    private readonly RecordingsTestFixture _fixture;

    public PersistenceRoundTripTests(RecordingsTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Collection_And_Recording_Round_Trip_With_New_Columns()
    {
        var userId = Guid.NewGuid();

        Guid collectionId;
        Guid recordingId;
        DateTimeOffset writeStartedAt = DateTimeOffset.UtcNow;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var collection = new TtsAudioCollectionEntity
            {
                UserId = userId,
                Name = "Daily News",
                Description = "Test channel",
                DefaultVoice = "af_heart",
                DefaultLanguage = "en-US",
                Author = "Test Author",
                AuthorEmail = "test@example.com",
                IsExplicit = false,
                ItunesCategory = "News",
            };
            db.Collections.Add(collection);
            await db.SaveChangesAsync();
            collectionId = collection.Id;

            var recording = new TtsRecordingEntity
            {
                UserId = userId,
                CollectionId = collectionId,
                Name = "First Episode",
                Description = "Smoke test recording",
                FilePath = $"https://s3.donkeywork.dev/recordings/{userId}/{Guid.NewGuid()}.mp3",
                Transcript = "Hello world, this is the first recording.",
                ContentType = "audio/mpeg",
                SizeBytes = 12_345,
                DurationSeconds = 4.25,
                Voice = "af_heart",
                Language = "en-US",
                Status = TtsRecordingStatus.Ready,
                Progress = 1.0,
                SequenceNumber = 1,
                ChapterTitle = "Episode 1",
            };
            db.Recordings.Add(recording);
            await db.SaveChangesAsync();
            recordingId = recording.Id;
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var collection = await db.Collections.SingleAsync(c => c.Id == collectionId);
            Assert.Equal("Daily News", collection.Name);
            Assert.Equal("af_heart", collection.DefaultVoice);
            Assert.Equal("Test Author", collection.Author);
            Assert.Equal("News", collection.ItunesCategory);
            Assert.True(collection.CreatedAt >= writeStartedAt.AddSeconds(-1));
            Assert.NotNull(collection.UpdatedAt);

            var recording = await db.Recordings.SingleAsync(r => r.Id == recordingId);
            Assert.Equal("First Episode", recording.Name);
            Assert.Equal(4.25, recording.DurationSeconds);
            Assert.Equal(TtsRecordingStatus.Ready, recording.Status);
            Assert.Equal(collectionId, recording.CollectionId);
            Assert.Equal(1, recording.SequenceNumber);
            Assert.Equal("en-US", recording.Language);
            Assert.True(recording.CreatedAt >= writeStartedAt.AddSeconds(-1));
        }
    }

    [Fact]
    public async Task Global_User_Filter_Isolates_Tenants()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userA);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            db.Collections.Add(new TtsAudioCollectionEntity
            {
                UserId = userA,
                Name = "A's channel",
                Description = string.Empty,
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userB);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var visibleCount = await db.Collections.CountAsync();
            Assert.Equal(0, visibleCount);

            var unfiltered = await db.Collections.IgnoreQueryFilters().CountAsync(c => c.UserId == userA);
            Assert.True(unfiltered >= 1);
        }
    }
}
