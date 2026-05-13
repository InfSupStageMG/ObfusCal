namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerProvisioningService
{
    Task<CalendarOwnerScope> EnsureForEntraUserAsync(
        string entraObjectId,
        string? displayName,
        CancellationToken ct = default);
}

