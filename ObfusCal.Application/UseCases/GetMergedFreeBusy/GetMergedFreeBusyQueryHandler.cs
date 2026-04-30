using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Application.UseCases.GetMergedFreeBusy;

public sealed class GetMergedFreeBusyUseCase(
    ICalendarSource calendarSource,
    ObfuscationPipeline obfuscationPipeline,
    IShadowSlotStore shadowSlotStore,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService,
    ILogger<GetMergedFreeBusyUseCase> logger)
    : IGetMergedFreeBusyUseCase
{
    public async Task<IReadOnlyList<MergedFreeBusyResponse>> ExecuteAsync(GetMergedFreeBusyQuery query, CancellationToken cancellationToken)
    {
        // Get own obfuscated busy slots
        var events = await calendarSource.GetEventsAsync(
            query.From,
            query.To,
            query.CalendarOwnerId, cancellationToken);
        var profile = await obfuscationProfileService.GetProfileAsync(
            query.CalendarOwnerId,
            ObfuscationAuditContext.Internal,
            cancellationToken);
        var ownBusySlots = obfuscationPipeline.Process(
            events,
            query.CalendarOwnerId.ToString(),
            ObfuscationAuditContext.Internal,
            profile);

        // Get shadow slots from all peers
        var shadowSlots = await shadowSlotStore.GetAllSlotsAsync(
            query.CalendarOwnerId,
            query.From,
            query.To,
            cancellationToken);

        // Combine into a single sorted list
        var mergedSlots = ownBusySlots
            .Concat(shadowSlots)
            .OrderBy(s => s.Start)
            .Select(s => new MergedFreeBusyResponse(
                s.Start,
                s.End,
                s.Title,
                s.Description,
                s.AttendeeEmails,
                s.Location))
            .ToList();

        logger.LogInformation(
            "Returning merged free/busy view for calendar owner {CalendarOwnerId}: " +
            "{OwnBusySlotCount} own + {ShadowSlotCount} shadow = {MergedSlotCount} total",
            query.CalendarOwnerId,
            ownBusySlots.Count,
            shadowSlots.Count,
            mergedSlots.Count);

        return mergedSlots;
    }
}


