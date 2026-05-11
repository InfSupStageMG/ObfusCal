using Microsoft.AspNetCore.Authentication;
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

    public static void Configure(RateLimiterOptions options, SyncOptions syncOptions)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            CreatePartition(context, syncOptions.PeerRequestRateLimitPermitLimit, syncOptions.PeerRequestRateLimitWindowSeconds));

        options.AddPolicy(PushShadowSlotsPolicyName, context =>
            CreatePartition(context, syncOptions.PushShadowSlotsRateLimitPermitLimit, syncOptions.PushShadowSlotsRateLimitWindowSeconds));

        options.AddPolicy(PullBusySlotsPolicyName, context =>
            CreatePartition(context, syncOptions.PullBusySlotsRateLimitPermitLimit, syncOptions.PullBusySlotsRateLimitWindowSeconds));

        options.OnRejected = RateLimitRejectionHandler.HandleRejectedAsync;
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

        var subject = ResolvePeerSubject(context);

        if (!RateLimitStore.TryAcquirePermit(instanceScope, ApiBackstopScope, subject,
                syncOptions.PeerRequestRateLimitPermitLimit, syncOptions.PeerRequestRateLimitWindowSeconds,
                out var retryAfterSeconds))
        {
            await RateLimitRejectionHandler.RejectAsync(context, subject, retryAfterSeconds, context.RequestAborted);
            return true;
        }

        if (requestPath.StartsWith("/api/shadow-slots", StringComparison.OrdinalIgnoreCase)
            && !RateLimitStore.TryAcquirePermit(instanceScope, PushScope, subject,
                syncOptions.PushShadowSlotsRateLimitPermitLimit, syncOptions.PushShadowSlotsRateLimitWindowSeconds,
                out retryAfterSeconds))
        {
            await RateLimitRejectionHandler.RejectAsync(context, subject, retryAfterSeconds, context.RequestAborted);
            return true;
        }

        if (requestPath.StartsWith("/api/sync/busy-slots", StringComparison.OrdinalIgnoreCase)
            && !RateLimitStore.TryAcquirePermit(instanceScope, PullScope, subject,
                syncOptions.PullBusySlotsRateLimitPermitLimit, syncOptions.PullBusySlotsRateLimitWindowSeconds,
                out retryAfterSeconds))
        {
            await RateLimitRejectionHandler.RejectAsync(context, subject, retryAfterSeconds, context.RequestAborted);
            return true;
        }

        return false;
    }

    internal static string ResolvePeerSubject(HttpContext context)
    {
        return TryGetPeerInstanceId(context, out var peerId)
            ? $"peer:{peerId}"
            : $"ip:{GetClientAddress(context)}";
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
        if (context.Items.TryGetValue(PeerInstanceIdItemKey, out var value) && value is string stringValue &&
            !string.IsNullOrWhiteSpace(stringValue))
        {
            peerId = stringValue;
            return true;
        }

        peerId = string.Empty;
        return false;
    }

    private static string GetClientAddress(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";

    private static bool ShouldAttemptPeerAuthentication(PathString path)
    {
        var requestPath = path.Value ?? string.Empty;
        return requestPath.StartsWith("/api/shadow-slots", StringComparison.OrdinalIgnoreCase)
               || requestPath.StartsWith("/api/sync/busy-slots", StringComparison.OrdinalIgnoreCase);
    }
}
