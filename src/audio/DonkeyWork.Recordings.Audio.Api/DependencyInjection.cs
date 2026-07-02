using System.Threading.Channels;
using DonkeyWork.Recordings.Audio.Contracts.Messages;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.BackgroundServices;
using DonkeyWork.Recordings.Audio.Core.Options;
using DonkeyWork.Recordings.Audio.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddAudioApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TtsOptions>(configuration.GetSection(TtsOptions.SectionName));
        services.Configure<KokoroOptions>(configuration.GetSection(KokoroOptions.SectionName));
        services.Configure<RecordingsOptions>(configuration.GetSection(RecordingsOptions.SectionName));

        services.AddSingleton<ITtsChunker, TtsChunker>();
        services.AddSingleton<ISsmlPreprocessor, SsmlPreprocessor>();

        services.AddSingleton(_ => Channel.CreateUnbounded<GenerateAudioRecordingCommand>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }));
        services.AddSingleton<IAudioGenerationDispatcher, InMemoryAudioGenerationDispatcher>();
        services.AddHostedService<AudioGenerationWorker>();

        services.AddScoped<IAudioGenerationService, AudioGenerationService>();
        services.AddScoped<ITtsService, TtsService>();
        services.AddScoped<IAudioCollectionService, AudioCollectionService>();
        services.AddScoped<IBacklogService, BacklogService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<IFeedSettingsService, FeedSettingsService>();

        services.AddMemoryCache();

        services.AddHttpClient(KokoroTtsProvider.ProviderKey, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<KokoroOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = opts.RequestTimeout;
        });

        services.AddSingleton<ITtsProvider, KokoroTtsProvider>();

        services.AddControllers().AddApplicationPart(typeof(DependencyInjection).Assembly);

        return services;
    }
}
