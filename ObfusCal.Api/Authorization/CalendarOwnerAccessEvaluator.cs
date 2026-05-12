using System.Security.Claims;
using Microsoft.Identity.Web;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Authorization;

public sealed class CalendarOwnerAccessEvaluator(ICalendarOwnerProvisioningService provisioningService)
{
    public async Task<CalendarOwnerAccessResult> EvaluateAsync(
        ClaimsPrincipal user,
        Guid requestedCalendarOwnerId,
        CancellationToken ct = default)
    {
        var entraObjectId = user.GetObjectId();
        if (string.IsNullOrWhiteSpace(entraObjectId))
            return CalendarOwnerAccessResult.Unauthorized();

        var scope = await provisioningService.EnsureForEntraUserAsync(entraObjectId, user.GetPreferredDisplayName(), ct);

        return scope.CalendarOwnerId != requestedCalendarOwnerId ? CalendarOwnerAccessResult.Forbidden(scope.CalendarOwnerId) : CalendarOwnerAccessResult.Allowed(scope.CalendarOwnerId);
    }
}

public sealed record CalendarOwnerAccessResult(
    CalendarOwnerAccessStatus Status,
    Guid? CalendarOwnerId = null)
{
    public static CalendarOwnerAccessResult Allowed(Guid calendarOwnerId) =>
        new(CalendarOwnerAccessStatus.Allowed, calendarOwnerId);

    public static CalendarOwnerAccessResult Forbidden(Guid calendarOwnerId) =>
        new(CalendarOwnerAccessStatus.Forbidden, calendarOwnerId);

    public static CalendarOwnerAccessResult NotFound() =>
        new(CalendarOwnerAccessStatus.NotFound);

    public static CalendarOwnerAccessResult Unauthorized() =>
        new(CalendarOwnerAccessStatus.Unauthorized);
}

public enum CalendarOwnerAccessStatus
{
    Allowed,
    Forbidden,
    NotFound,
    Unauthorized
}

