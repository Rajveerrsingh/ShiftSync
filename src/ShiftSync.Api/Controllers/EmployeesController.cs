using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftSync.Application.Dtos;
using ShiftSync.Application.Services;

namespace ShiftSync.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly EmployeeService _employees;

    public EmployeesController(EmployeeService employees)
    {
        _employees = employees;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmployeeDto>>> List(CancellationToken ct)
        => Ok(await _employees.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Get(int id, CancellationToken ct)
        => Ok(await _employees.GetAsync(id, ct));

    [Authorize(Roles = "Manager")]
    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken ct)
    {
        var created = await _employees.CreateAsync(request, ct);
        return Created($"/api/employees/{created.Id}", created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Update(int id, UpdateEmployeeRequest request, CancellationToken ct)
        => Ok(await _employees.UpdateAsync(id, request, ct));

    [HttpPut("{id:int}/availability")]
    public async Task<ActionResult<EmployeeDto>> SetAvailability(int id, List<AvailabilityInput> windows, CancellationToken ct)
        => Ok(await _employees.SetAvailabilityAsync(id, windows, ct));
}
