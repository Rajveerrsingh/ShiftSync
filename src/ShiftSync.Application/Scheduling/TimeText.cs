using System.Globalization;

namespace ShiftSync.Application.Scheduling;

public static class TimeText
{
    private static readonly string[] Formats = ["HH:mm", "H:mm", "HH:mm:ss"];

    public static bool TryParse(string? text, out TimeOnly time)
        => TimeOnly.TryParseExact(
            text?.Trim(),
            Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
}
