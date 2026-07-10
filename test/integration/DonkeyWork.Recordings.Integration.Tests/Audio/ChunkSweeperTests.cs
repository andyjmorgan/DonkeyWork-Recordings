using DonkeyWork.Recordings.Audio.Core.Options;
using DonkeyWork.Recordings.Audio.Core.Services;
using DonkeyWork.Recordings.Identity.Core.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using DonkeyWork.Recordings.Storage.Contracts.Models;
using DonkeyWork.Recordings.Storage.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

public class ChunkSweeperTests : IClassFixture<RecordingsTestFixture>
{
    private readonly RecordingsTestFixture _fixture;

    public ChunkSweeperTests(RecordingsTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Sweeps_Chunks_Of_Settled_Recordings_Past_The_Grace_Period()
    {
        var userId = Guid.NewGuid();
        Guid readyId, failedId, generatingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var ready = RecordingChunksTests.NewGeneratingRecording(userId, "Ready & settled");
            ready.Status = TtsRecordingStatus.Ready;
            ready.Progress = 1.0;
            var failed = RecordingChunksTests.NewGeneratingRecording(userId, "Failed & settled");
            failed.Status = TtsRecordingStatus.Failed;
            var generating = RecordingChunksTests.NewGeneratingRecording(userId, "Still generating");

            db.Recordings.AddRange(ready, failed, generating);
            await db.SaveChangesAsync();
            (readyId, failedId, generatingId) = (ready.Id, failed.Id, generating.Id);

            db.RecordingChunks.AddRange(
                RecordingChunksTests.NewChunk(userId, readyId, index: 0),
                RecordingChunksTests.NewChunk(userId, readyId, index: 1),
                RecordingChunksTests.NewChunk(userId, failedId, index: 0),
                RecordingChunksTests.NewChunk(userId, generatingId, index: 0));
            await db.SaveChangesAsync();
        }

        var storage = new FakeStorageService();
        int swept;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            // No identity on purpose: the sweeper runs as a background worker without a request
            // user and must see every user's chunks via IgnoreQueryFilters.
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            var sweeper = new ChunkSweeper(
                db,
                storage,
                Options.Create(new TtsOptions { ChunkSweepGracePeriod = TimeSpan.Zero }),
                NullLogger<ChunkSweeper>.Instance);

            swept = await sweeper.SweepOnceAsync();
        }

        // Tests in this class share one database and the sweep is global, so leftovers from
        // sibling tests may be collected too — assert on this test's recordings, not exact totals.
        Assert.True(swept >= 3, $"Expected at least 3 swept chunks, got {swept}");
        Assert.Contains($"{userId}/{readyId}/chunks/", storage.DeletedPrefixes);
        Assert.Contains($"{userId}/{failedId}/chunks/", storage.DeletedPrefixes);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            Assert.False(await db.RecordingChunks.AnyAsync(c => c.RecordingId == readyId));
            Assert.False(await db.RecordingChunks.AnyAsync(c => c.RecordingId == failedId));
            // In-flight recordings keep their chunks regardless of age.
            Assert.True(await db.RecordingChunks.AnyAsync(c => c.RecordingId == generatingId));
        }
    }

    [Fact]
    public async Task Keeps_Chunks_Of_Recently_Settled_Recordings_Within_The_Grace_Period()
    {
        var userId = Guid.NewGuid();
        Guid recordingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var recording = RecordingChunksTests.NewGeneratingRecording(userId, "Just settled");
            recording.Status = TtsRecordingStatus.Ready;
            recording.Progress = 1.0;
            db.Recordings.Add(recording);
            await db.SaveChangesAsync();
            recordingId = recording.Id;

            db.RecordingChunks.Add(RecordingChunksTests.NewChunk(userId, recordingId, index: 0));
            await db.SaveChangesAsync();
        }

        var storage = new FakeStorageService();

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            var sweeper = new ChunkSweeper(
                db,
                storage,
                Options.Create(new TtsOptions { ChunkSweepGracePeriod = TimeSpan.FromHours(1) }),
                NullLogger<ChunkSweeper>.Instance);

            Assert.Equal(0, await sweeper.SweepOnceAsync());
        }

        Assert.Empty(storage.DeletedPrefixes);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            Assert.True(await db.RecordingChunks.AnyAsync(c => c.RecordingId == recordingId));
        }
    }

    [Fact]
    public async Task Storage_Failure_Does_Not_Block_Row_Cleanup()
    {
        var userId = Guid.NewGuid();
        Guid recordingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var recording = RecordingChunksTests.NewGeneratingRecording(userId, "Storage down");
            recording.Status = TtsRecordingStatus.Ready;
            recording.Progress = 1.0;
            db.Recordings.Add(recording);
            await db.SaveChangesAsync();
            recordingId = recording.Id;

            db.RecordingChunks.Add(RecordingChunksTests.NewChunk(userId, recordingId, index: 0));
            await db.SaveChangesAsync();
        }

        var storage = new FakeStorageService { ThrowOnDelete = true };

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            var sweeper = new ChunkSweeper(
                db,
                storage,
                Options.Create(new TtsOptions { ChunkSweepGracePeriod = TimeSpan.Zero }),
                NullLogger<ChunkSweeper>.Instance);

            Assert.True(await sweeper.SweepOnceAsync() >= 1);
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            Assert.False(await db.RecordingChunks.AnyAsync(c => c.RecordingId == recordingId));
        }
    }

    private sealed class FakeStorageService : IStorageService
    {
        private readonly List<string> _deletedPrefixes = [];

        public bool ThrowOnDelete { get; init; }

        public IReadOnlyList<string> DeletedPrefixes => _deletedPrefixes;

        public Task<StorageUploadResult> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The sweeper never uploads.");

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            if (ThrowOnDelete)
            {
                throw new InvalidOperationException("Simulated storage outage.");
            }

            _deletedPrefixes.Add(prefix);
            return Task.CompletedTask;
        }

        public string GetPublicUrl(string objectKey) => $"http://localhost:9999/recordings/{objectKey}";
    }
}
