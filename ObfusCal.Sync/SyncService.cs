using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Core.Configuration;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Obfuscation;

namespace ObfusCal.Sync;

public class SyncService(
    IServiceScopeFactory scopeFactory,
    IShadowSlotStore store,
    IPeerClient peerClient,
    IOptions<SyncOptions> opts,
    ILogger<SyncService> logger) : BackgroundService
{
    private readonly SyncOptions _opts = opts.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Sync started for instance {Id}", _opts.InstanceId);
        while (!ct.IsCancellationRequested)
        {
            await SyncAllPeersAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(_opts.SyncIntervalSeconds), ct);
        }
    }

    public async Task SyncAllPeersAsync(CancellationToken ct = default)
    {
        var from = DateTimeOffset.UtcNow;
        var to   = from.AddDays(_opts.LookAheadDays);

        // Create a scope for this sync cycle so scoped services resolve correctly
        await using var scope = scopeFactory.CreateAsyncScope();
        var calendarSource = scope.ServiceProvider.GetRequiredService<ICalendarSource>();
        var pipeline       = scope.ServiceProvider.GetRequiredService<ObfuscationPipeline>();

        var rawEvents = await calendarSource.GetEventsAsync(from, to, ct);
        var ourSlots  = pipeline.Process(rawEvents);

        foreach (var peer in _opts.Peers)
        {
            try
            {
                await peerClient.PushSlotsAsync(peer, ourSlots, ct);

                var peerSlots = await peerClient.PullSlotsAsync(peer, from, to, ct);
                await store.SaveAsync(peer.Id, peerSlots, ct);

                logger.LogInformation("Synced with {PeerId}: pushed {Out}, received {In}",
                    peer.Id, ourSlots.Count, peerSlots.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sync failed for peer {PeerId}", peer.Id);
            }
        }
    }
}