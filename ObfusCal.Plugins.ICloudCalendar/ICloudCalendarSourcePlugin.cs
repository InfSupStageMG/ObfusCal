using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Plugins.ICloudCalendar;

[CalendarSourcePlugin("icloud", "iCloud Calendar")]
public sealed class ICloudCalendarSourcePlugin(ICloudCalendarSourceCore sourceCore)
    : ICalendarSource, ICalendarSourceReadinessEvaluator, ICalendarSourceInstanceHandler, ICalendarSourceInstanceReadinessEvaluator
{
    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? calendarOwnerId = null,
        CancellationToken ct = default) =>
        sourceCore.GetEventsAsync(from, to, calendarOwnerId, ct);

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarSourceInstanceContext instance,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default) =>
        sourceCore.GetEventsAsync(instance, from, to, ct);

    public Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default) =>
        sourceCore.GetReadinessAsync(calendarOwnerId, ct);

    public Task<CalendarSourceReadiness> GetReadinessAsync(CalendarSourceInstanceContext instance, CancellationToken ct = default) =>
        sourceCore.GetReadinessAsync(instance, ct);
}

