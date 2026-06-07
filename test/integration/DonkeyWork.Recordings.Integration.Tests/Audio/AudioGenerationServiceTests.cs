using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Identity.Core.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

public class AudioGenerationServiceTests : IClassFixture<RecordingsTestFixture>
{
    private readonly RecordingsTestFixture _fixture;

    public AudioGenerationServiceTests(RecordingsTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StartGeneration_Inserts_Pending_Recording_And_Returns_Id()
    {
        var userId = Guid.NewGuid();
        Guid collectionId;
        Guid recordingId;

        await using (var setupScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            setupScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = setupScope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            var collection = new TtsAudioCollectionEntity { UserId = userId, Name = "Smoke channel", Description = "" };
            db.Collections.Add(collection);
            await db.SaveChangesAsync();
            collectionId = collection.Id;
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = scope.ServiceProvider.GetRequiredService<IAudioGenerationService>();

            recordingId = await service.StartGenerationAsync(new StartAudioGenerationRequestV1
            {
                Paragraphs = ["Hello world, this is a generation request that should land as a Pending recording."],
                Name = "Smoke test recording",
                Description = "Inserted by AudioGenerationServiceTests",
                CollectionId = collectionId,
                Voice = "af_heart",
                Language = "en-US",
            });
        }

        Assert.NotEqual(Guid.Empty, recordingId);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var recording = await db.Recordings.SingleAsync(r => r.Id == recordingId);
            Assert.Equal(TtsRecordingStatus.Pending, recording.Status);
            Assert.Equal(0, recording.Progress);
            Assert.Equal("Smoke test recording", recording.Name);
            Assert.Equal(collectionId, recording.CollectionId);
            Assert.Equal("af_heart", recording.Voice);
            Assert.Equal("en-US", recording.Language);
            Assert.Contains("Hello world", recording.Transcript);
        }
    }

    [Fact]
    public async Task Regenerate_Replaces_Transcript_And_Resets_To_Pending()
    {
        var userId = Guid.NewGuid();
        Guid collectionId;
        Guid recordingId;

        await using (var setupScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            setupScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = setupScope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            var collection = new TtsAudioCollectionEntity { UserId = userId, Name = "Re-record channel", Description = "" };
            db.Collections.Add(collection);
            await db.SaveChangesAsync();
            collectionId = collection.Id;
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = scope.ServiceProvider.GetRequiredService<IAudioGenerationService>();

            recordingId = await service.StartGenerationAsync(new StartAudioGenerationRequestV1
            {
                Paragraphs = ["Original first paragraph.", "Original second paragraph."],
                Name = "Re-record me",
                CollectionId = collectionId,
                Voice = "af_heart",
                Language = "en-US",
            });
        }

        // Pretend the first generation completed so we can observe the reset back to Pending.
        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            var recording = await db.Recordings.SingleAsync(r => r.Id == recordingId);
            recording.Status = TtsRecordingStatus.Ready;
            recording.Progress = 1.0;
            recording.FilePath = "https://example.invalid/old.mp3";
            await db.SaveChangesAsync();
        }

        bool started;
        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = scope.ServiceProvider.GetRequiredService<IAudioGenerationService>();

            started = await service.RegenerateAsync(recordingId, new RegenerateRecordingRequestV1
            {
                Paragraphs = ["Edited paragraph one.", "Edited paragraph two."],
            });
        }

        Assert.True(started);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var recording = await db.Recordings.SingleAsync(r => r.Id == recordingId);
            Assert.Equal(TtsRecordingStatus.Pending, recording.Status);
            Assert.Equal(0, recording.Progress);
            Assert.Equal("Edited paragraph one.\n\nEdited paragraph two.", recording.Transcript);
            // The original audio is left in place until the new generation overwrites it.
            Assert.Equal("https://example.invalid/old.mp3", recording.FilePath);
        }
    }

    [Fact]
    public async Task Regenerate_Missing_Recording_Returns_False()
    {
        var userId = Guid.NewGuid();

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        var service = scope.ServiceProvider.GetRequiredService<IAudioGenerationService>();

        var started = await service.RegenerateAsync(Guid.NewGuid(), new RegenerateRecordingRequestV1
        {
            Paragraphs = ["Nothing to re-record."],
        });

        Assert.False(started);
    }

    [Fact]
    public async Task StartGeneration_Without_Collection_Throws()
    {
        var userId = Guid.NewGuid();

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        var service = scope.ServiceProvider.GetRequiredService<IAudioGenerationService>();

        await Assert.ThrowsAsync<ArgumentException>(() => service.StartGenerationAsync(new StartAudioGenerationRequestV1
        {
            Paragraphs = ["A recording with no channel must be rejected."],
            Name = "No channel",
            CollectionId = Guid.Empty,
        }));
    }

    [Fact]
    public async Task StartGeneration_With_Collection_Inherits_Defaults_And_Assigns_Sequence_Number()
    {
        var userId = Guid.NewGuid();
        Guid collectionId;

        await using (var setupScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            setupScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = setupScope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var collection = new TtsAudioCollectionEntity
            {
                UserId = userId,
                Name = "Daily Roundup",
                Description = "Default-voice channel",
                DefaultVoice = "af_bella",
                DefaultLanguage = "en-US",
            };
            db.Collections.Add(collection);
            await db.SaveChangesAsync();
            collectionId = collection.Id;
        }

        Guid firstId, secondId;
        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = scope.ServiceProvider.GetRequiredService<IAudioGenerationService>();

            firstId = await service.StartGenerationAsync(new StartAudioGenerationRequestV1
            {
                Paragraphs = ["First episode of the daily roundup."],
                Name = "Episode 1",
                CollectionId = collectionId,
            });

            secondId = await service.StartGenerationAsync(new StartAudioGenerationRequestV1
            {
                Paragraphs = ["Second episode of the daily roundup."],
                Name = "Episode 2",
                CollectionId = collectionId,
            });
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var first = await db.Recordings.SingleAsync(r => r.Id == firstId);
            var second = await db.Recordings.SingleAsync(r => r.Id == secondId);

            Assert.Equal("af_bella", first.Voice);
            Assert.Equal("en-US", first.Language);
            Assert.Equal(collectionId, first.CollectionId);
            Assert.Equal(1, first.SequenceNumber);
            Assert.Equal(2, second.SequenceNumber);
        }
    }
}
