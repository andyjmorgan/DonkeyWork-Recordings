using System.Text;
using System.Text.Json;
using DonkeyWork.Recordings.Audio.Core.Services;
using DonkeyWork.Recordings.Identity.Core.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

// Exercises the SSE contract against a real database: replay-on-connect, live chunk/progress
// events, and both terminal events. The controller adds auth + headers on top of this stream
// (see ControllerAuthGateTests for the 401 gate).
public class RecordingEventStreamTests : IClassFixture<RecordingsTestFixture>
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);

    private readonly RecordingsTestFixture _fixture;

    public RecordingEventStreamTests(RecordingsTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Ready_Recording_Replays_Chunks_Then_Emits_Ready_And_Completes()
    {
        var userId = Guid.NewGuid();
        Guid recordingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var recording = RecordingChunksTests.NewGeneratingRecording(userId, "Replay ready");
            recording.Status = TtsRecordingStatus.Ready;
            recording.Progress = 1.0;
            recording.FilePath = "http://localhost:9999/recordings/final.mp3";
            db.Recordings.Add(recording);
            await db.SaveChangesAsync();
            recordingId = recording.Id;

            db.RecordingChunks.AddRange(
                RecordingChunksTests.NewChunk(userId, recordingId, index: 0),
                RecordingChunksTests.NewChunk(userId, recordingId, index: 1));
            await db.SaveChangesAsync();
        }

        var events = await CollectUntilCompletedAsync(userId, recordingId);

        Assert.Equal(["chunk-ready", "chunk-ready", "ready"], events.Select(e => e.Name));

        Assert.Equal(0, events[0].Data.GetProperty("index").GetInt32());
        Assert.Equal(0, events[0].Data.GetProperty("playableUpTo").GetInt32());
        Assert.Contains("/chunks/0.wav", events[0].Data.GetProperty("url").GetString());

        Assert.Equal(1, events[1].Data.GetProperty("index").GetInt32());
        Assert.Equal(1, events[1].Data.GetProperty("playableUpTo").GetInt32());

        Assert.Equal("http://localhost:9999/recordings/final.mp3", events[2].Data.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Failed_Recording_Emits_Failed_And_Completes()
    {
        var userId = Guid.NewGuid();
        Guid recordingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var recording = RecordingChunksTests.NewGeneratingRecording(userId, "Replay failed");
            recording.Status = TtsRecordingStatus.Failed;
            recording.ErrorMessage = "Kokoro exploded";
            db.Recordings.Add(recording);
            await db.SaveChangesAsync();
            recordingId = recording.Id;
        }

        var events = await CollectUntilCompletedAsync(userId, recordingId);

        var failed = Assert.Single(events);
        Assert.Equal("failed", failed.Name);
        Assert.Equal("Kokoro exploded", failed.Data.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Live_Chunks_And_Terminal_Ready_Are_Streamed_As_They_Land()
    {
        var userId = Guid.NewGuid();
        Guid recordingId;

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();

            var recording = RecordingChunksTests.NewGeneratingRecording(userId, "Live stream");
            db.Recordings.Add(recording);
            await db.SaveChangesAsync();
            recordingId = recording.Id;

            db.RecordingChunks.Add(RecordingChunksTests.NewChunk(userId, recordingId, index: 0));
            await db.SaveChangesAsync();
        }

        await using var streamScope = _fixture.Factory.Services.CreateAsyncScope();
        streamScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        var db2 = streamScope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
        var stream = new RecordingEventStream(db2, pollInterval: TimeSpan.FromMilliseconds(50), heartbeatInterval: TimeSpan.FromSeconds(30));

        var output = new CollectingStream();
        using var cts = new CancellationTokenSource(WaitTimeout);
        var streamTask = stream.StreamAsync(recordingId, output, cts.Token);

        // Replay on connect: chunk 0 + the current progress snapshot.
        await WaitForAsync(() => ParseEvents(output.Text).Count(e => e.Name == "chunk-ready") == 1, output);
        await WaitForAsync(() => ParseEvents(output.Text).Any(e => e.Name == "progress"), output);

        // Chunks land out of order: index 2 first (watermark frozen at 0), then index 1 (unlocks 2).
        await MutateAsync(userId, db => db.RecordingChunks.Add(RecordingChunksTests.NewChunk(userId, recordingId, index: 2)));
        await WaitForAsync(() => ParseEvents(output.Text).Count(e => e.Name == "chunk-ready") == 2, output);

        await MutateAsync(userId, db => db.RecordingChunks.Add(RecordingChunksTests.NewChunk(userId, recordingId, index: 1)));
        await WaitForAsync(() => ParseEvents(output.Text).Count(e => e.Name == "chunk-ready") == 3, output);

        await MutateAsync(userId, async db =>
        {
            var recording = await db.Recordings.SingleAsync(r => r.Id == recordingId);
            recording.Status = TtsRecordingStatus.Ready;
            recording.Progress = 1.0;
            recording.FilePath = "http://localhost:9999/recordings/live-final.mp3";
        });

        await streamTask.WaitAsync(WaitTimeout);

        var events = ParseEvents(output.Text);
        var chunkEvents = events.Where(e => e.Name == "chunk-ready").ToList();

        Assert.Equal([0, 2, 1], chunkEvents.Select(e => e.Data.GetProperty("index").GetInt32()));
        // Watermark gates playback across the out-of-order arrivals: 0 → still 0 (gap at 1) → 2.
        Assert.Equal([0, 0, 2], chunkEvents.Select(e => e.Data.GetProperty("playableUpTo").GetInt32()));

        var progress = events.First(e => e.Name == "progress");
        Assert.Equal(0.5, progress.Data.GetProperty("progress").GetDouble());
        Assert.Equal("Generating audio — segment 1 of 3", progress.Data.GetProperty("statusDetail").GetString());

        var ready = events.Last();
        Assert.Equal("ready", ready.Name);
        Assert.Equal("http://localhost:9999/recordings/live-final.mp3", ready.Data.GetProperty("url").GetString());
    }

    private async Task<List<SseEvent>> CollectUntilCompletedAsync(Guid userId, Guid recordingId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
        var stream = new RecordingEventStream(db, pollInterval: TimeSpan.FromMilliseconds(50), heartbeatInterval: TimeSpan.FromSeconds(30));

        var output = new CollectingStream();
        using var cts = new CancellationTokenSource(WaitTimeout);
        await stream.StreamAsync(recordingId, output, cts.Token);

        return ParseEvents(output.Text);
    }

    private async Task MutateAsync(Guid userId, Action<RecordingsDbContext> mutate)
    {
        await MutateAsync(userId, db =>
        {
            mutate(db);
            return Task.CompletedTask;
        });
    }

    private async Task MutateAsync(Guid userId, Func<RecordingsDbContext, Task> mutate)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
        await mutate(db);
        await db.SaveChangesAsync();
    }

    private static async Task WaitForAsync(Func<bool> condition, CollectingStream output)
    {
        var deadline = DateTimeOffset.UtcNow + WaitTimeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                Assert.Fail($"Timed out waiting for SSE condition. Stream so far:\n{output.Text}");
            }

            await Task.Delay(25);
        }
    }

    private static List<SseEvent> ParseEvents(string text)
    {
        var events = new List<SseEvent>();
        foreach (var block in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? name = null;
            string? data = null;
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    name = line["event: ".Length..];
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    data = line["data: ".Length..];
                }
            }

            if (name is not null && data is not null)
            {
                events.Add(new SseEvent(name, JsonDocument.Parse(data).RootElement));
            }
        }

        return events;
    }

    private sealed record SseEvent(string Name, JsonElement Data);

    // Thread-safe write-only stream the streaming task flushes into while the test polls Text.
    private sealed class CollectingStream : Stream
    {
        private readonly StringBuilder _builder = new();

        public string Text
        {
            get
            {
                lock (_builder)
                {
                    return _builder.ToString();
                }
            }
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_builder)
            {
                _builder.Append(Encoding.UTF8.GetString(buffer, offset, count));
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
