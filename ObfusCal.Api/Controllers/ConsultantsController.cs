using Microsoft.AspNetCore.Mvc;
using ObfusCal.Core;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Route("api/consultants")]
public class ConsultantsController : ControllerBase
{
    private readonly ICalendarSource _calendarSource;
    private readonly ObfuscationPipeline _obfuscationPipeline;

    public ConsultantsController(ICalendarSource calendarSource, ObfuscationPipeline obfuscationPipeline)
    {
        _calendarSource = calendarSource;
        _obfuscationPipeline = obfuscationPipeline;
    }

    [HttpGet("{id}/busy-slots")]
    public async Task<IActionResult> GetBusySlots(string id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return BadRequest("Query parameters 'from' and 'to' are required.");
        }

        if (!DateTimeOffset.TryParse(from, out var fromDate) || !DateTimeOffset.TryParse(to, out var toDate))
        {
            return BadRequest("Query parameters 'from' and 'to' must be valid date-time strings.");
        }

        var events = await _calendarSource.GetEventsAsync(fromDate, toDate, ct);
        var busySlots = _obfuscationPipeline.Process(events);

        var result = busySlots.Select(bs => new { start = bs.Start, end = bs.End }).ToList();

        return Ok(result);
    }
}
