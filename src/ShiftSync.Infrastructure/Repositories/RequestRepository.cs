using Microsoft.EntityFrameworkCore;
using ShiftSync.Application.Abstractions;
using ShiftSync.Domain.Entities;
using ShiftSync.Domain.Enums;
using ShiftSync.Infrastructure.Persistence;

namespace ShiftSync.Infrastructure.Repositories;

public class RequestRepository : IRequestRepository
{
    private readonly AppDbContext _db;

    public RequestRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<TimeOffRequest?> GetTimeOffAsync(int id, CancellationToken ct)
        => TimeOffQuery().FirstOrDefaultAsync(request => request.Id == id, ct);

    public Task<TimeOffRequest?> ReadTimeOffAsync(int id, CancellationToken ct)
        => TimeOffQuery().AsNoTracking().FirstOrDefaultAsync(request => request.Id == id, ct);

    public Task<List<TimeOffRequest>> ListTimeOffForStoreAsync(int storeId, CancellationToken ct)
        => TimeOffQuery().AsNoTracking()
            .Where(request => request.Employee.StoreId == storeId)
            .OrderByDescending(request => request.Status == RequestStatus.Pending)
            .ThenByDescending(request => request.CreatedAt)
            .ToListAsync(ct);

    public Task<List<TimeOffRequest>> ListApprovedAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct)
        => _db.TimeOffRequests.AsNoTracking()
            .Where(request => request.EmployeeId == employeeId
                && request.Status == RequestStatus.Approved
                && request.StartDate <= to
                && request.EndDate >= from)
            .ToListAsync(ct);

    public Task<List<TimeOffRequest>> ListApprovedForStoreAsync(int storeId, DateOnly from, DateOnly to, CancellationToken ct)
        => _db.TimeOffRequests.AsNoTracking()
            .Where(request => request.Employee.StoreId == storeId
                && request.Status == RequestStatus.Approved
                && request.StartDate <= to
                && request.EndDate >= from)
            .ToListAsync(ct);

    public Task<int> CountTimeOffAsync(int storeId, RequestStatus status, CancellationToken ct)
        => _db.TimeOffRequests.CountAsync(
            request => request.Employee.StoreId == storeId && request.Status == status,
            ct);

    public Task AddTimeOffAsync(TimeOffRequest request, CancellationToken ct)
        => _db.TimeOffRequests.AddAsync(request, ct).AsTask();

    public Task<ShiftSwapRequest?> GetSwapAsync(int id, CancellationToken ct)
        => SwapQuery().FirstOrDefaultAsync(request => request.Id == id, ct);

    public Task<ShiftSwapRequest?> ReadSwapAsync(int id, CancellationToken ct)
        => SwapQuery().AsNoTracking().FirstOrDefaultAsync(request => request.Id == id, ct);

    public Task<List<ShiftSwapRequest>> ListSwapsForStoreAsync(int storeId, CancellationToken ct)
        => SwapQuery().AsNoTracking()
            .Where(request => request.Requester.StoreId == storeId)
            .OrderByDescending(request => request.Status == RequestStatus.Pending)
            .ThenByDescending(request => request.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> HasPendingForShiftAsync(int shiftId, CancellationToken ct)
        => _db.ShiftSwapRequests.AnyAsync(
            request => request.Status == RequestStatus.Pending
                && (request.ShiftId == shiftId || request.CounterShiftId == shiftId),
            ct);

    public Task<int> CountSwapsAsync(int storeId, RequestStatus status, CancellationToken ct)
        => _db.ShiftSwapRequests.CountAsync(
            request => request.Requester.StoreId == storeId && request.Status == status,
            ct);

    public Task AddSwapAsync(ShiftSwapRequest request, CancellationToken ct)
        => _db.ShiftSwapRequests.AddAsync(request, ct).AsTask();

    public async Task RemoveSwapsForShiftAsync(int shiftId, CancellationToken ct)
    {
        var rows = await _db.ShiftSwapRequests
            .Where(request => request.ShiftId == shiftId || request.CounterShiftId == shiftId)
            .ToListAsync(ct);
        _db.ShiftSwapRequests.RemoveRange(rows);
    }

    private IQueryable<TimeOffRequest> TimeOffQuery()
        => _db.TimeOffRequests
            .Include(request => request.Employee)
            .Include(request => request.ReviewedBy);

    private IQueryable<ShiftSwapRequest> SwapQuery()
        => _db.ShiftSwapRequests
            .AsSplitQuery()
            .Include(request => request.Requester)
            .Include(request => request.Shift).ThenInclude(shift => shift.Employee)
            .Include(request => request.CounterShift!).ThenInclude(shift => shift.Employee)
            .Include(request => request.TargetEmployee)
            .Include(request => request.ReviewedBy);
}
