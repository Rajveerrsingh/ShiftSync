using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftSync.Application.Dtos;
using ShiftSync.Application.Services;

namespace ShiftSync.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;

    public DashboardController(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get([FromQuery] DateOnly? weekStart, CancellationToken ct)
        => Ok(await _dashboard.GetAsync(weekStart, ct));
}
