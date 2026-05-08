using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Application.UseCases.Validation;

namespace ObfusCal.Application.UseCases.GetMergedFreeBusy;

public sealed class GetMergedFreeBusyUseCase(
    ICalendarOwnerAvailabilitySlotStore availabilitySlotStore,
    ICalendarSourceResolver calendarSourceResolver,
    ObfuscationPipeline obfuscationPipeline,
    IShadowSlotStore shadowSlotStore,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService,
    IOptions<SyncOptions> syncOptions,
    ILogger<GetMergedFreeBusyUseCase> logger)
    : IGetMergedFreeBusyUseCase
{
    public async Task<IReadOnlyList<MergedFreeBusyResponse>> ExecuteAsync(GetMergedFreeBusyQuery query, CancellationToken cancellationToken)
    {
        ValidateWindow(query, syncOptions.Value.MaxQueryWindowDays);

        var ownBusySlots = await availabilitySlotStore.GetSlotsAsync(
            query.CalendarOwnerId,
            query.From,
            query.To,
            cancellationToken);

        if (ownBusySlots.Count == 0)
        {
            var calendarSource = await calendarSourceResolver.ResolveAsync(query.CalendarOwnerId, cancellationToken);
            var events = await calendarSource.GetEventsAsync(
                query.From,
                query.To,
                query.CalendarOwnerId,
                cancellationToken);
            var profile = await obfuscationProfileService.GetProfileAsync(
                query.CalendarOwnerId,
                ObfuscationAuditContext.Internal,
                cancellationToken);
            ownBusySlots = obfuscationPipeline.Process(
                events,
                query.CalendarOwnerId.ToString(),
                ObfuscationAuditContext.Internal,
                profile);
        }

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

    private static void ValidateWindow(GetMergedFreeBusyQuery query, int configuredMaxWindowDays)
    {
        if (query.To <= query.From)
            throw new RequestValidationException("to", "'to' must be greater than 'from'.");

        var maxWindowDays = Math.Max(1, configuredMaxWindowDays);
        if (query.To - query.From > TimeSpan.FromDays(maxWindowDays))
        {
            throw new RequestValidationException(
                "to",
                $"The requested window exceeds the maximum of {maxWindowDays} day(s).");
        }
    }
}


