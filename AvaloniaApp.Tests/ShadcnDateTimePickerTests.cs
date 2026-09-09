using System.Globalization;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class ShadcnDateTimePickerTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [TestMethod]
    [DataRow("9:05 AM", 9, 5)]
    [DataRow("09:05 AM", 9, 5)]
    [DataRow("9:05PM", 21, 5)]
    public void ParsesTwelveHourTime(string text, int hour, int minute)
    {
        var valid = ShadcnDateTimePicker.TryParseTime(text, English, out var value);

        Assert.IsTrue(valid);
        Assert.AreEqual(new TimeSpan(hour, minute, 0), value);
    }

    [TestMethod]
    [DataRow("0:05", 0, 5)]
    [DataRow("09:05", 9, 5)]
    [DataRow("21:05", 21, 5)]
    [DataRow("23:59", 23, 59)]
    public void ParsesTwentyFourHourTime(string text, int hour, int minute)
    {
        var valid = ShadcnDateTimePicker.TryParseTime(text, English, out var value);

        Assert.IsTrue(valid);
        Assert.AreEqual(new TimeSpan(hour, minute, 0), value);
    }

    [TestMethod]
    [DataRow("24:00")]
    [DataRow("12:60")]
    [DataRow("9:5 AM")]
    [DataRow("morning")]
    public void RejectsInvalidTime(string text)
    {
        var valid = ShadcnDateTimePicker.TryParseTime(text, English, out var value);

        Assert.IsFalse(valid);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TreatsEmptyTimeAsNull()
    {
        var valid = ShadcnDateTimePicker.TryParseTime("  ", English, out var value);

        Assert.IsTrue(valid);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void FormatsTimeWithoutSeconds()
    {
        var display = ShadcnDateTimePicker.FormatTime(new TimeSpan(21, 5, 37), English);

        Assert.AreEqual("9:05 PM", display);
    }
}
