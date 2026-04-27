namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerScopeResolver
{
    Task<CalendarOwnerScope?> FindByEntraObjectIdAsync(string entraObjectId, CancellationToken ct = default);
}

public sealed record CalendarOwnerScope(Guid CalendarOwnerId, string EntraObjectId, string Name);

