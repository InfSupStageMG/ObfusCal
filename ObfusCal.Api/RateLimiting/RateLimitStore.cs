using System.Collections.Concurrent;

namespace ObfusCal.Api.RateLimiting;

internal static class RateLimitStore
{
    private static readonly ConcurrentDictionary<string, FixedWindowBucket> Buckets = new();
    private static readonly TimeSpan ExpiredBucketThreshold = TimeSpan.FromMinutes(5);

    internal static bool TryAcquirePermit(string instanceScope, string scope, string subject, int permitLimit,
        int windowSeconds, out int retryAfterSeconds)
    {

        var key = $"{instanceScope}:{scope}:{subject}";
        var bucket = Buckets.GetOrAdd(key, _ => new FixedWindowBucket());
        var window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds));
        var limit = Math.Max(1, permitLimit);

        bool result;
        lock (bucket.Gate)
        {
            var now = DateTimeOffset.UtcNow;

            if (bucket.WindowStartedAt == default || now - bucket.WindowStartedAt >= window)
            {
                bucket.WindowStartedAt = now;
                bucket.Count = 0;
            }

            if (bucket.Count < limit)
            {
                bucket.Count++;
                retryAfterSeconds = 0;
                result = true;
            }
            else
            {
                retryAfterSeconds = (int)Math.Ceiling(Math.Max(1,
                    (window - (now - bucket.WindowStartedAt)).TotalSeconds));
                result = false;
            }
        }

        return result;
    }

    internal static void EvictExpiredBuckets()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var kvp in Buckets)
        {
            lock (kvp.Value.Gate)
            {
                if (kvp.Value.WindowStartedAt != default && now - kvp.Value.WindowStartedAt >= ExpiredBucketThreshold)
                    Buckets.TryRemove(kvp.Key, out _);
            }
        }
    }

    internal sealed class FixedWindowBucket
    {
        public object Gate { get; } = new();
        public DateTimeOffset WindowStartedAt { get; set; }
        public int Count { get; set; }
    }
}

