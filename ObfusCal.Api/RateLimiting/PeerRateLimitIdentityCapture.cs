using Microsoft.AspNetCore.Authentication;
using ObfusCal.Api.Authentication;

namespace ObfusCal.Api.RateLimiting;

internal static class PeerRateLimitIdentityCapture
{
    internal static async Task CaptureAsync(HttpContext context)
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
            context.Items[RateLimitingContextKeys.PeerInstanceIdItemKey] = peerId;
    }

    private static bool ShouldAttemptPeerAuthentication(PathString path)
    {
        var requestPath = path.Value ?? string.Empty;
        return requestPath.StartsWith("/api/shadow-slots", StringComparison.OrdinalIgnoreCase)
               || requestPath.StartsWith("/api/sync/busy-slots", StringComparison.OrdinalIgnoreCase);
    }
}

