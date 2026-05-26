using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Sync;

public sealed class PeerSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    SyncProgressMonitor progressMonitor,
    IOptions<SyncOptions> syncOptions,
    ILogger<PeerSyncBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (progressMonitor.TryBeginPeerSync())
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var outboundSyncService = scope.ServiceProvider.GetRequiredService<IOutboundPeerSyncService>();
                    var inboundSyncService = scope.ServiceProvider.GetRequiredService<IInboundPeerPullSyncService>();

                    await outboundSyncService.RunSyncCycleAsync(stoppingToken);
                    await inboundSyncService.RunSyncCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    progressMonitor.EndPeerSync();
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Scheduled peer sync cycle failed; continuing with next interval.");
                }
                finally
                {
                    progressMonitor.EndPeerSync();
                }
            }
            else
            {
                logger.LogDebug("Scheduled peer sync skipped: a sync cycle is already in progress.");
            }

            var intervalSeconds = Math.Max(1, syncOptions.Value.SyncIntervalSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

