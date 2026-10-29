using ShiftSync.Domain.Enums;

namespace ShiftSync.Domain.Entities;

public class ShiftSwapRequest
{
    public int Id { get; set; }
    public SwapKind Kind { get; set; }
    public int RequesterId { get; set; }
    public Employee Requester { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public int? CounterShiftId { get; set; }
    public Shift? CounterShift { get; set; }
    public int? TargetEmployeeId { get; set; }
    public Employee? TargetEmployee { get; set; }
    public string Note { get; set; } = "";
    public RequestStatus Status { get; set; }
    public int? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
