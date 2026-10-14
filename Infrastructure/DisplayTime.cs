using System.Globalization;

namespace RevUpIQ.Admin.Infrastructure;

public static class DisplayTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-7);
    public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(Offset);
    public static DateTime Today => Now.Date;

    public static DateTimeOffset Convert(DateTime value)
    {
        // Supabase timestamps without a Kind are UTC, not the server's local time.
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        return new DateTimeOffset(utc).ToOffset(Offset);
    }

    public static string Format(DateTime? value, string missing = "—") =>
        value.HasValue ? Convert(value.Value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : missing;

    public static string Format(DateTimeOffset value) =>
        value.ToOffset(Offset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
