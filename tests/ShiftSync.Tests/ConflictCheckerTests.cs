using ShiftSync.Application.Scheduling;
using ShiftSync.Domain.Scheduling;

namespace ShiftSync.Tests;

public class ConflictCheckerTests
{
    private static readonly DateTime Monday = new(2026, 11, 2);

    [Fact]
    public void Overlapping_shifts_are_flagged()
    {
        var existing = new List<ShiftSpan>
        {
            new(1, At(9), At(13))
        };

        var conflicts = Check(new ShiftDraft(null, At(12), At(16)), 40, existing);

        Assert.Contains(conflicts, conflict => conflict.Kind == ConflictKind.Overlap);
    }

    [Fact]
    public void Back_to_back_shifts_are_allowed()
    {
        var existing = new List<ShiftSpan>
        {
            new(1, At(9), At(13))
        };

        var conflicts = Check(new ShiftDraft(null, At(13), At(17)), 40, existing);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void Editing_a_shift_ignores_itself()
    {
        var existing = new List<ShiftSpan>
        {
            new(7, At(9), At(13))
        };

        var conflicts = Check(new ShiftDraft(7, At(9), At(14)), 40, existing);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void Shift_outside_availability_is_flagged()
    {
        var availability = new List<AvailabilitySpan>
        {
            new(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(14, 0))
        };

        var conflicts = Check(
            new ShiftDraft(null, At(15), At(19)),
            40,
            availability: availability);

        Assert.Contains(conflicts, conflict => conflict.Kind == ConflictKind.OutsideAvailability);
    }

    [Fact]
    public void Shift_inside_a_second_window_is_allowed()
    {
        var availability = new List<AvailabilitySpan>
        {
            new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
            new(DayOfWeek.Monday, new TimeOnly(16, 0), new TimeOnly(21, 0))
        };

        var conflicts = Check(
            new ShiftDraft(null, At(17), At(20)),
            40,
            availability: availability);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void A_day_with_no_availability_is_flagged()
    {
        var conflicts = Check(new ShiftDraft(null, At(10), At(14)), 40, availability: []);

        Assert.Contains(conflicts, conflict => conflict.Kind == ConflictKind.OutsideAvailability);
    }

    [Fact]
    public void Hours_over_the_weekly_max_are_flagged()
    {
        var existing = new List<ShiftSpan>
        {
            new(1, At(9), At(15))
        };

        var conflicts = Check(new ShiftDraft(null, At(15), At(20)), 10, existing);

        Assert.Contains(conflicts, conflict => conflict.Kind == ConflictKind.MaxWeeklyHours);
    }

    [Fact]
    public void Hours_sitting_on_the_max_are_allowed()
    {
        var existing = new List<ShiftSpan>
        {
            new(1, At(9), At(13))
        };

        var conflicts = Check(
            new ShiftDraft(null, At(13), At(17)),
            8,
            existing,
            availability: AllDay());

        Assert.Empty(conflicts);
    }

    [Fact]
    public void Approved_time_off_blocks_the_shift()
    {
        var timeOff = new List<DateRange>
        {
            new(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3))
        };

        var conflicts = Check(
            new ShiftDraft(null, At(10), At(14)),
            40,
            timeOff: timeOff);

        Assert.Contains(conflicts, conflict => conflict.Kind == ConflictKind.ApprovedTimeOff);
    }

    [Fact]
    public void The_day_after_time_off_is_clear()
    {
        var timeOff = new List<DateRange>
        {
            new(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2))
        };

        var conflicts = Check(
            new ShiftDraft(null, At(10, dayOffset: 1), At(14, dayOffset: 1)),
            40,
            timeOff: timeOff);

        Assert.DoesNotContain(conflicts, conflict => conflict.Kind == ConflictKind.ApprovedTimeOff);
    }

    [Fact]
    public void A_shift_that_does_not_end_after_it_starts_is_invalid()
    {
        var conflicts = Check(new ShiftDraft(null, At(12), At(9)), 40);

        Assert.Contains(conflicts, conflict => conflict.Kind == ConflictKind.InvalidRange);
    }

    [Fact]
    public void Overnight_shifts_are_rejected()
    {
        var conflicts = Check(
            new ShiftDraft(null, At(20), At(2, dayOffset: 1)),
            40,
            availability: AllDay());

        Assert.Contains(conflicts, conflict => conflict.Kind == ConflictKind.InvalidRange);
    }

    [Fact]
    public void Weekly_hours_from_another_week_do_not_count()
    {
        var existing = new List<ShiftSpan>
        {
            new(1, At(9, dayOffset: -7), At(17, dayOffset: -7))
        };

        var conflicts = Check(
            new ShiftDraft(null, At(9), At(17)),
            8,
            existing,
            availability: AllDay());

        Assert.Empty(conflicts);
    }

    private static IReadOnlyList<ScheduleConflict> Check(
        ShiftDraft draft,
        decimal maxHours,
        IReadOnlyList<ShiftSpan>? existing = null,
        IReadOnlyList<AvailabilitySpan>? availability = null,
        IReadOnlyList<DateRange>? timeOff = null)
    {
        return ConflictChecker.Evaluate(
            draft,
            maxHours,
            existing ?? [],
            availability ?? AllDay(),
            timeOff ?? []);
    }

    private static List<AvailabilitySpan> AllDay()
        => [new(DayOfWeek.Monday, new TimeOnly(0, 0), new TimeOnly(23, 59)),
            new(DayOfWeek.Tuesday, new TimeOnly(0, 0), new TimeOnly(23, 59))];

    private static DateTime At(int hour, int dayOffset = 0) => Monday.AddDays(dayOffset).AddHours(hour);
}
