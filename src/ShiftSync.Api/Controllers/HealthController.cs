using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftSync.Infrastructure.Persistence;

namespace ShiftSync.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var connected = await _db.Database.CanConnectAsync(ct);
        return Ok(new { status = connected ? "ok" : "down" });
    }
}
