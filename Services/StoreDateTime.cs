using System.Globalization;

namespace AvaloniaApp.Services;

public static class StoreDateTime
{
    public const string TimestampFormat = "MMMM d, yyyy h:mm tt";
    public const string DateFormat = "MMMM d, yyyy";
    public const string ExcelTimestampFormat = "mmmm d, yyyy h:mm AM/PM";

    private static readonly TimeZoneInfo StoreTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public static DateTime UtcNow => DateTime.UtcNow;
    public static DateTime StoreNow => ToStoreTimeFromUtc(UtcNow);
    public static DateTime StoreToday => StoreNow.Date;

    public static string FormatUtc(DateTime value) =>
        ToStoreTimeFromUtc(value).ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static string FormatEvent(DateTime value) =>
        ToStoreTime(value).ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static DateTime ToStoreTimeFromUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, StoreTimeZone);
    }

    public static DateTime ToStoreTime(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => ToStoreTimeFromUtc(value),
        DateTimeKind.Local => ToStoreTimeFromUtc(value.ToUniversalTime()),
        _ => value
    };

    public static DateTime NormalizeEventToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(value, StoreTimeZone)
    };

    public static DateTime StoreTimeToUtc(DateTime value) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), StoreTimeZone);

    public static DateTimeOffset AtStoreMidnight(DateTime date)
    {
        var wallTime = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        return new DateTimeOffset(wallTime, StoreTimeZone.GetUtcOffset(wallTime));
    }

    public static (DateTimeOffset? FromUtc, DateTimeOffset? ToUtcExclusive) GetUtcDateRange(
        DateTimeOffset? start,
        DateTimeOffset? end)
    {
        DateTimeOffset? fromUtc = start.HasValue ? AtStoreMidnight(start.Value.Date).ToUniversalTime() : null;
        var lastDate = end ?? start;
        DateTimeOffset? toUtcExclusive = lastDate.HasValue
            ? AtStoreMidnight(lastDate.Value.Date.AddDays(1)).ToUniversalTime()
            : null;
        return (fromUtc, toUtcExclusive);
    }

    public static bool IsStoreToday(DateTime value) => ToStoreTime(value).Date == StoreToday;

    public static string FormatDateOnly(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString(DateFormat, CultureInfo.InvariantCulture)
            : value;
}
