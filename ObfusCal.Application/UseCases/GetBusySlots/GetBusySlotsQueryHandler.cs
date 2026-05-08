using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Application.UseCases.Validation;

namespace ObfusCal.Application.UseCases.GetBusySlots;

public sealed class GetBusySlotsUseCase(
    ICalendarSourceResolver calendarSourceResolver,
    ObfuscationPipeline obfuscationPipeline,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService,
    IOptions<SyncOptions> syncOptions,
    ILogger<GetBusySlotsUseCase> logger)
    : IGetBusySlotsUseCase
{
    public async Task<IReadOnlyList<BusySlotResponse>> ExecuteAsync(GetBusySlotsQuery query, CancellationToken cancellationToken)
    {
        ValidateWindow(query, syncOptions.Value.MaxQueryWindowDays);

        var calendarSource = await calendarSourceResolver.ResolveAsync(query.CalendarOwnerId, cancellationToken);
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

    private static void ValidateWindow(GetBusySlotsQuery query, int configuredMaxWindowDays)
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


