using Serilog;

namespace ObfusCal.Api.RateLimiting;

/// <summary>
/// Background service that periodically evicts expired rate-limit buckets.
/// Runs independently of request processing to keep the hot path clean.
/// </summary>
internal sealed class RateLimitBucketEvictionService : BackgroundService
{
    private readonly TimeSpan _evictionInterval;

    internal RateLimitBucketEvictionService(TimeSpan evictionInterval)
    {
        _evictionInterval = evictionInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_evictionInterval, stoppingToken);
                RateLimitStore.EvictExpiredBuckets();
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("Rate limit bucket eviction service is stopping.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Rate limit bucket eviction service failed.");
            throw;
        }
    }
}


