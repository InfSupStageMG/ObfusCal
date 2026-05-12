using ObfusCal.Application.Configuration;

namespace ObfusCal.Api.RateLimiting;

internal static class ApiRequestRateLimitEnforcer
{
    internal static async Task<bool> TryEnforceAsync(HttpContext context, SyncOptions syncOptions)
    {
        var requestPath = context.Request.Path.Value ?? string.Empty;
        if (!requestPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        var instanceScope = string.IsNullOrWhiteSpace(syncOptions.InstanceId)
            ? "default"
            : syncOptions.InstanceId.Trim().ToLowerInvariant();

        var subject = RateLimitSubjectResolver.Resolve(context);

        if (!TryAcquire(instanceScope, PeerRateLimiting.ApiBackstopScope, subject,
                syncOptions.PeerRequestRateLimitPermitLimit, syncOptions.PeerRequestRateLimitWindowSeconds,
                out var retryAfterSeconds))
        {
            await RateLimitRejectionHandler.RejectAsync(context, subject, retryAfterSeconds, context.RequestAborted);
            return true;
        }

        if (IsPushShadowSlots(requestPath)
            && !TryAcquire(instanceScope, PeerRateLimiting.PushScope, subject,
                syncOptions.PushShadowSlotsRateLimitPermitLimit, syncOptions.PushShadowSlotsRateLimitWindowSeconds,
                out retryAfterSeconds))
        {
            await RateLimitRejectionHandler.RejectAsync(context, subject, retryAfterSeconds, context.RequestAborted);
            return true;
        }

        if (IsPullBusySlots(requestPath)
            && !TryAcquire(instanceScope, PeerRateLimiting.PullScope, subject,
                syncOptions.PullBusySlotsRateLimitPermitLimit, syncOptions.PullBusySlotsRateLimitWindowSeconds,
                out retryAfterSeconds))
        {
            await RateLimitRejectionHandler.RejectAsync(context, subject, retryAfterSeconds, context.RequestAborted);
            return true;
        }

        return false;
    }

    private static bool TryAcquire(string instanceScope, string scope, string subject,
        int permitLimit, int windowSeconds, out int retryAfterSeconds)
        => RateLimitStore.TryAcquirePermit(instanceScope, scope, subject, permitLimit, windowSeconds,
            out retryAfterSeconds);

    private static bool IsPushShadowSlots(string requestPath)
        => requestPath.StartsWith("/api/shadow-slots", StringComparison.OrdinalIgnoreCase);

    private static bool IsPullBusySlots(string requestPath)
        => requestPath.StartsWith("/api/sync/busy-slots", StringComparison.OrdinalIgnoreCase);
}


