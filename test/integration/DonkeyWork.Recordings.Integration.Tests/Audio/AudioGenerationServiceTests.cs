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
        Guid recordingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = scope.ServiceProvider.GetRequiredService<IAudioGenerationService>();

            recordingId = await service.StartGenerationAsync(new StartAudioGenerationRequestV1
            {
                Text = "Hello world, this is a generation request that should land as a Pending recording.",
                Name = "Smoke test recording",
                Description = "Inserted by AudioGenerationServiceTests",
                Voice = "Magpie-Multilingual.EN-US.Aria",
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
            Assert.Equal("Magpie-Multilingual.EN-US.Aria", recording.Voice);
            Assert.Equal("en-US", recording.Language);
            Assert.Contains("Hello world", recording.Transcript);
        }
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
                DefaultVoice = "Magpie-Multilingual.EN-US.Mia",
                DefaultLanguage = "en-US",
                Tone = "warm conversational",
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
                Text = "First episode of the daily roundup.",
                Name = "Episode 1",
                CollectionId = collectionId,
            });

            secondId = await service.StartGenerationAsync(new StartAudioGenerationRequestV1
            {
                Text = "Second episode of the daily roundup.",
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

            Assert.Equal("Magpie-Multilingual.EN-US.Mia", first.Voice);
            Assert.Equal("en-US", first.Language);
            Assert.Equal(collectionId, first.CollectionId);
            Assert.Equal(1, first.SequenceNumber);
            Assert.Equal(2, second.SequenceNumber);
        }
    }
}
