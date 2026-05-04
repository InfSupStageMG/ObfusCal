using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/status")]
public sealed class StatusController(IStatusService statusService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarOwnerStatusEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var status = await statusService.GetStatusAsync(ct);
        return Ok(status);
    }
}

