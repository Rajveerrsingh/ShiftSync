using Microsoft.EntityFrameworkCore;
using ShiftSync.Application.Abstractions;
using ShiftSync.Domain.Entities;
using ShiftSync.Infrastructure.Persistence;

namespace ShiftSync.Infrastructure.Repositories;

public class ShiftRepository : IShiftRepository
{
    private readonly AppDbContext _db;

    public ShiftRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Shift?> GetByIdAsync(int id, CancellationToken ct)
        => _db.Shifts.Include(shift => shift.Employee)
            .FirstOrDefaultAsync(shift => shift.Id == id, ct);

    public Task<List<Shift>> ListForStoreWeekAsync(int storeId, DateTime weekStart, DateTime weekEnd, CancellationToken ct)
        => _db.Shifts.AsNoTracking()
            .Include(shift => shift.Employee)
            .Where(shift => shift.StoreId == storeId && shift.StartsAt >= weekStart && shift.StartsAt < weekEnd)
            .OrderBy(shift => shift.StartsAt)
            .ToListAsync(ct);

    public Task<List<Shift>> ListForEmployeeAsync(int employeeId, DateTime from, DateTime to, CancellationToken ct)
        => _db.Shifts.AsNoTracking()
            .Where(shift => shift.EmployeeId == employeeId && shift.StartsAt >= from && shift.StartsAt < to)
            .OrderBy(shift => shift.StartsAt)
            .ToListAsync(ct);

    public Task AddAsync(Shift shift, CancellationToken ct)
        => _db.Shifts.AddAsync(shift, ct).AsTask();

    public void Remove(Shift shift) => _db.Shifts.Remove(shift);
}
