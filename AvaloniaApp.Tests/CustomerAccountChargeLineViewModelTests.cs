using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class CustomerAccountChargeLineViewModelTests
{
    [TestMethod]
    public void QuantityAndPriceCalculateRoundedAmountAndNotifyParent()
    {
        var changes = 0;
        var line = new CustomerAccountChargeLineViewModel(() => changes++)
        {
            Particular = "Diesel",
            Unit = "L",
            Quantity = 1000,
            UnitPrice = 10.125m
        };

        Assert.AreEqual(10125m, line.Amount);
        Assert.AreEqual("₱10,125.00", line.AmountDisplay);
        Assert.IsTrue(line.IsValid);
        Assert.IsTrue(changes >= 4);
    }

    [TestMethod]
    public void MissingParticularOrUnitIsInvalid()
    {
        var line = new CustomerAccountChargeLineViewModel(() => { }) { Quantity = 6, UnitPrice = 500 };
        Assert.IsFalse(line.IsValid);
        line.Particular = "Lubricant";
        line.Unit = "Can";
        Assert.IsTrue(line.IsValid);
    }
}
