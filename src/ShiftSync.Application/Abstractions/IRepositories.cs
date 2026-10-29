using ShiftSync.Domain.Entities;
using ShiftSync.Domain.Enums;

namespace ShiftSync.Application.Abstractions;

public interface IStoreRepository
{
    Task<Store?> GetWithHoursAsync(int storeId, CancellationToken ct);
}

public interface IEmployeeRepository
{
    Task<Employee?> FindByEmailAsync(string email, CancellationToken ct);
    Task<Employee?> GetByIdAsync(int id, CancellationToken ct);
    Task<List<Employee>> ListForStoreAsync(int storeId, bool activeOnly, CancellationToken ct);
    Task<List<Employee>> ListAllAsync(CancellationToken ct);
    Task<int> CountAsync(int storeId, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    Task AddAsync(Employee employee, CancellationToken ct);
    Task ReplaceAvailabilityAsync(int employeeId, IReadOnlyList<AvailabilityWindow> windows, CancellationToken ct);
}

public interface IShiftRepository
{
    Task<Shift?> GetByIdAsync(int id, CancellationToken ct);
    Task<List<Shift>> ListForStoreWeekAsync(int storeId, DateTime weekStart, DateTime weekEnd, CancellationToken ct);
    Task<List<Shift>> ListForEmployeeAsync(int employeeId, DateTime from, DateTime to, CancellationToken ct);
    Task AddAsync(Shift shift, CancellationToken ct);
    void Remove(Shift shift);
}

public interface IRequestRepository
{
    Task<TimeOffRequest?> GetTimeOffAsync(int id, CancellationToken ct);
    Task<TimeOffRequest?> ReadTimeOffAsync(int id, CancellationToken ct);
    Task<List<TimeOffRequest>> ListTimeOffForStoreAsync(int storeId, CancellationToken ct);
    Task<List<TimeOffRequest>> ListApprovedAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<List<TimeOffRequest>> ListApprovedForStoreAsync(int storeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<int> CountTimeOffAsync(int storeId, RequestStatus status, CancellationToken ct);
    Task AddTimeOffAsync(TimeOffRequest request, CancellationToken ct);

    Task<ShiftSwapRequest?> GetSwapAsync(int id, CancellationToken ct);
    Task<ShiftSwapRequest?> ReadSwapAsync(int id, CancellationToken ct);
    Task<List<ShiftSwapRequest>> ListSwapsForStoreAsync(int storeId, CancellationToken ct);
    Task<bool> HasPendingForShiftAsync(int shiftId, CancellationToken ct);
    Task<int> CountSwapsAsync(int storeId, RequestStatus status, CancellationToken ct);
    Task AddSwapAsync(ShiftSwapRequest request, CancellationToken ct);
    Task RemoveSwapsForShiftAsync(int shiftId, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
