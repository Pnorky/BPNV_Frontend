using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class DateRangePickerTests
{
    [TestMethod]
    public void SelectsAndNormalizesDateRange()
    {
        var range = DateRangePicker.UpdateRange(null, null, new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero));
        Assert.AreEqual(new DateTime(2026, 8, 20), range.Start!.Value.Date);
        Assert.IsNull(range.End);

        range = DateRangePicker.UpdateRange(range.Start, range.End, new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero));
        Assert.AreEqual(new DateTime(2026, 8, 10), range.Start!.Value.Date);
        Assert.AreEqual(new DateTime(2026, 8, 20), range.End!.Value.Date);

        range = DateRangePicker.UpdateRange(range.Start, range.End, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.AreEqual(new DateTime(2026, 9, 1), range.Start!.Value.Date);
        Assert.IsNull(range.End);
    }

    [TestMethod]
    public void FormatsDateRange()
    {
        var start = new DateTimeOffset(2026, 8, 8, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero);
        StringAssert.Contains(DateRangePicker.FormatRange(start, end), "August 8, 2026");
        StringAssert.Contains(DateRangePicker.FormatRange(start, end), "August 21, 2026");
        Assert.AreEqual("Pick a date range", DateRangePicker.FormatRange(null, null));
    }
}
