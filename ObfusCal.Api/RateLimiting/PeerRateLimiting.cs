using Microsoft.AspNetCore.RateLimiting;
using ObfusCal.Application.Configuration;
using System.Threading.RateLimiting;

namespace ObfusCal.Api.RateLimiting;

internal static class PeerRateLimiting
{
    public const string PushShadowSlotsPolicyName = "PeerPushShadowSlots";
    public const string PullBusySlotsPolicyName = "PeerPullBusySlots";
    internal const string ApiBackstopScope = "api-backstop";
    internal const string PushScope = "push-shadow-slots";
    internal const string PullScope = "pull-busy-slots";

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

    public static Task CapturePeerIdentityForRateLimitingAsync(HttpContext context)
        => PeerRateLimitIdentityCapture.CaptureAsync(context);

    public static Task<bool> TryEnforceApiRequestRateLimitsAsync(HttpContext context, SyncOptions syncOptions)
        => ApiRequestRateLimitEnforcer.TryEnforceAsync(context, syncOptions);

    internal static string ResolvePeerSubject(HttpContext context)
        => RateLimitSubjectResolver.Resolve(context);

    private static RateLimitPartition<string> CreatePartition(HttpContext context, int permitLimit, int windowSeconds)
    {
        var partitionKey = RateLimitSubjectResolver.Resolve(context);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, permitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }
}
