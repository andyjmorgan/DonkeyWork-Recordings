using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.BackgroundServices;

// Periodically removes chunk clips (storage objects + DB rows) once their recording has settled
// and the grace period has passed. Chunks are a progressive-playback aid, not durable data.
public sealed class ChunkSweepWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TtsOptions _options;
    private readonly ILogger<ChunkSweepWorker> _logger;

    public ChunkSweepWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<TtsOptions> options,
        ILogger<ChunkSweepWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.ChunkSweepInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sweeper = scope.ServiceProvider.GetRequiredService<IChunkSweeper>();
                    await sweeper.SweepOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Chunk sweep pass failed; will retry on the next tick");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
    }
}
