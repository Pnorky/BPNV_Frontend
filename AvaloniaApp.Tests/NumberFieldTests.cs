using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class NumberFieldTests
{
    [TestMethod]
    public void ValueIsClampedAndFormatted()
    {
        var field = new NumberField
        {
            Minimum = 1,
            Maximum = 24,
            FormatString = "0.00",
            Value = 30
        };

        Assert.AreEqual(24m, field.Value);
        Assert.AreEqual(24m.ToString("0.00"), field.Input.Text);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("1.5")]
    [DataRow("abc")]
    [DataRow("12x")]
    public void PositiveWholeNumberFieldRejectsInvalidText(string input)
    {
        var field = new NumberField
        {
            Minimum = 1,
            Maximum = int.MaxValue,
            FormatString = "0",
            Value = 5
        };

        field.Input.Text = input;

        Assert.IsFalse(field.IsInputValid);
        Assert.AreEqual(0m, field.Value);
        Assert.AreEqual(string.Empty, field.Input.Text);
    }

    [TestMethod]
    public void ValidWholeNumberClearsInvalidState()
    {
        var field = new NumberField { Minimum = 1, Maximum = int.MaxValue, FormatString = "0", Value = 5 };
        field.Input.Text = "letters";

        field.Input.Text = "12";

        Assert.IsTrue(field.IsInputValid);
        Assert.AreEqual(12m, field.Value);
        Assert.AreEqual("12", field.Input.Text);
    }

    [TestMethod]
    public void DecimalFieldAllowsConfiguredPrecisionButRejectsExtraDigits()
    {
        var field = new NumberField { Minimum = 0, FormatString = "0.00", Value = 0 };

        field.Input.Text = "12.50";

        Assert.IsTrue(field.IsInputValid);
        Assert.AreEqual(12.50m, field.Value);

        field.Input.Text = "12.505";

        Assert.IsFalse(field.IsInputValid);
        Assert.AreEqual(-1m, field.Value);
    }
}
