using ShiftSync.Domain.Scheduling;

namespace ShiftSync.Application.Scheduling;

public static class CoveragePlanner
{
    public static IReadOnlyList<CoverageGap> FindGaps(
        DateTime weekStart,
        int minimumStaff,
        IReadOnlyList<OpenWindow> hours,
        IReadOnlyList<ShiftSpan> shifts)
    {
        var gaps = new List<CoverageGap>();
        var monday = WeekStart(weekStart);

        for (var offset = 0; offset < 7; offset++)
        {
            var date = monday.AddDays(offset);
            var window = hours.FirstOrDefault(item => item.DayOfWeek == date.DayOfWeek);
            if (window is null || window.IsClosed || window.Close <= window.Open)
                continue;

            TimeOnly? runStart = null;
            var runStaff = -1;
            var cursor = window.Open;

            while (cursor < window.Close)
            {
                var next = cursor.AddHours(1);
                if (next <= cursor || next > window.Close)
                    next = window.Close;

                var slotStart = date.Add(cursor.ToTimeSpan());
                var slotEnd = date.Add(next.ToTimeSpan());
                var staff = shifts.Count(shift => shift.StartsAt < slotEnd && shift.EndsAt > slotStart);

                if (staff < minimumStaff)
                {
                    if (runStart is null || staff != runStaff)
                    {
                        if (runStart is TimeOnly started)
                            gaps.Add(new(DateOnly.FromDateTime(date), started, cursor, runStaff, minimumStaff));
                        runStart = cursor;
                        runStaff = staff;
                    }
                }
                else if (runStart is TimeOnly started)
                {
                    gaps.Add(new(DateOnly.FromDateTime(date), started, cursor, runStaff, minimumStaff));
                    runStart = null;
                    runStaff = -1;
                }

                if (next == cursor)
                    break;
                cursor = next;
            }

            if (runStart is TimeOnly leftover)
                gaps.Add(new(DateOnly.FromDateTime(date), leftover, window.Close, runStaff, minimumStaff));
        }

        return gaps;
    }

    private static DateTime WeekStart(DateTime instant) => ConflictChecker.WeekStart(instant);
}
