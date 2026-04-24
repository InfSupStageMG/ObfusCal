using ObfusCal.Domain.Models;

namespace ObfusCal.Application.Interfaces;

public interface ICalendarSource
{
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}

