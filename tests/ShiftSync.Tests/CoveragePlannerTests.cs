using ShiftSync.Application.Scheduling;
using ShiftSync.Domain.Scheduling;

namespace ShiftSync.Tests;

public class CoveragePlannerTests
{
    private static readonly DateTime Monday = new(2026, 11, 2);

    [Fact]
    public void An_hour_under_the_minimum_is_a_gap()
    {
        var hours = Open(DayOfWeek.Monday, 10, 12);
        var shifts = new List<ShiftSpan> { new(1, Monday.AddHours(10), Monday.AddHours(12)) };

        var gaps = CoveragePlanner.FindGaps(Monday, 2, hours, shifts);

        var gap = Assert.Single(gaps);
        Assert.Equal(new TimeOnly(10, 0), gap.Start);
        Assert.Equal(new TimeOnly(12, 0), gap.End);
        Assert.Equal(1, gap.Scheduled);
        Assert.Equal(2, gap.Minimum);
    }

    [Fact]
    public void Covered_hours_are_not_reported()
    {
        var hours = Open(DayOfWeek.Monday, 10, 12);
        var shifts = new List<ShiftSpan>
        {
            new(1, Monday.AddHours(10), Monday.AddHours(12)),
            new(2, Monday.AddHours(10), Monday.AddHours(12))
        };

        var gaps = CoveragePlanner.FindGaps(Monday, 2, hours, shifts);

        Assert.Empty(gaps);
    }

    [Fact]
    public void A_thin_stretch_in_the_middle_of_the_day_is_merged()
    {
        var hours = Open(DayOfWeek.Friday, 10, 14);
        var friday = Monday.AddDays(4);
        var shifts = new List<ShiftSpan>
        {
            new(1, friday.AddHours(10), friday.AddHours(14)),
            new(2, friday.AddHours(10), friday.AddHours(12))
        };

        var gaps = CoveragePlanner.FindGaps(Monday, 2, hours, shifts);

        var gap = Assert.Single(gaps);
        Assert.Equal(new DateOnly(2026, 11, 6), gap.Date);
        Assert.Equal(new TimeOnly(12, 0), gap.Start);
        Assert.Equal(new TimeOnly(14, 0), gap.End);
        Assert.Equal(1, gap.Scheduled);
    }

    [Fact]
    public void A_closed_day_is_ignored()
    {
        var hours = new List<OpenWindow>
        {
            new(DayOfWeek.Sunday, new TimeOnly(12, 0), new TimeOnly(17, 0), true)
        };

        var gaps = CoveragePlanner.FindGaps(Monday, 2, hours, []);

        Assert.Empty(gaps);
    }

    private static List<OpenWindow> Open(DayOfWeek day, int from, int to)
        => [new(day, new TimeOnly(from, 0), new TimeOnly(to, 0), false)];
}
