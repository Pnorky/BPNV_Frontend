using AvaloniaApp.Services;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class StoreDateTimeTests
{
    [TestMethod]
    public void FormatsUtcTimestampInManilaTime()
    {
        var utc = new DateTime(2025, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual("August 26, 2025 8:00 AM", StoreDateTime.FormatUtc(utc));
    }

    [TestMethod]
    public void PreservesLegacyUnspecifiedWallClockTime()
    {
        var legacy = new DateTime(2025, 8, 26, 8, 0, 0, DateTimeKind.Unspecified);

        Assert.AreEqual("August 26, 2025 8:00 AM", StoreDateTime.FormatEvent(legacy));
        Assert.AreEqual(
            new DateTime(2025, 8, 26, 0, 0, 0, DateTimeKind.Utc),
            StoreDateTime.NormalizeEventToUtc(legacy));
    }

    [TestMethod]
    public void ConvertsManilaCalendarRangeToExclusiveUtcBounds()
    {
        var start = StoreDateTime.AtStoreMidnight(new DateTime(2025, 8, 26));
        var end = StoreDateTime.AtStoreMidnight(new DateTime(2025, 8, 27));

        var range = StoreDateTime.GetUtcDateRange(start, end);

        Assert.AreEqual(new DateTimeOffset(2025, 8, 25, 16, 0, 0, TimeSpan.Zero), range.FromUtc);
        Assert.AreEqual(new DateTimeOffset(2025, 8, 27, 16, 0, 0, TimeSpan.Zero), range.ToUtcExclusive);
    }

    [TestMethod]
    public void FormatsDateOnlyValueWithoutAddingTime()
    {
        Assert.AreEqual("August 26, 2025", StoreDateTime.FormatDateOnly("2025-08-26"));
    }
}
