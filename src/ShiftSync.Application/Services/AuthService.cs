using Microsoft.AspNetCore.Identity;
using ShiftSync.Application.Abstractions;
using ShiftSync.Domain.Entities;

namespace ShiftSync.Application.Services;

public class AuthService
{
    private readonly IEmployeeRepository _employees;
    private readonly IPasswordHasher<Employee> _hasher;

    public AuthService(IEmployeeRepository employees, IPasswordHasher<Employee> hasher)
    {
        _employees = employees;
        _hasher = hasher;
    }

    public async Task<Employee?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        var employee = await _employees.FindByEmailAsync(email, ct);
        if (employee is null || !employee.IsActive)
            return null;

        var result = _hasher.VerifyHashedPassword(employee, employee.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : employee;
    }
}
