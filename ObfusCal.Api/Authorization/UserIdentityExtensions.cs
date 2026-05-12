using System.Security.Claims;

namespace ObfusCal.Api.Authorization;

public static class UserIdentityExtensions
{
    public static string GetPreferredDisplayName(this ClaimsPrincipal user, string fallback = "Signed in")
    {
        var displayName = user.FindFirst("name")?.Value;
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        return user.Identity?.Name
               ?? user.FindFirst("preferred_username")?.Value
               ?? user.FindFirst(ClaimTypes.Email)?.Value
               ?? fallback;
    }

    public static string GetIdentityTooltip(this ClaimsPrincipal user)
    {
        return user.Identity?.Name
               ?? user.FindFirst("preferred_username")?.Value
               ?? user.FindFirst(ClaimTypes.Email)?.Value
               ?? user.GetPreferredDisplayName();
    }
}

