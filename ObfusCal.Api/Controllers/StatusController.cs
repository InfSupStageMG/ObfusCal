using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ObfusCal.Core.Configuration;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Route("api/status")]
public class StatusController(IOptions<SyncOptions> opts) : ControllerBase
{
    [HttpGet]
    public object Get() => new
    {
        opts.Value.InstanceId,
        Peers = opts.Value.Peers.Select(p => p.Id),
        Time  = DateTimeOffset.UtcNow
    };
}