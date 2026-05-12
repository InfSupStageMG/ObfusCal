using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Authorization;

public sealed class CurrentUserContextAccessor(
    AuthenticationStateProvider authenticationStateProvider,
    ICalendarOwnerProvisioningService calendarOwnerProvisioningService)
{
    public async Task<CurrentUserContext> GetCurrentAsync(CancellationToken ct = default)
    {
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authenticationState.User;

        if (user.Identity?.IsAuthenticated != true)
            return CurrentUserContext.Anonymous;

        var entraObjectId = user.GetObjectId();
        var isSysadmin = AppAuthorizationPolicies.HasSysadminRole(user);
        var displayName = user.GetPreferredDisplayName();

        if (string.IsNullOrWhiteSpace(entraObjectId))
            return new CurrentUserContext(true, isSysadmin, null, null, null, displayName);

        var scope = await calendarOwnerProvisioningService.EnsureForEntraUserAsync(entraObjectId, displayName, ct);
        return new CurrentUserContext(
            true,
            isSysadmin,
            entraObjectId,
            scope?.CalendarOwnerId,
            scope?.Name,
            displayName);
    }
}

public sealed record CurrentUserContext(
    bool IsAuthenticated,
    bool IsSysadmin,
    string? EntraObjectId,
    Guid? CalendarOwnerId,
    string? CalendarOwnerName,
    string? DisplayName)
{
    public static CurrentUserContext Anonymous { get; } = new(false, false, null, null, null, null);
}

