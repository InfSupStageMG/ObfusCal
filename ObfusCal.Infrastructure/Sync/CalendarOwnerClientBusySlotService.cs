using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;

namespace ObfusCal.Infrastructure.Sync;

public sealed class CalendarOwnerClientBusySlotService(
    ICalendarSourceResolver calendarSourceResolver,
    ObfuscationPipeline obfuscationPipeline,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService) : ICalendarOwnerClientBusySlotService
{
    public async Task<IReadOnlyList<BusySlot>> BuildAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var calendarSource = await calendarSourceResolver.ResolveAsync(calendarOwnerId, ct);
        var events = await calendarSource.GetEventsAsync(from, to, calendarOwnerId, ct);
        var profile = await obfuscationProfileService.GetProfileAsync(
            calendarOwnerId,
            ObfuscationAuditContext.Client,
            ct);

        return obfuscationPipeline.Process(
            events,
            calendarOwnerId.ToString(),
            ObfuscationAuditContext.Client,
            profile);
    }
}

