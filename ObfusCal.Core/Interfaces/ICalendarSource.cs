using ObfusCal.Core.Models;

namespace ObfusCal.Core.Interfaces;

public interface ICalendarSource
{
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}