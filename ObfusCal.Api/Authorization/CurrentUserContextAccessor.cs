using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Authorization;

public sealed class CurrentUserContextAccessor(
    AuthenticationStateProvider authenticationStateProvider,
    ICalendarOwnerProvisioningService calendarOwnerProvisioningService)
{
    private readonly SemaphoreSlim _resolutionLock = new(1, 1);
    private CurrentUserContext? _cachedContext;

    public async Task<CurrentUserContext> GetCurrentAsync(CancellationToken ct = default)
    {
        if (_cachedContext is not null)
            return _cachedContext;

        await _resolutionLock.WaitAsync(ct);
        try
        {
            if (_cachedContext is not null)
                return _cachedContext;

            _cachedContext = await ResolveCurrentAsync(ct);
            return _cachedContext;
        }
        finally
        {
            _resolutionLock.Release();
        }
    }

    private async Task<CurrentUserContext> ResolveCurrentAsync(CancellationToken ct)
    {
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authenticationState.User;

        if (user.Identity?.IsAuthenticated != true)
            return CurrentUserContext.Anonymous;

        var entraObjectId = user.GetObjectId();
        var isSysadmin = AppAuthorizationPolicies.HasSysadminRole(user);
        var displayName = user.GetPreferredDisplayName();

        if (string.IsNullOrWhiteSpace(entraObjectId) || isSysadmin)
            return new CurrentUserContext(true, isSysadmin, null, null, null, displayName);

        var scope = await calendarOwnerProvisioningService.EnsureForEntraUserAsync(entraObjectId, displayName, ct);
        return new CurrentUserContext(
            true,
            isSysadmin,
            entraObjectId,
            scope.CalendarOwnerId,
            scope.Name,
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

