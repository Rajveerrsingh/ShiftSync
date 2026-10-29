using ShiftSync.Domain.Scheduling;

namespace ShiftSync.Application.Scheduling;

public static class ConflictChecker
{
    public static IReadOnlyList<ScheduleConflict> Evaluate(
        ShiftDraft draft,
        decimal maxWeeklyHours,
        IReadOnlyList<ShiftSpan> existingShifts,
        IReadOnlyList<AvailabilitySpan> availability,
        IReadOnlyList<DateRange> approvedTimeOff)
    {
        var conflicts = new List<ScheduleConflict>();

        if (draft.EndsAt <= draft.StartsAt)
        {
            conflicts.Add(new(ConflictKind.InvalidRange, "The shift has to end after it starts."));
            return conflicts;
        }

        if (draft.StartsAt.Date != draft.EndsAt.Date)
        {
            conflicts.Add(new(ConflictKind.InvalidRange, "Keep a shift on one calendar day."));
            return conflicts;
        }

        foreach (var other in existingShifts)
        {
            if (draft.ShiftId is int id && other.ShiftId == id)
                continue;

            if (Overlaps(draft.StartsAt, draft.EndsAt, other.StartsAt, other.EndsAt))
            {
                conflicts.Add(new(
                    ConflictKind.Overlap,
                    $"Overlaps the shift from {other.StartsAt:ddd HH:mm} to {other.EndsAt:HH:mm}."));
                break;
            }
        }

        var start = TimeOnly.FromDateTime(draft.StartsAt);
        var end = TimeOnly.FromDateTime(draft.EndsAt);
        var windows = availability.Where(window => window.DayOfWeek == draft.StartsAt.DayOfWeek).ToList();
        var fits = windows.Any(window => window.Start <= start && end <= window.End);
        if (!fits)
        {
            var message = windows.Count == 0
                ? $"Not available on {draft.StartsAt:dddd}."
                : $"Outside availability on {draft.StartsAt:dddd} ({string.Join(", ", windows.Select(window => $"{window.Start:HH:mm}–{window.End:HH:mm}"))}).";
            conflicts.Add(new(ConflictKind.OutsideAvailability, message));
        }

        var day = DateOnly.FromDateTime(draft.StartsAt);
        if (approvedTimeOff.Any(range => range.Start <= day && day <= range.End))
            conflicts.Add(new(ConflictKind.ApprovedTimeOff, "Approved time off covers this day."));

        var weekStart = WeekStart(draft.StartsAt);
        var weekEnd = weekStart.AddDays(7);
        var hours = Hours(draft.StartsAt, draft.EndsAt);
        foreach (var other in existingShifts)
        {
            if (draft.ShiftId is int id && other.ShiftId == id)
                continue;
            if (other.StartsAt >= weekStart && other.StartsAt < weekEnd)
                hours += Hours(other.StartsAt, other.EndsAt);
        }

        if (hours > maxWeeklyHours)
        {
            conflicts.Add(new(
                ConflictKind.MaxWeeklyHours,
                $"This books {FormatHours(hours)} h in the week. The cap is {FormatHours(maxWeeklyHours)} h."));
        }

        return conflicts;
    }

    public static DateTime WeekStart(DateTime instant)
    {
        var day = instant.Date;
        var offset = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-offset);
    }

    public static decimal Hours(DateTime start, DateTime end)
        => Math.Round((decimal)(end - start).TotalMinutes / 60m, 2, MidpointRounding.AwayFromZero);

    private static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd)
        => start < otherEnd && end > otherStart;

    private static string FormatHours(decimal hours) => hours.ToString("0.#");
}
