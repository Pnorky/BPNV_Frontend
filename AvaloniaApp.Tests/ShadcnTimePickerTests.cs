using System.Globalization;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class ShadcnTimePickerTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [TestMethod]
    [DataRow("6:00 AM", 6, 0)]
    [DataRow("2:15PM", 14, 15)]
    [DataRow("21:05", 21, 5)]
    public void ParsesSupportedTimeFormats(string text, int hour, int minute)
    {
        var valid = ShadcnTimePicker.TryParseTime(text, English, out var value);

        Assert.IsTrue(valid);
        Assert.AreEqual(new TimeSpan(hour, minute, 0), value);
    }

    [TestMethod]
    public void RejectsInvalidTime()
    {
        var valid = ShadcnTimePicker.TryParseTime("25:00", English, out var value);

        Assert.IsFalse(valid);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void FormatsBoundValueWithoutSeconds()
    {
        Assert.AreEqual("2:15 PM", ShadcnTimePicker.FormatTime(new TimeSpan(14, 15, 42), English));
        Assert.AreEqual("", ShadcnTimePicker.FormatTime(null, English));
    }
}
