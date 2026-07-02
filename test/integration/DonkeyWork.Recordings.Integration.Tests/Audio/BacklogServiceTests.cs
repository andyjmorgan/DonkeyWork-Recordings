using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Identity.Core.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Persistence.Entities.Tts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

public class BacklogServiceTests : IClassFixture<RecordingsTestFixture>
{
    private readonly RecordingsTestFixture _fixture;

    public BacklogServiceTests(RecordingsTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<Guid> CreateCollectionAsync(Guid userId, string name = "Daily News")
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
        var collection = new TtsAudioCollectionEntity { UserId = userId, Name = name, Description = "Test channel" };
        db.Collections.Add(collection);
        await db.SaveChangesAsync();
        return collection.Id;
    }

    private async Task<Guid> CreateRecordingAsync(Guid userId, Guid collectionId, string name = "Episode 1")
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        var db = scope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
        var recording = new TtsRecordingEntity
        {
            UserId = userId,
            CollectionId = collectionId,
            Name = name,
            Status = TtsRecordingStatus.Ready,
        };
        db.Recordings.Add(recording);
        await db.SaveChangesAsync();
        return recording.Id;
    }

    private AsyncServiceScope CreateServiceScope(Guid userId, out IBacklogService service)
    {
        var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        service = scope.ServiceProvider.GetRequiredService<IBacklogService>();
        return scope;
    }

    [Fact]
    public async Task Create_List_Update_Delete_Round_Trip()
    {
        var userId = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userId);

        await using var scope = CreateServiceScope(userId, out var service);

        var first = await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Older story", Content = "Body A" });
        var second = await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Newer story", SourceUrl = "https://example.com/a" });

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Pending", first!.Status);

        var pending = await service.ListAsync(collectionId, "Pending", 0, 50);
        Assert.NotNull(pending);
        Assert.Equal(2, pending!.TotalCount);
        Assert.Equal(first.Id, pending.Items[0].Id); // FIFO: oldest first
        Assert.Equal(second!.Id, pending.Items[1].Id);

        var updated = await service.UpdateAsync(first.Id, new UpdateBacklogItemRequestV1 { Title = "Renamed story", Notes = "keep it brief" });
        Assert.NotNull(updated);
        Assert.Equal("Renamed story", updated!.Title);
        Assert.Equal("Body A", updated.Content);
        Assert.Equal("keep it brief", updated.Notes);

        Assert.True(await service.DeleteAsync(second.Id));
        Assert.False(await service.DeleteAsync(second.Id));

        var remaining = await service.ListAsync(collectionId, "all", 0, 50);
        Assert.Equal(1, remaining!.TotalCount);
    }

    [Fact]
    public async Task List_Returns_Null_For_Unknown_Collection()
    {
        var userId = Guid.NewGuid();
        await using var scope = CreateServiceScope(userId, out var service);

        Assert.Null(await service.ListAsync(Guid.NewGuid(), null, 0, 50));
        Assert.Null(await service.CreateAsync(Guid.NewGuid(), new CreateBacklogItemRequestV1 { Title = "Nope" }));
    }

    [Fact]
    public async Task List_Throws_For_Invalid_Status()
    {
        var userId = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userId);
        await using var scope = CreateServiceScope(userId, out var service);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(collectionId, "bogus", 0, 50));
    }

    [Fact]
    public async Task Backlog_Is_User_Scoped()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userA);

        Guid itemId;
        await using (var aScope = CreateServiceScope(userA, out var aService))
        {
            var item = await aService.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "A's secret scoop" });
            itemId = item!.Id;
        }

        await using var bScope = CreateServiceScope(userB, out var bService);
        Assert.Null(await bService.ListAsync(collectionId, null, 0, 50));
        Assert.Null(await bService.GetAsync(itemId));
        Assert.Null(await bService.UpdateAsync(itemId, new UpdateBacklogItemRequestV1 { Title = "hijacked" }));
        Assert.False(await bService.DeleteAsync(itemId));
        Assert.Null(await bService.DismissAsync(itemId));
    }

    [Fact]
    public async Task Consume_Marks_Explicit_Items_And_Links_Recording()
    {
        var userId = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userId);
        var recordingId = await CreateRecordingAsync(userId, collectionId);

        await using var scope = CreateServiceScope(userId, out var service);
        var used = await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Used item" });
        var untouched = await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Still pending" });

        var response = await service.ConsumeAsync(collectionId, new ConsumeBacklogItemsRequestV1
        {
            RecordingId = recordingId,
            ItemIds = [used!.Id],
        });

        Assert.NotNull(response);
        Assert.Equal([used.Id], response!.ConsumedIds);
        Assert.Empty(response.SkippedIds);

        var consumed = await service.GetAsync(used.Id);
        Assert.Equal("Consumed", consumed!.Status);
        Assert.Equal(recordingId, consumed.ConsumedByRecordingId);
        Assert.Equal("Episode 1", consumed.ConsumedByRecordingName);
        Assert.NotNull(consumed.ConsumedAt);

        var stillPending = await service.GetAsync(untouched!.Id);
        Assert.Equal("Pending", stillPending!.Status);
    }

    [Fact]
    public async Task Consume_All_Pending_When_ItemIds_Omitted()
    {
        var userId = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userId);
        var recordingId = await CreateRecordingAsync(userId, collectionId);

        await using var scope = CreateServiceScope(userId, out var service);
        await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "One" });
        await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Two" });

        var response = await service.ConsumeAsync(collectionId, new ConsumeBacklogItemsRequestV1 { RecordingId = recordingId });

        Assert.Equal(2, response!.ConsumedIds.Count);
        Assert.Empty(response.SkippedIds);

        var pending = await service.ListAsync(collectionId, "Pending", 0, 50);
        Assert.Equal(0, pending!.TotalCount);
        var history = await service.ListAsync(collectionId, "Consumed", 0, 50);
        Assert.Equal(2, history!.TotalCount);
    }

    [Fact]
    public async Task Consume_Is_Idempotent_And_Preserves_Original_Link()
    {
        var userId = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userId);
        var firstRecording = await CreateRecordingAsync(userId, collectionId, "Episode 1");
        var secondRecording = await CreateRecordingAsync(userId, collectionId, "Episode 2");

        await using var scope = CreateServiceScope(userId, out var service);
        var item = await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Once only" });

        var firstPass = await service.ConsumeAsync(collectionId, new ConsumeBacklogItemsRequestV1
        {
            RecordingId = firstRecording,
            ItemIds = [item!.Id],
        });
        Assert.Equal([item.Id], firstPass!.ConsumedIds);

        var secondPass = await service.ConsumeAsync(collectionId, new ConsumeBacklogItemsRequestV1
        {
            RecordingId = secondRecording,
            ItemIds = [item.Id],
        });
        Assert.Empty(secondPass!.ConsumedIds);
        Assert.Equal([item.Id], secondPass.SkippedIds);

        var current = await service.GetAsync(item.Id);
        Assert.Equal(firstRecording, current!.ConsumedByRecordingId);
    }

    [Fact]
    public async Task Consume_Returns_Null_For_Unknown_Recording_Or_Collection()
    {
        var userId = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userId);
        var recordingId = await CreateRecordingAsync(userId, collectionId);

        await using var scope = CreateServiceScope(userId, out var service);

        Assert.Null(await service.ConsumeAsync(collectionId, new ConsumeBacklogItemsRequestV1 { RecordingId = Guid.NewGuid() }));
        Assert.Null(await service.ConsumeAsync(Guid.NewGuid(), new ConsumeBacklogItemsRequestV1 { RecordingId = recordingId }));
    }

    [Fact]
    public async Task Dismiss_Only_Transitions_Pending_Items()
    {
        var userId = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userId);
        var recordingId = await CreateRecordingAsync(userId, collectionId);

        await using var scope = CreateServiceScope(userId, out var service);
        var stale = await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Stale" });
        var consumed = await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Already used" });
        await service.ConsumeAsync(collectionId, new ConsumeBacklogItemsRequestV1 { RecordingId = recordingId, ItemIds = [consumed!.Id] });

        var dismissed = await service.DismissAsync(stale!.Id);
        Assert.Equal("Dismissed", dismissed!.Status);

        var again = await service.DismissAsync(stale.Id);
        Assert.Equal("Dismissed", again!.Status);

        var notDismissable = await service.DismissAsync(consumed.Id);
        Assert.Equal("Consumed", notDismissable!.Status);
        Assert.Equal(recordingId, notDismissable.ConsumedByRecordingId);
    }

    [Fact]
    public async Task Channel_Delete_Cascades_Backlog_Items()
    {
        var userId = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userId);

        Guid itemId;
        await using (var scope = CreateServiceScope(userId, out var service))
        {
            var item = await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Doomed" });
            itemId = item!.Id;
        }

        await using (var deleteScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            deleteScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var collectionService = deleteScope.ServiceProvider.GetRequiredService<IAudioCollectionService>();
            Assert.True(await collectionService.DeleteAsync(collectionId));
        }

        await using (var checkScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            checkScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = checkScope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            Assert.False(await db.BacklogItems.AnyAsync(b => b.Id == itemId));
        }
    }

    [Fact]
    public async Task Recording_Delete_Nulls_Link_But_Keeps_History()
    {
        var userId = Guid.NewGuid();
        var collectionId = await CreateCollectionAsync(userId);
        var recordingId = await CreateRecordingAsync(userId, collectionId);

        Guid itemId;
        await using (var scope = CreateServiceScope(userId, out var service))
        {
            var item = await service.CreateAsync(collectionId, new CreateBacklogItemRequestV1 { Title = "Survivor" });
            itemId = item!.Id;
            await service.ConsumeAsync(collectionId, new ConsumeBacklogItemsRequestV1 { RecordingId = recordingId, ItemIds = [itemId] });
        }

        await using (var deleteScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            deleteScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var db = deleteScope.ServiceProvider.GetRequiredService<RecordingsDbContext>();
            var recording = await db.Recordings.SingleAsync(r => r.Id == recordingId);
            db.Recordings.Remove(recording);
            await db.SaveChangesAsync();
        }

        await using (var checkScope = CreateServiceScope(userId, out var service))
        {
            var item = await service.GetAsync(itemId);
            Assert.NotNull(item);
            Assert.Equal("Consumed", item!.Status);
            Assert.Null(item.ConsumedByRecordingId);
            Assert.NotNull(item.ConsumedAt);
        }
    }
}
