using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Application.UseCases.GetBusySlots;

public class GetBusySlotsUseCase(
    ICalendarSource calendarSource,
    ObfuscationPipeline obfuscationPipeline,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService,
    ILogger<GetBusySlotsUseCase> logger)
    : IGetBusySlotsUseCase
{
    public virtual async Task<IReadOnlyList<BusySlotResponse>> ExecuteAsync(GetBusySlotsQuery query, CancellationToken cancellationToken)
    {
        var events = await calendarSource.GetEventsAsync(
            query.From,
            query.To,
            query.CalendarOwnerId, cancellationToken);
        var profile = await obfuscationProfileService.GetProfileAsync(
            query.CalendarOwnerId,
            ObfuscationAuditContext.Client,
            cancellationToken);
        var busySlots = obfuscationPipeline.Process(
            events,
            query.CalendarOwnerId.ToString(),
            ObfuscationAuditContext.Client,
            profile);

        logger.LogInformation(
            "Returning {BusySlotCount} obfuscated busy slots for calendar owner {CalendarOwnerId}",
            busySlots.Count,
            query.CalendarOwnerId);

        return busySlots
            .Select(s => new BusySlotResponse(
                s.Start,
                s.End,
                s.Title,
                s.Description,
                s.AttendeeEmails,
                s.Location))
            .ToList();
    }
}

[Obsolete("Use GetBusySlotsUseCase or IGetBusySlotsUseCase instead.")]
public sealed class GetBusySlotsQueryHandler(
    ICalendarSource calendarSource,
    ObfuscationPipeline obfuscationPipeline,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService,
    ILogger<GetBusySlotsUseCase> logger)
    : GetBusySlotsUseCase(calendarSource, obfuscationPipeline, obfuscationProfileService, logger)
{
    public Task<IReadOnlyList<BusySlotResponse>> Handle(GetBusySlotsQuery query, CancellationToken cancellationToken) =>
        ExecuteAsync(query, cancellationToken);
}

