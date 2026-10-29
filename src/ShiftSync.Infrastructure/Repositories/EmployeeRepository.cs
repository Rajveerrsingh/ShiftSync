using Microsoft.EntityFrameworkCore;
using ShiftSync.Application.Abstractions;
using ShiftSync.Domain.Entities;
using ShiftSync.Infrastructure.Persistence;

namespace ShiftSync.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _db;

    public EmployeeRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Employee?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var key = email.Trim().ToLowerInvariant();
        return _db.Employees.Include(employee => employee.Availability)
            .FirstOrDefaultAsync(employee => employee.Email == key, ct);
    }

    public Task<Employee?> GetByIdAsync(int id, CancellationToken ct)
        => _db.Employees.Include(employee => employee.Availability)
            .FirstOrDefaultAsync(employee => employee.Id == id, ct);

    public Task<List<Employee>> ListForStoreAsync(int storeId, bool activeOnly, CancellationToken ct)
    {
        var query = _db.Employees.AsNoTracking()
            .Include(employee => employee.Availability)
            .Where(employee => employee.StoreId == storeId);
        if (activeOnly)
            query = query.Where(employee => employee.IsActive);

        return query
            .OrderByDescending(employee => employee.Role)
            .ThenBy(employee => employee.LastName)
            .ThenBy(employee => employee.FirstName)
            .ToListAsync(ct);
    }

    public Task<List<Employee>> ListAllAsync(CancellationToken ct)
        => _db.Employees.AsNoTracking()
            .OrderByDescending(employee => employee.Role)
            .ThenBy(employee => employee.LastName)
            .ToListAsync(ct);

    public Task<int> CountAsync(int storeId, CancellationToken ct)
        => _db.Employees.CountAsync(employee => employee.StoreId == storeId, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct)
    {
        var key = email.Trim().ToLowerInvariant();
        return _db.Employees.AnyAsync(employee => employee.Email == key, ct);
    }

    public Task AddAsync(Employee employee, CancellationToken ct)
        => _db.Employees.AddAsync(employee, ct).AsTask();

    public async Task ReplaceAvailabilityAsync(int employeeId, IReadOnlyList<AvailabilityWindow> windows, CancellationToken ct)
    {
        var existing = await _db.AvailabilityWindows.Where(window => window.EmployeeId == employeeId).ToListAsync(ct);
        _db.AvailabilityWindows.RemoveRange(existing);
        await _db.AvailabilityWindows.AddRangeAsync(windows, ct);
    }
}
