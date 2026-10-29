using ShiftSync.Domain.Scheduling;

namespace ShiftSync.Domain.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }

    public AppException(int statusCode, string title, string message) : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }
}

public class ConflictException : AppException
{
    public IReadOnlyList<ScheduleConflict> Conflicts { get; }

    public ConflictException(IReadOnlyList<ScheduleConflict> conflicts)
        : base(409, "Schedule conflict", Describe(conflicts))
    {
        Conflicts = conflicts;
    }

    private static string Describe(IReadOnlyList<ScheduleConflict> conflicts)
    {
        if (conflicts.Count == 0)
            return "This shift breaks a scheduling rule.";
        if (conflicts.Count == 1)
            return conflicts[0].Message;
        return $"{conflicts[0].Message} ({conflicts.Count} rules).";
    }
}

public static class Errors
{
    public static AppException NotFound(string message) => new(404, "Not found", message);

    public static AppException Forbidden(string message) => new(403, "Not allowed", message);

    public static AppException Bad(string title, string message) => new(400, title, message);
}
