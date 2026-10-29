using ShiftSync.Application.Dtos;
using ShiftSync.Domain.Entities;
using ShiftSync.Domain.Scheduling;

namespace ShiftSync.Application.Mapping;

public static class StaffMap
{
    public static EmployeeDto Employee(Employee employee) => new()
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        Role = employee.Role.ToString(),
        Phone = employee.Phone,
        MaxWeeklyHours = employee.MaxWeeklyHours,
        IsActive = employee.IsActive,
        Color = employee.Color,
        Availability = employee.Availability
            .OrderBy(window => window.DayOfWeek)
            .ThenBy(window => window.Start)
            .Select(window => new AvailabilityDto
            {
                DayOfWeek = (int)window.DayOfWeek,
                Start = window.Start.ToString("HH:mm"),
                End = window.End.ToString("HH:mm")
            })
            .ToList()
    };

    public static ShiftDto Shift(Shift shift, IReadOnlyList<string>? flags = null) => new()
    {
        Id = shift.Id,
        EmployeeId = shift.EmployeeId,
        EmployeeName = shift.Employee?.FullName ?? "",
        StartsAt = shift.StartsAt,
        EndsAt = shift.EndsAt,
        Position = shift.Position.ToString(),
        Notes = shift.Notes,
        Flags = flags?.ToList() ?? []
    };

    public static CoverageGapDto Gap(CoverageGap gap) => new()
    {
        Date = gap.Date,
        Start = gap.Start.ToString("HH:mm"),
        End = gap.End.ToString("HH:mm"),
        Scheduled = gap.Scheduled,
        Minimum = gap.Minimum
    };

    public static ConflictDto Conflict(ScheduleConflict conflict) => new()
    {
        Kind = conflict.Kind.ToString(),
        Message = conflict.Message
    };

    public static TimeOffDto TimeOff(TimeOffRequest request, IReadOnlyList<string>? warnings = null) => new()
    {
        Id = request.Id,
        EmployeeId = request.EmployeeId,
        EmployeeName = request.Employee?.FullName ?? "",
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        Reason = request.Reason,
        Status = request.Status.ToString(),
        ReviewNote = request.ReviewNote,
        ReviewedBy = request.ReviewedBy?.FullName,
        CreatedAt = request.CreatedAt,
        Warnings = warnings?.ToList() ?? []
    };

    public static SwapDto Swap(ShiftSwapRequest request) => new()
    {
        Id = request.Id,
        Kind = request.Kind.ToString(),
        RequesterId = request.RequesterId,
        RequesterName = request.Requester?.FullName ?? "",
        Shift = Shift(request.Shift),
        CounterShift = request.CounterShift is null ? null : Shift(request.CounterShift),
        TargetEmployeeId = request.TargetEmployeeId,
        TargetEmployeeName = request.TargetEmployee?.FullName,
        Note = request.Note,
        Status = request.Status.ToString(),
        ReviewNote = request.ReviewNote,
        ReviewedBy = request.ReviewedBy?.FullName,
        CreatedAt = request.CreatedAt
    };
}
