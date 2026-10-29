using System.ComponentModel.DataAnnotations;

namespace ShiftSync.Application.Dtos;

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(120)]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public EmployeeDto Employee { get; set; } = new();
}

public class DemoDirectoryDto
{
    public string Password { get; set; } = "";
    public List<DemoAccountDto> Accounts { get; set; } = new();
}

public class DemoAccountDto
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class EmployeeDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string Phone { get; set; } = "";
    public decimal MaxWeeklyHours { get; set; }
    public bool IsActive { get; set; }
    public string Color { get; set; } = "";
    public List<AvailabilityDto> Availability { get; set; } = new();
}

public class AvailabilityDto
{
    public int DayOfWeek { get; set; }
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
}

public class CreateEmployeeRequest
{
    [Required, MaxLength(60)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(60)]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, MaxLength(120)]
    public string Email { get; set; } = "";

    [MaxLength(30)]
    public string? Phone { get; set; }

    [Required]
    public string Role { get; set; } = "Associate";

    [Range(1, 60)]
    public decimal MaxWeeklyHours { get; set; } = 40;

    [Required, MinLength(8), MaxLength(80)]
    public string Password { get; set; } = "";
}

public class UpdateEmployeeRequest
{
    [Required, MaxLength(60)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(60)]
    public string LastName { get; set; } = "";

    [MaxLength(30)]
    public string? Phone { get; set; }

    [Range(1, 60)]
    public decimal MaxWeeklyHours { get; set; }

    public bool IsActive { get; set; } = true;
}

public class AvailabilityInput
{
    [Range(0, 6)]
    public int DayOfWeek { get; set; }

    [Required]
    public string Start { get; set; } = "";

    [Required]
    public string End { get; set; } = "";
}

public class UpsertShiftRequest
{
    [Range(1, int.MaxValue)]
    public int EmployeeId { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    [Required]
    public string Position { get; set; } = "";

    [MaxLength(280)]
    public string? Notes { get; set; }
}

public class PreviewShiftRequest : UpsertShiftRequest
{
    public int? ShiftId { get; set; }
}

public class ShiftDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Position { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<string> Flags { get; set; } = new();
}

public class StoreHourDto
{
    public int DayOfWeek { get; set; }
    public string? Open { get; set; }
    public string? Close { get; set; }
    public bool IsClosed { get; set; }
}

public class CoverageGapDto
{
    public DateOnly Date { get; set; }
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public int Scheduled { get; set; }
    public int Minimum { get; set; }
}

public class WeekScheduleDto
{
    public DateOnly WeekStart { get; set; }
    public string StoreName { get; set; } = "";
    public string City { get; set; } = "";
    public int MinimumStaff { get; set; }
    public List<StoreHourDto> Hours { get; set; } = new();
    public List<EmployeeDto> Employees { get; set; } = new();
    public List<ShiftDto> Shifts { get; set; } = new();
    public List<CoverageGapDto> Gaps { get; set; } = new();
}

public class ConflictDto
{
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
}

public class CreateTimeOffRequest
{
    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Required, MaxLength(400)]
    public string Reason { get; set; } = "";
}

public class CreateSwapRequest
{
    [Required]
    public string Kind { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int ShiftId { get; set; }

    public int? CounterShiftId { get; set; }

    public int? TargetEmployeeId { get; set; }

    [MaxLength(400)]
    public string? Note { get; set; }
}

public class ReviewRequest
{
    public bool Approve { get; set; }

    [MaxLength(400)]
    public string? Note { get; set; }
}

public class TimeOffDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ReviewNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class SwapDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public int RequesterId { get; set; }
    public string RequesterName { get; set; } = "";
    public ShiftDto Shift { get; set; } = new();
    public ShiftDto? CounterShift { get; set; }
    public int? TargetEmployeeId { get; set; }
    public string? TargetEmployeeName { get; set; }
    public string Note { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ReviewNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DayLabourDto
{
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public int ShiftCount { get; set; }
}

public class PositionHoursDto
{
    public string Position { get; set; } = "";
    public decimal Hours { get; set; }
}

public class DashboardDto
{
    public DateOnly WeekStart { get; set; }
    public string StoreName { get; set; } = "";
    public decimal TotalHours { get; set; }
    public int ScheduledPeople { get; set; }
    public int GapCount { get; set; }
    public int PendingTimeOff { get; set; }
    public int PendingSwaps { get; set; }
    public bool IncludesToday { get; set; }
    public List<DayLabourDto> Days { get; set; } = new();
    public List<PositionHoursDto> Positions { get; set; } = new();
    public List<CoverageGapDto> Gaps { get; set; } = new();
    public List<ShiftDto> Today { get; set; } = new();
}
