using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftSync.Application.Dtos;
using ShiftSync.Application.Services;

namespace ShiftSync.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/schedule")]
public class ScheduleController : ControllerBase
{
    private readonly ScheduleService _schedule;

    public ScheduleController(ScheduleService schedule)
    {
        _schedule = schedule;
    }

    [HttpGet]
    public async Task<ActionResult<WeekScheduleDto>> Get([FromQuery] DateOnly? weekStart, CancellationToken ct)
        => Ok(await _schedule.GetWeekAsync(weekStart, ct));

    [Authorize(Roles = "Manager")]
    [HttpPost("shifts")]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftDto>> Create(UpsertShiftRequest request, CancellationToken ct)
    {
        var created = await _schedule.CreateAsync(request, ct);
        return Created($"/api/schedule/shifts/{created.Id}", created);
    }

    [Authorize(Roles = "Manager")]
    [HttpPut("shifts/{id:int}")]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftDto>> Update(int id, UpsertShiftRequest request, CancellationToken ct)
        => Ok(await _schedule.UpdateAsync(id, request, ct));

    [Authorize(Roles = "Manager")]
    [HttpDelete("shifts/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _schedule.DeleteAsync(id, ct);
        return NoContent();
    }

    [Authorize(Roles = "Manager")]
    [HttpPost("shifts/preview")]
    public async Task<ActionResult<List<ConflictDto>>> Preview(PreviewShiftRequest request, CancellationToken ct)
        => Ok(await _schedule.PreviewAsync(request, ct));
}
