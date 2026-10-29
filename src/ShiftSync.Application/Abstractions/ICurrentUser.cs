using ShiftSync.Domain.Enums;

namespace ShiftSync.Application.Abstractions;

public interface ICurrentUser
{
    int EmployeeId { get; }
    int StoreId { get; }
    StaffRole Role { get; }
    bool IsManager { get; }
}
