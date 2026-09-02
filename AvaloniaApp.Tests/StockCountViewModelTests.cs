using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class StockCountViewModelTests
{
    [TestMethod]
    public void BuildsBodegaDecreaseFromPhysicalRemainingQuantity()
    {
        var product = Product(ApiInventoryItemType.Supply, display: 10, bodega: 100);
        var viewModel = new StockCountViewModel([product], product) { CountedQuantity = 75, Notes = " Weekly count " };

        var valid = viewModel.TryBuildRequest(out var request, out var error);

        Assert.IsTrue(valid, error);
        Assert.IsNotNull(request);
        Assert.AreEqual(ApiInventoryStockLocation.Bodega, request.Location);
        Assert.AreEqual(75, request.CountedQuantity);
        Assert.AreEqual(-25, viewModel.Variance);
        Assert.AreEqual(7UL, request.ExpectedProductVersion);
        Assert.AreEqual("Weekly count", request.Notes);
    }

    [TestMethod]
    public void LocationChangeRefreshesBalanceAndNoChangeIsRejected()
    {
        var product = Product(ApiInventoryItemType.Consumable, display: 8, bodega: 20);
        var viewModel = new StockCountViewModel([product], product);

        viewModel.SelectedLocation = viewModel.Locations.Single(location => location.Location == ApiInventoryStockLocation.Display);

        Assert.AreEqual(8, viewModel.CurrentQuantity);
        Assert.AreEqual(8m, viewModel.CountedQuantity);
        Assert.IsFalse(viewModel.TryBuildRequest(out _, out var error));
        StringAssert.Contains(error, "already matches");
    }

    [TestMethod]
    public void MerchandiseIsNotAvailableForPeriodicStockCount()
    {
        var merchandise = Product(ApiInventoryItemType.Merchandise, display: 5, bodega: 10);

        var viewModel = new StockCountViewModel([merchandise], merchandise);

        Assert.IsEmpty(viewModel.Products);
        Assert.IsNull(viewModel.SelectedProduct);
    }

    private static ProductResponse Product(ApiInventoryItemType itemType, int display, int bodega)
    {
        var id = Guid.NewGuid();
        return new ProductResponse(
            id, Guid.NewGuid(), "Supplier", itemType, "SKU-1", null, "Product", "Category", "piece",
            1, 0, 0, 5, 10, 10, 10, display, bodega, display + bodega,
            false, false, 0, 7, true,
            [new ProductUnitResponse(id, null, "piece", 1, 0, 0, true, true)]);
    }
}
