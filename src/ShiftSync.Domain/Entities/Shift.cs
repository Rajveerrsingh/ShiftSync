using ShiftSync.Domain.Enums;

namespace ShiftSync.Domain.Entities;

public class Shift
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public ShiftPosition Position { get; set; }
    public string Notes { get; set; } = "";
}
