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
}
