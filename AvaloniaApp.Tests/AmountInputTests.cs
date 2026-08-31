using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class AmountInputTests
{
    [TestMethod]
    public void StartsBlankWithoutPlaceholder()
    {
        var input = new AmountInput();

        Assert.AreEqual(string.Empty, input.Text ?? string.Empty);
        Assert.IsNull(input.PlaceholderText);
        Assert.IsNull(input.Value);
    }

    [TestMethod]
    public void ValidTextUpdatesAmount()
    {
        var input = new AmountInput { Text = "125.50" };

        Assert.AreEqual(125.50m, input.Value);
    }

    [TestMethod]
    public void ValueUsesCurrencyPrecision()
    {
        var input = new AmountInput { Value = 200m };

        Assert.AreEqual("200.00", input.Text);
    }

    [TestMethod]
    public void InvalidTextDoesNotReplaceAmount()
    {
        var input = new AmountInput { Value = 100m };

        input.Text = "100.999";

        Assert.AreEqual(100m, input.Value);
        Assert.AreEqual("100.00", input.Text);
    }
}
