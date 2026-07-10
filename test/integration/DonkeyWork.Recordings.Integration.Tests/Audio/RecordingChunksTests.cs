using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Identity.Core.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

public class RecordingChunksTests : IClassFixture<RecordingsTestFixture>
{
    private readonly RecordingsTestFixture _fixture;

    public RecordingChunksTests(RecordingsTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_Recording_Includes_Chunks_And_Contiguous_PlayableUpTo()
    {
        var userId = Guid.NewGuid();
        Guid recordingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var recording = NewGeneratingRecording(userId, "Chunked recording");
            db.Recordings.Add(recording);
            await db.SaveChangesAsync();
            recordingId = recording.Id;

            // Persisted out of order with a hole at index 2: 0, 1 and 3 are done.
            db.RecordingChunks.AddRange(
                NewChunk(userId, recordingId, index: 3),
                NewChunk(userId, recordingId, index: 0),
                NewChunk(userId, recordingId, index: 1));
            await db.SaveChangesAsync();
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = scope.ServiceProvider.GetRequiredService<ITtsService>();

            var recording = await service.GetRecordingAsync(recordingId);

            Assert.NotNull(recording);
            Assert.Equal([0, 1, 3], recording.Chunks.Select(c => c.Index));
            Assert.All(recording.Chunks, c => Assert.Contains($"/{recordingId}/chunks/", c.Url));
            // Index 2 is still in flight, so the watermark stops at 1 despite 3 being persisted.
            Assert.Equal(1, recording.PlayableUpTo);
        }
    }

    [Fact]
    public async Task Get_Recording_Without_Chunks_Reports_Empty_And_Minus_One()
    {
        var userId = Guid.NewGuid();
        Guid recordingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            var recording = NewGeneratingRecording(userId, "No chunks yet");
            db.Recordings.Add(recording);
            await db.SaveChangesAsync();
            recordingId = recording.Id;
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = scope.ServiceProvider.GetRequiredService<ITtsService>();

            var recording = await service.GetRecordingAsync(recordingId);

            Assert.NotNull(recording);
            Assert.Empty(recording.Chunks);
            Assert.Equal(-1, recording.PlayableUpTo);
        }
    }

    [Fact]
    public async Task Deleting_A_Recording_Cascades_Its_Chunk_Rows()
    {
        var userId = Guid.NewGuid();
        Guid recordingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var recording = NewGeneratingRecording(userId, "Doomed recording");
            db.Recordings.Add(recording);
            await db.SaveChangesAsync();
            recordingId = recording.Id;

            db.RecordingChunks.AddRange(
                NewChunk(userId, recordingId, index: 0),
                NewChunk(userId, recordingId, index: 1));
            await db.SaveChangesAsync();
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = scope.ServiceProvider.GetRequiredService<ITtsService>();
            // Storage deletion is best-effort (the test S3 endpoint is a black hole); the row
            // delete must still succeed.
            Assert.True(await service.DeleteRecordingAsync(recordingId));
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            Assert.False(await db.Recordings.AnyAsync(r => r.Id == recordingId));
            Assert.False(await db.RecordingChunks.AnyAsync(c => c.RecordingId == recordingId));
        }
    }

    [Fact]
    public async Task Duplicate_Chunk_Index_For_A_Recording_Is_Rejected()
    {
        var userId = Guid.NewGuid();

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

        var recording = NewGeneratingRecording(userId, "Unique index recording");
        db.Recordings.Add(recording);
        await db.SaveChangesAsync();

        db.RecordingChunks.Add(NewChunk(userId, recording.Id, index: 0));
        await db.SaveChangesAsync();

        db.RecordingChunks.Add(NewChunk(userId, recording.Id, index: 0));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    internal static TtsRecordingEntity NewGeneratingRecording(Guid userId, string name) => new()
    {
        UserId = userId,
        Name = name,
        Description = string.Empty,
        FilePath = string.Empty,
        Transcript = "Test transcript.",
        ContentType = "audio/mpeg",
        Status = TtsRecordingStatus.Generating,
        Progress = 0.5,
        StatusDetail = "Generating audio — segment 1 of 3",
    };

    internal static TtsRecordingChunkEntity NewChunk(Guid userId, Guid recordingId, int index) => new()
    {
        UserId = userId,
        RecordingId = recordingId,
        Index = index,
        StoragePath = $"{userId}/{recordingId}/chunks/{index}.wav",
        FilePath = $"http://localhost:9999/recordings/{userId}/{recordingId}/chunks/{index}.wav",
        SizeBytes = 48_000,
        DurationSeconds = 1.0,
    };
}
