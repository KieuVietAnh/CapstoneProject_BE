namespace UrbanService.BLL.Common.Helpers;

public static class SlaDateTimeHelper
{
    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// SQL Server datetime/datetime2 does not preserve DateTime.Kind.
    /// Values read back from DB can therefore be Unspecified even though
    /// the stored clock value represents UTC.
    /// </summary>
    public static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public static DateTime? AsUtc(DateTime? value)
    {
        return value.HasValue
            ? AsUtc(value.Value)
            : null;
    }

    /// <summary>
    /// Normalizes an input DateTime to UTC.
    /// Prefer clients sending ISO-8601 with Z or an explicit offset.
    /// If Kind is Unspecified, this project treats the value as UTC.
    /// </summary>
    public static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public static DateTime? NormalizeToUtc(DateTime? value)
    {
        return value.HasValue
            ? NormalizeToUtc(value.Value)
            : null;
    }

    public static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            // Linux / Docker
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows
            return TimeZoneInfo.FindSystemTimeZoneById(
                "SE Asia Standard Time");
        }
    }

    public static DateTime ToVietnamTime(
        DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(
            AsUtc(utcDateTime),
            GetVietnamTimeZone());
    }

    public static DateTime VietnamToUtc(
        DateTime vietnamDateTime)
    {
        var unspecified =
            DateTime.SpecifyKind(
                vietnamDateTime,
                DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(
            unspecified,
            GetVietnamTimeZone());
    }

    public static string FormatVietnamDateTime(
        DateTime utcDateTime)
    {
        return ToVietnamTime(utcDateTime)
            .ToString("dd/MM/yyyy HH:mm");
    }
}