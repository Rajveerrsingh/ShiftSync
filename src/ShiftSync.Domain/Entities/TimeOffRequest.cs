using ShiftSync.Domain.Enums;

namespace ShiftSync.Domain.Entities;

public class TimeOffRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = "";
    public RequestStatus Status { get; set; }
    public int? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
