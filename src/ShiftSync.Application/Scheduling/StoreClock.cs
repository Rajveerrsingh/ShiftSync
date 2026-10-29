namespace ShiftSync.Application.Scheduling;

public static class StoreClock
{
    public static DateTime Today()
        => DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);

    public static DateTime WeekContaining(DateTime instant)
    {
        var date = instant.Kind switch
        {
            DateTimeKind.Utc => DateTime.SpecifyKind(instant.ToLocalTime().Date, DateTimeKind.Unspecified),
            DateTimeKind.Local => DateTime.SpecifyKind(instant.Date, DateTimeKind.Unspecified),
            _ => DateTime.SpecifyKind(instant.Date, DateTimeKind.Unspecified)
        };
        return ConflictChecker.WeekStart(date);
    }

    public static DateTime AsStoreTime(DateTime value)
    {
        var clock = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        return DateTime.SpecifyKind(clock, DateTimeKind.Unspecified);
    }
}
