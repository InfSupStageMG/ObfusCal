using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Plugins.ICloudCalendar;

[CalendarSourcePlugin("icloud", "iCloud Calendar")]
[CalendarSourcePluginUi(
    supportsMultipleInstances: true,
    configurationJsonTemplate: "{\"calendarUrl\":\"https://caldav.icloud.com/.../calendar/\"}",
    secretDataJsonTemplate: "{\"appleId\":\"you@example.com\",\"appSpecificPassword\":\"\"}",
    setupHint: "Generate an app-specific password in your Apple ID settings. The Apple ID and app-specific password are stored encrypted.")]
public sealed class ICloudCalendarSourcePlugin(ICloudCalendarSourceCore sourceCore)
    : ICalendarSource, ICalendarWriteBack, ICalendarSourceReadinessEvaluator, ICalendarSourceInstanceHandler,
        ICalendarSourceInstanceReadinessEvaluator, ICalendarSourceInstanceWriteBack
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

    public Task<CalendarSourceReadiness> GetReadinessAsync(CalendarSourceInstanceContext instance,
        CancellationToken ct = default) =>
        sourceCore.GetReadinessAsync(instance, ct);

    public Task WriteBackSlotsAsync(
        Guid calendarOwnerId,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default) =>
        sourceCore.WriteBackSlotsAsync(calendarOwnerId, busySlots, placeholderTitle, windowStart, windowEnd, ct);

    public Task WriteBackSlotsAsync(
        CalendarSourceInstanceContext instance,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default) =>
        sourceCore.WriteBackSlotsAsync(instance, busySlots, placeholderTitle, windowStart, windowEnd, ct);
}

