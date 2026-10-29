using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftSync.Api.Auth;
using ShiftSync.Application.Dtos;
using ShiftSync.Application.Mapping;
using ShiftSync.Application.Services;
using ShiftSync.Domain.Exceptions;
using ShiftSync.Infrastructure.Seed;

namespace ShiftSync.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly EmployeeService _employees;
    private readonly JwtTokenService _tokens;
    private readonly IHostEnvironment _environment;

    public AuthController(
        AuthService auth,
        EmployeeService employees,
        JwtTokenService tokens,
        IHostEnvironment environment)
    {
        _auth = auth;
        _employees = employees;
        _tokens = tokens;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var employee = await _auth.AuthenticateAsync(request.Email, request.Password, ct);
        if (employee is null)
            throw new AppException(401, "Unauthorized", "Those credentials didn't match.");

        return Ok(new LoginResponse
        {
            Token = _tokens.Create(employee),
            Employee = StaffMap.Employee(employee)
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<EmployeeDto>> Me(CancellationToken ct)
    {
        var id = User.FindFirst("sub")?.Value;
        if (!int.TryParse(id, out var employeeId))
            throw new AppException(401, "Unauthorized", "Sign in again.");
        return Ok(await _employees.GetAsync(employeeId, ct));
    }

    [AllowAnonymous]
    [HttpGet("demo")]
    public async Task<ActionResult<DemoDirectoryDto>> Demo(CancellationToken ct)
    {
        if (_environment.IsProduction())
            return NotFound();

        var directory = await _employees.DemoDirectoryAsync(ct);
        directory.Password = DemoSeeder.Password;
        return Ok(directory);
    }
}
