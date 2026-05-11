using System.Globalization;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObfusCal.Api.Authentication;
using ObfusCal.Application.Configuration;
using System.Threading.RateLimiting;

namespace ObfusCal.Api.RateLimiting;

internal static class PeerRateLimiting
{
    public const string PushShadowSlotsPolicyName = "PeerPushShadowSlots";
    public const string PullBusySlotsPolicyName = "PeerPullBusySlots";
    private const string PeerInstanceIdItemKey = "PeerRateLimiting.PeerInstanceId";
    private const string ApiBackstopScope = "api-backstop";
    private const string PushScope = "push-shadow-slots";
    private const string PullScope = "pull-busy-slots";

    private static readonly ConcurrentDictionary<string, FixedWindowBucket> Buckets = new();

    public static void Configure(RateLimiterOptions options, SyncOptions syncOptions)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            CreatePartition(context, syncOptions.PeerRequestRateLimitPermitLimit, syncOptions.PeerRequestRateLimitWindowSeconds));

        options.AddPolicy(PushShadowSlotsPolicyName, context =>
            CreatePartition(context, syncOptions.PushShadowSlotsRateLimitPermitLimit, syncOptions.PushShadowSlotsRateLimitWindowSeconds));

        options.AddPolicy(PullBusySlotsPolicyName, context =>
            CreatePartition(context, syncOptions.PullBusySlotsRateLimitPermitLimit, syncOptions.PullBusySlotsRateLimitWindowSeconds));

        options.OnRejected = async (context, cancellationToken) =>
        {
            var httpContext = context.HttpContext;
            var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting");
            var subject = TryGetPeerInstanceId(httpContext, out var peerId)
                ? $"peer:{peerId}"
                : $"ip:{GetClientAddress(httpContext)}";
            var retryAfterSeconds = GetRetryAfterSeconds(context.Lease);

            if (retryAfterSeconds > 0)
                httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            logger.LogWarning(
                "Rate limit exceeded for {Subject} on {RequestMethod} {RequestPath}",
                subject,
                httpContext.Request.Method,
                httpContext.Request.Path.Value ?? string.Empty);

            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests."
            }, cancellationToken);
        };
    }

    public static async Task CapturePeerIdentityForRateLimitingAsync(HttpContext context)
    {
        if (!ShouldAttemptPeerAuthentication(context.Request.Path))
            return;

        if (!context.Request.Headers.Authorization.ToString().StartsWith("ApiKey", StringComparison.OrdinalIgnoreCase))
            return;

        var authenticationService = context.RequestServices.GetRequiredService<IAuthenticationService>();
        var result = await authenticationService.AuthenticateAsync(context, PeerApiKeyAuthenticationDefaults.SchemeName);
        if (!result.Succeeded || result.Principal is null)
            return;

        var peerId = result.Principal.FindFirst(PeerApiKeyClaimTypes.PeerInstanceId)?.Value;
        if (!string.IsNullOrWhiteSpace(peerId))
            context.Items[PeerInstanceIdItemKey] = peerId;
    }

    public static async Task<bool> TryEnforceApiRequestRateLimitsAsync(HttpContext context, SyncOptions syncOptions)
    {
        var requestPath = context.Request.Path.Value ?? string.Empty;
        var instanceScope = string.IsNullOrWhiteSpace(syncOptions.InstanceId)
            ? "default"
            : syncOptions.InstanceId.Trim().ToLowerInvariant();

        if (!requestPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        var subject = TryGetPeerInstanceId(context, out var peerId)
            ? $"peer:{peerId}"
            : $"ip:{GetClientAddress(context)}";

        if (!TryAcquirePermit(instanceScope, ApiBackstopScope, subject, syncOptions.PeerRequestRateLimitPermitLimit, syncOptions.PeerRequestRateLimitWindowSeconds, out var retryAfterSeconds))
        {
            await RejectAsync(context, subject, retryAfterSeconds, cancellationToken: context.RequestAborted);
            return true;
        }

        if (requestPath.StartsWith("/api/shadow-slots", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryAcquirePermit(instanceScope, PushScope, subject, syncOptions.PushShadowSlotsRateLimitPermitLimit, syncOptions.PushShadowSlotsRateLimitWindowSeconds, out retryAfterSeconds))
            {
                await RejectAsync(context, subject, retryAfterSeconds, cancellationToken: context.RequestAborted);
                return true;
            }
        }
        else if (requestPath.StartsWith("/api/sync/busy-slots", StringComparison.OrdinalIgnoreCase)
            && !TryAcquirePermit(instanceScope, PullScope, subject, syncOptions.PullBusySlotsRateLimitPermitLimit, syncOptions.PullBusySlotsRateLimitWindowSeconds, out retryAfterSeconds))
        {
            await RejectAsync(context, subject, retryAfterSeconds, cancellationToken: context.RequestAborted);
            return true;
        }

        return false;
    }

    private static RateLimitPartition<string> CreatePartition(HttpContext context, int permitLimit, int windowSeconds)
    {
        var partitionKey = GetPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, permitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    private static string GetPartitionKey(HttpContext context)
    {
        if (TryGetPeerInstanceId(context, out var peerId))
            return $"peer:{peerId}";

        return $"ip:{GetClientAddress(context)}";
    }

    private static bool TryGetPeerInstanceId(HttpContext context, out string peerId)
    {
        if (context.Items.TryGetValue(PeerInstanceIdItemKey, out var value) && value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
        {
            peerId = stringValue;
            return true;
        }

        peerId = string.Empty;
        return false;
    }

    private static string GetClientAddress(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";

    private static int GetRetryAfterSeconds(RateLimitLease lease)
    {
        if (!lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
            return 0;

        return (int)Math.Ceiling(retryAfter.TotalSeconds);
    }

    private static bool ShouldAttemptPeerAuthentication(PathString path)
    {
        var requestPath = path.Value ?? string.Empty;
        return requestPath.StartsWith("/api/shadow-slots", StringComparison.OrdinalIgnoreCase)
               || requestPath.StartsWith("/api/sync/busy-slots", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryAcquirePermit(string instanceScope, string scope, string subject, int permitLimit, int windowSeconds, out int retryAfterSeconds)
    {
        var key = $"{instanceScope}:{scope}:{subject}";
        var bucket = Buckets.GetOrAdd(key, _ => new FixedWindowBucket());
        var window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds));
        var limit = Math.Max(1, permitLimit);

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
                return true;
            }

            retryAfterSeconds = (int)Math.Ceiling(Math.Max(1, (window - (now - bucket.WindowStartedAt)).TotalSeconds));
            return false;
        }
    }

    private static async Task RejectAsync(HttpContext context, string subject, int retryAfterSeconds, CancellationToken cancellationToken)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting");

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        logger.LogWarning(
            "Rate limit exceeded for {Subject} on {RequestMethod} {RequestPath}",
            subject,
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty);

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests."
        }, cancellationToken);
    }

    private sealed class FixedWindowBucket
    {
        public object Gate { get; } = new();
        public DateTimeOffset WindowStartedAt { get; set; }
        public int Count { get; set; }
    }
}






