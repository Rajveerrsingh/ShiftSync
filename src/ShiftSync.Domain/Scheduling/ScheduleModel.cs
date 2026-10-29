namespace ShiftSync.Domain.Scheduling;

public enum ConflictKind
{
    InvalidRange,
    Overlap,
    OutsideAvailability,
    MaxWeeklyHours,
    ApprovedTimeOff
}

public record ScheduleConflict(ConflictKind Kind, string Message);

public record ShiftDraft(int? ShiftId, DateTime StartsAt, DateTime EndsAt);

public record ShiftSpan(int ShiftId, DateTime StartsAt, DateTime EndsAt);

public record AvailabilitySpan(DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End);

public record DateRange(DateOnly Start, DateOnly End);

public record OpenWindow(DayOfWeek DayOfWeek, TimeOnly Open, TimeOnly Close, bool IsClosed);

public record CoverageGap(DateOnly Date, TimeOnly Start, TimeOnly End, int Scheduled, int Minimum);
