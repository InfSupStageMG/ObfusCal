using System.Security.Claims;

namespace ObfusCal.Api.Authorization;

public static class AppRoles
{
    public const string Sysadmin = "Sysadmin";
}

public static class AppAuthorizationPolicies
{
    public const string Sysadmin = nameof(Sysadmin);

    public static bool HasSysadminRole(ClaimsPrincipal user)
    {
        return user.Claims.Any(claim =>
            claim.Type is ClaimTypes.Role or "roles" or "role" &&
            string.Equals(claim.Value, AppRoles.Sysadmin, StringComparison.Ordinal));
    }
}

