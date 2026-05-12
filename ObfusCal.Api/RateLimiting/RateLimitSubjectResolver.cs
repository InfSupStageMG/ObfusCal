namespace ObfusCal.Api.RateLimiting;

internal static class RateLimitSubjectResolver
{
    internal static string Resolve(HttpContext context)
    {
        if (context.Items.TryGetValue(RateLimitingContextKeys.PeerInstanceIdItemKey, out var value)
            && value is string peerId
            && !string.IsNullOrWhiteSpace(peerId))
        {
            return $"peer:{peerId}";
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return $"ip:{ipAddress}";
    }
}

