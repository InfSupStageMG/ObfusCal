using MediatR;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Application.UseCases.GetBusySlots;

internal sealed class GetBusySlotsQueryHandler(
    ICalendarSource calendarSource,
    ObfuscationPipeline obfuscationPipeline,
    ILogger<GetBusySlotsQueryHandler> logger)
    : IRequestHandler<GetBusySlotsQuery, IReadOnlyList<BusySlotResponse>>
{
    public async Task<IReadOnlyList<BusySlotResponse>> Handle(GetBusySlotsQuery query, CancellationToken ct)
    {
        var events = await calendarSource.GetEventsAsync(query.From, query.To, ct);
        var busySlots = obfuscationPipeline.Process(events, query.CalendarOwnerId, ObfuscationAuditContext.Client);

        logger.LogInformation(
            "Returning {BusySlotCount} obfuscated busy slots for calendar owner {CalendarOwnerId}",
            busySlots.Count,
            query.CalendarOwnerId);

        return busySlots
            .Select(s => new BusySlotResponse(s.Start, s.End))
            .ToList();
    }
}

