using Microsoft.AspNetCore.Identity;
using ShiftSync.Application.Abstractions;
using ShiftSync.Application.Dtos;
using ShiftSync.Application.Mapping;
using ShiftSync.Application.Scheduling;
using ShiftSync.Domain.Entities;
using ShiftSync.Domain.Enums;
using ShiftSync.Domain.Exceptions;

namespace ShiftSync.Application.Services;

public class EmployeeService
{
    private static readonly string[] Palette =
    [
        "#b5522a",
        "#1f6f6a",
        "#3d5a80",
        "#8a5a44",
        "#5c6b3c",
        "#7a4e7a",
        "#a15c12"
    ];

    private readonly ICurrentUser _current;
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher<Employee> _hasher;

    public EmployeeService(
        ICurrentUser current,
        IEmployeeRepository employees,
        IUnitOfWork uow,
        IPasswordHasher<Employee> hasher)
    {
        _current = current;
        _employees = employees;
        _uow = uow;
        _hasher = hasher;
    }

    public async Task<List<EmployeeDto>> ListAsync(CancellationToken ct)
    {
        var people = await _employees.ListForStoreAsync(_current.StoreId, activeOnly: !_current.IsManager, ct);
        return people.Select(StaffMap.Employee).ToList();
    }

    public async Task<EmployeeDto> GetAsync(int id, CancellationToken ct)
    {
        var employee = await RequireInStoreAsync(id, ct);
        if (!_current.IsManager && !employee.IsActive && employee.Id != _current.EmployeeId)
            throw Errors.NotFound("Employee not found.");
        return StaffMap.Employee(employee);
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken ct)
    {
        if (!_current.IsManager)
            throw Errors.Forbidden("Only a manager can add someone to the roster.");

        if (!Enum.TryParse<StaffRole>(request.Role, true, out var role) || !Enum.IsDefined(role))
            throw Errors.Bad("Invalid role", "Role must be Manager or Associate.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _employees.EmailExistsAsync(email, ct))
            throw new AppException(409, "Email in use", "That email is already on the roster.");

        var count = await _employees.CountAsync(_current.StoreId, ct);
        var employee = new Employee
        {
            StoreId = _current.StoreId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            Phone = request.Phone?.Trim() ?? "",
            Role = role,
            MaxWeeklyHours = request.MaxWeeklyHours,
            IsActive = true,
            Color = Palette[count % Palette.Length]
        };
        employee.PasswordHash = _hasher.HashPassword(employee, request.Password);
        await _employees.AddAsync(employee, ct);
        await _uow.SaveChangesAsync(ct);
        return StaffMap.Employee(employee);
    }

    public async Task<EmployeeDto> UpdateAsync(int id, UpdateEmployeeRequest request, CancellationToken ct)
    {
        if (!_current.IsManager)
            throw Errors.Forbidden("Only a manager can edit a profile.");

        var employee = await RequireInStoreAsync(id, ct);
        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.Phone = request.Phone?.Trim() ?? "";
        employee.MaxWeeklyHours = request.MaxWeeklyHours;
        employee.IsActive = request.IsActive;
        await _uow.SaveChangesAsync(ct);
        return StaffMap.Employee(employee);
    }

    public async Task<EmployeeDto> SetAvailabilityAsync(int id, IReadOnlyList<AvailabilityInput> windows, CancellationToken ct)
    {
        var employee = await RequireInStoreAsync(id, ct);
        if (!_current.IsManager && employee.Id != _current.EmployeeId)
            throw Errors.Forbidden("You can edit your own availability.");

        var mapped = new List<AvailabilityWindow>();
        foreach (var window in windows)
        {
            if (!Enum.IsDefined(typeof(DayOfWeek), window.DayOfWeek))
                throw Errors.Bad("Invalid day", "Day of week must be 0 (Sunday) through 6 (Saturday).");
            if (!TimeText.TryParse(window.Start, out var start) || !TimeText.TryParse(window.End, out var end))
                throw Errors.Bad("Invalid time", "Use 24-hour times like 09:00.");
            if (end <= start)
                throw Errors.Bad("Invalid time", "Availability has to end after it starts.");

            mapped.Add(new AvailabilityWindow
            {
                EmployeeId = employee.Id,
                DayOfWeek = (DayOfWeek)window.DayOfWeek,
                Start = start,
                End = end
            });
        }

        await _employees.ReplaceAvailabilityAsync(employee.Id, mapped, ct);
        await _uow.SaveChangesAsync(ct);
        employee.Availability = mapped;
        return StaffMap.Employee(employee);
    }

    public async Task<DemoDirectoryDto> DemoDirectoryAsync(CancellationToken ct)
    {
        var people = await _employees.ListAllAsync(ct);
        return new DemoDirectoryDto
        {
            Accounts = people.Select(person => new DemoAccountDto
            {
                FirstName = person.FirstName,
                LastName = person.LastName,
                Email = person.Email,
                Role = person.Role.ToString()
            }).ToList()
        };
    }

    private async Task<Employee> RequireInStoreAsync(int id, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(id, ct) ?? throw Errors.NotFound("Employee not found.");
        if (employee.StoreId != _current.StoreId)
            throw Errors.NotFound("Employee not found.");
        return employee;
    }
}
