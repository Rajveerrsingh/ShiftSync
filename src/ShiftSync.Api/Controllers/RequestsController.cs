using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftSync.Application.Dtos;
using ShiftSync.Application.Services;

namespace ShiftSync.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/requests")]
public class RequestsController : ControllerBase
{
    private readonly RequestService _requests;

    public RequestsController(RequestService requests)
    {
        _requests = requests;
    }

    [HttpGet("time-off")]
    public async Task<ActionResult<List<TimeOffDto>>> ListTimeOff(CancellationToken ct)
        => Ok(await _requests.ListTimeOffAsync(ct));

    [HttpPost("time-off")]
    public async Task<ActionResult<TimeOffDto>> CreateTimeOff(CreateTimeOffRequest request, CancellationToken ct)
    {
        var created = await _requests.CreateTimeOffAsync(request, ct);
        return Created($"/api/requests/time-off/{created.Id}", created);
    }

    [Authorize(Roles = "Manager")]
    [HttpPost("time-off/{id:int}/review")]
    public async Task<ActionResult<TimeOffDto>> ReviewTimeOff(int id, ReviewRequest request, CancellationToken ct)
        => Ok(await _requests.ReviewTimeOffAsync(id, request, ct));

    [HttpPost("time-off/{id:int}/cancel")]
    public async Task<IActionResult> CancelTimeOff(int id, CancellationToken ct)
    {
        await _requests.CancelTimeOffAsync(id, ct);
        return NoContent();
    }

    [HttpGet("swaps")]
    public async Task<ActionResult<List<SwapDto>>> ListSwaps(CancellationToken ct)
        => Ok(await _requests.ListSwapsAsync(ct));

    [HttpPost("swaps")]
    public async Task<ActionResult<SwapDto>> CreateSwap(CreateSwapRequest request, CancellationToken ct)
    {
        var created = await _requests.CreateSwapAsync(request, ct);
        return Created($"/api/requests/swaps/{created.Id}", created);
    }

    [Authorize(Roles = "Manager")]
    [HttpPost("swaps/{id:int}/review")]
    [ProducesResponseType(typeof(SwapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SwapDto>> ReviewSwap(int id, ReviewRequest request, CancellationToken ct)
        => Ok(await _requests.ReviewSwapAsync(id, request, ct));

    [HttpPost("swaps/{id:int}/cancel")]
    public async Task<IActionResult> CancelSwap(int id, CancellationToken ct)
    {
        await _requests.CancelSwapAsync(id, ct);
        return NoContent();
    }
}
