using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Identity.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

public class FeedSettingsServiceTests : IClassFixture<RecordingsTestFixture>
{
    private readonly RecordingsTestFixture _fixture;

    public FeedSettingsServiceTests(RecordingsTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task First_Get_Returns_Defaults_With_Computed_Master_Feed_Url()
    {
        var userId = Guid.NewGuid();

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
        var service = scope.ServiceProvider.GetRequiredService<IFeedSettingsService>();

        var settings = await service.GetAsync("https://recordings.donkeywork.dev");

        Assert.Equal("DonkeyWork Recordings", settings.Title);
        Assert.Equal("en-US", settings.Language);
        Assert.False(settings.IsExplicit);
        Assert.Null(settings.Author);
        Assert.Equal($"https://recordings.donkeywork.dev/feeds/{userId}/all.xml", settings.MasterFeedUrl);
    }

    [Fact]
    public async Task Update_Persists_Patch_And_Subsequent_Get_Returns_Same_Values()
    {
        var userId = Guid.NewGuid();

        await using (var writeScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            writeScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = writeScope.ServiceProvider.GetRequiredService<IFeedSettingsService>();

            var updated = await service.UpdateAsync(
                new UpdateFeedSettingsRequestV1
                {
                    Title = "Andy's Podcast",
                    Description = "Tech ramblings and side-project notes.",
                    Author = "Andy Morgan",
                    AuthorEmail = "andy@example.com",
                    Language = "en-IE",
                    Link = "https://andy.example.com",
                    IsExplicit = true,
                    ItunesCategory = "Technology",
                },
                "https://recordings.donkeywork.dev");

            Assert.Equal("Andy's Podcast", updated.Title);
            Assert.Equal("en-IE", updated.Language);
            Assert.True(updated.IsExplicit);
            Assert.Equal("Andy Morgan", updated.Author);
        }

        await using (var readScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            readScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userId);
            var service = readScope.ServiceProvider.GetRequiredService<IFeedSettingsService>();

            var settings = await service.GetAsync("https://recordings.donkeywork.dev");

            Assert.Equal("Andy's Podcast", settings.Title);
            Assert.Equal("Tech ramblings and side-project notes.", settings.Description);
            Assert.Equal("Andy Morgan", settings.Author);
            Assert.Equal("andy@example.com", settings.AuthorEmail);
            Assert.Equal("en-IE", settings.Language);
            Assert.Equal("https://andy.example.com", settings.Link);
            Assert.True(settings.IsExplicit);
            Assert.Equal("Technology", settings.ItunesCategory);
        }
    }

    [Fact]
    public async Task Settings_Are_User_Scoped()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using (var aScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            aScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userA);
            var service = aScope.ServiceProvider.GetRequiredService<IFeedSettingsService>();
            await service.UpdateAsync(
                new UpdateFeedSettingsRequestV1 { Title = "User A's feed" },
                "https://recordings.donkeywork.dev");
        }

        await using (var bScope = _fixture.Factory.Services.CreateAsyncScope())
        {
            bScope.ServiceProvider.GetRequiredService<IdentityContext>().SetIdentity(userB);
            var service = bScope.ServiceProvider.GetRequiredService<IFeedSettingsService>();
            var settings = await service.GetAsync("https://recordings.donkeywork.dev");

            Assert.Equal("DonkeyWork Recordings", settings.Title);
        }
    }
}
