using AvaloniaApp.Services;
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

    [TestMethod]
    public void CatalogProductUsesSelectedUnitPriceAndDisplayAvailability()
    {
        var productId = Guid.NewGuid();
        var unit = new ProductUnitResponse(Guid.NewGuid(), null, "Case", 2, 125, 100, false, true);
        var product = new PosProductResponse(productId, "Supplier", "SKU-1", null, "Engine Oil", "Can",
            100, 90, 6, 1, [unit], unit);
        var line = CustomerAccountChargeLineViewModel.Product(() => { }, product, unit);

        line.Quantity = 3;

        Assert.AreEqual(ApiCustomerAccountChargeLineSource.Product, line.SourceType);
        Assert.AreEqual(375m, line.Amount);
        Assert.AreEqual(6, line.RequiredBasePieces);
        Assert.IsTrue(line.IsValid);
        line.Quantity = 4;
        Assert.IsFalse(line.IsValid);
    }

    [TestMethod]
    public void FuelUsesCurrentPriceAndLiterUnit()
    {
        var fuel = new FuelProductResponse(Guid.NewGuid(), "Diesel", 1, true, 88.330m, DateTime.UtcNow,
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), DateTime.UtcNow);

        var line = CustomerAccountChargeLineViewModel.Fuel(() => { }, fuel);
        line.Quantity = 10;

        Assert.AreEqual(ApiCustomerAccountChargeLineSource.Fuel, line.SourceType);
        Assert.AreEqual("L", line.Unit);
        Assert.AreEqual(883.30m, line.Amount);
        Assert.IsTrue(line.IsValid);
    }
}
