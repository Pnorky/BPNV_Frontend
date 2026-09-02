using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class StockReceivingSelectionTests
{
    [TestMethod]
    public void BarcodeLessSupplyCanBeSelectedForReceivingByCatalog()
    {
        var api = new StoreApiClient(new AuthApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new AssertFailedException("No API request expected.")))
            {
                BaseAddress = new Uri("https://test/")
            },
            new AuthSession()));
        var productId = Guid.NewGuid();
        var baseUnit = new ProductUnitResponse(productId, null, "piece", 1, 0, 0, true, true);
        var product = new ProductResponse(
            productId, Guid.NewGuid(), "Supplier", ApiInventoryItemType.Supply,
            "SUP-CUP", null, "Disposable cup", "Supplies", "piece",
            1.25m, 0, 0, 5, 10, 10, 10,
            0, 100, 100, false, false, 0, 1, true, [baseUnit]);
        var viewModel = new StockReceivingViewModel(api, new TestNotificationService());

        viewModel.CatalogLookupSelection = product;

        Assert.AreEqual(productId, viewModel.SelectedProduct!.Id);
        Assert.AreEqual(baseUnit, viewModel.SelectedUnit);
        Assert.AreEqual(1.25m, viewModel.UnitCost);
        StringAssert.Contains(viewModel.StatusMessage, "catalog search");
    }
}
