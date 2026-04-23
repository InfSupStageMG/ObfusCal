using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using ObfusCal.Core;
using ObfusCal.Core.Interfaces;
using Serilog;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Route("api/calendar-owners")]
public class CalendarOwnersController(
    ICalendarSource calendarSource,
    ObfuscationPipeline obfuscationPipeline,
    IShadowSlotStore shadowSlotStore)
    : ControllerBase
{
    private const string CalendarOwnerIdProperty = "CalendarOwnerId";

    [HttpGet("{id}/busy-slots")]
    public async Task<IActionResult> GetBusySlots(string id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            Log.ForContext(CalendarOwnerIdProperty, id)
                .Warning("Rejected busy-slot request because required query parameters are missing");
            return BadRequest("Query parameters 'from' and 'to' are required.");
        }

        if (!DateTimeOffset.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fromDate)
            || !DateTimeOffset.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var toDate))
        {
            Log.ForContext(CalendarOwnerIdProperty, id)
                .Warning("Rejected busy-slot request because query parameters are invalid date-time values");
            return BadRequest("Query parameters 'from' and 'to' must be valid date-time strings.");
        }

        var events = await calendarSource.GetEventsAsync(fromDate, toDate, ct);
        var busySlots = obfuscationPipeline.Process(events);

        Log.ForContext(CalendarOwnerIdProperty, id)
            .ForContext("BusySlotCount", busySlots.Count)
            .ForContext("From", fromDate)
            .ForContext("To", toDate)
            .Information("Returning obfuscated busy slots");

        var result = busySlots.Select(bs => new { start = bs.Start, end = bs.End }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}/merged-freebusy")]
    public async Task<IActionResult> GetMergedFreeBusy(string id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            Log.ForContext(CalendarOwnerIdProperty, id)
                .Warning("Rejected merged-freebusy request because required query parameters are missing");
            return BadRequest("Query parameters 'from' and 'to' are required.");
        }

        if (!DateTimeOffset.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fromDate)
            || !DateTimeOffset.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var toDate))
        {
            Log.ForContext(CalendarOwnerIdProperty, id)
                .Warning("Rejected merged-freebusy request because query parameters are invalid date-time values");
            return BadRequest("Query parameters 'from' and 'to' must be valid date-time strings.");
        }

        // Get own obfuscated busy slots
        var events = await calendarSource.GetEventsAsync(fromDate, toDate, ct);
        var ownBusySlots = obfuscationPipeline.Process(events);

        // Get shadow slots from all peers
        var shadowSlots = await shadowSlotStore.GetAllSlotsAsync(fromDate, toDate, ct);

        // Combine into a single list
        var mergedSlots = ownBusySlots.Concat(shadowSlots).OrderBy(s => s.Start).ToList();

        Log.ForContext(CalendarOwnerIdProperty, id)
            .ForContext("OwnBusySlotCount", ownBusySlots.Count)
            .ForContext("ShadowSlotCount", shadowSlots.Count)
            .ForContext("MergedSlotCount", mergedSlots.Count)
            .ForContext("From", fromDate)
            .ForContext("To", toDate)
            .Information("Returning merged free/busy view");

        var result = mergedSlots.Select(s => new { start = s.Start, end = s.End }).ToList();

        return Ok(result);
    }
}

