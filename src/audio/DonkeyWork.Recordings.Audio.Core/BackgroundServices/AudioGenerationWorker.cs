using System.Threading.Channels;
using DonkeyWork.Recordings.Audio.Contracts.Messages;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Handlers;
using DonkeyWork.Recordings.Audio.Core.Options;
using DonkeyWork.Recordings.Identity.Contracts.Services;
using DonkeyWork.Recordings.Persistence;
using DonkeyWork.Recordings.Storage.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.BackgroundServices;

public sealed class AudioGenerationWorker : BackgroundService
{
    private readonly Channel<GenerateAudioRecordingCommand> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AudioGenerationWorker> _logger;

    public AudioGenerationWorker(
        Channel<GenerateAudioRecordingCommand> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<AudioGenerationWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var command in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await ProcessAsync(command, scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled error processing GenerateAudioRecordingCommand for {RecordingId}",
                    command.RecordingId);
            }
        }
    }

    private static Task ProcessAsync(
        GenerateAudioRecordingCommand command,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        return GenerateAudioRecordingHandler.Handle(
            command,
            services.GetRequiredService<RecordingsDbContext>(),
            services.GetRequiredService<IIdentityContext>(),
            services.GetRequiredService<IGptOssPreprocessor>(),
            services.GetRequiredService<ISsmlPreprocessor>(),
            services.GetRequiredService<ITtsChunker>(),
            services.GetRequiredService<ITtsProviderRegistry>(),
            services.GetRequiredService<IStorageService>(),
            services.GetRequiredService<ILogger<GenerateAudioRecordingCommand>>(),
            cancellationToken);
    }
}
