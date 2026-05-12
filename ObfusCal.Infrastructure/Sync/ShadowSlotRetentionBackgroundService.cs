using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Sync;

/// <summary>
/// Background service that periodically purges shadow slot rows (busy slots received from peers)
/// that are older than the configured <see cref="SyncOptions.ShadowSlotRetentionDays"/> window.
///
/// The service runs once every 24 hours. A value of 0 for <c>ShadowSlotRetentionDays</c> disables
/// automatic purging. Sync metadata (<c>LastSyncedAt</c>, <c>LastSyncSucceeded</c>) lives directly
/// on <see cref="PeerConnection"/> and is cascade-deleted when the connection is removed.
/// </summary>
public sealed class ShadowSlotRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> syncOptions,
    ILogger<ShadowSlotRetentionBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger the first run so it doesn't race application startup.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var retentionDays = syncOptions.Value.ShadowSlotRetentionDays;

            if (retentionDays > 0)
            {
                await PurgeExpiredSlotsAsync(retentionDays, stoppingToken);
            }
            else
            {
                logger.LogInformation(
                    "Shadow slot retention purge is disabled (ShadowSlotRetentionDays = 0). Skipping.");
            }

            try
            {
                await Task.Delay(PurgeInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task PurgeExpiredSlotsAsync(int retentionDays, CancellationToken ct)
    {
        try
        {
            var threshold = DateTimeOffset.UtcNow.AddDays(-retentionDays);

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var deleted = await db.BusySlots
                .Where(b => b.CreatedAtUtc < threshold)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                logger.LogInformation(
                    "Shadow slot retention: purged {DeletedCount} expired rows older than {Threshold:O} (retention = {RetentionDays} days).",
                    deleted, threshold, retentionDays);
            }
            else
            {
                logger.LogDebug(
                    "Shadow slot retention: no expired rows found (threshold = {Threshold:O}, retention = {RetentionDays} days).",
                    threshold, retentionDays);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex,
                "Shadow slot retention purge failed; will retry in {IntervalHours} hours.",
                (int)PurgeInterval.TotalHours);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "Shadow slot retention purge failed; will retry in {IntervalHours} hours.",
                (int)PurgeInterval.TotalHours);
        }
    }
}

