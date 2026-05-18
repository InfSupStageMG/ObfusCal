using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Plugins.GoogleCalendar;

[CalendarSourcePlugin("google", "Google Calendar")]
[CalendarSourcePluginUi(
    supportsMultipleInstances: true,
    configurationJsonTemplate: "{\"calendarId\":\"primary\"}",
    setupHint: "Use the Google consent flow to populate tokens for each source instance.")]
[CalendarSourcePluginAction(
    "google-instance-consent",
    "Start Google OAuth",
    hint: "Authorizes ObfusCal to read your Google Calendar for this source instance.")]
public sealed class GoogleCalendarSourcePlugin(GoogleCalendarSourceCore sourceCore)
    : ICalendarSource, ICalendarSourceReadinessEvaluator, ICalendarSourceInstanceHandler, ICalendarSourceInstanceReadinessEvaluator, ICalendarSourceInstanceWriteBack
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
        GoogleCalendarSourceCore.GetReadinessAsync(instance, ct);

    public Task WriteBackSlotsAsync(
        CalendarSourceInstanceContext instance,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default) =>
        sourceCore.WriteBackSlotsAsync(instance, busySlots, placeholderTitle, windowStart, windowEnd, ct);
}

