using System.Net;
using System.Net.Http.Json;
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

    [TestMethod]
    public void ZeroUnitEmployeePriceDefaultsToRegularPriceWhenReceiving()
    {
        var api = new StoreApiClient(new AuthApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new AssertFailedException("No API request expected.")))
            {
                BaseAddress = new Uri("https://test/")
            },
            new AuthSession()));
        var productId = Guid.NewGuid();
        var baseUnit = new ProductUnitResponse(productId, "750515031043", "piece", 1, 12, 0, true, true);
        var product = new ProductResponse(
            productId, Guid.NewGuid(), "Shoppers", ApiInventoryItemType.Merchandise,
            "MER-SKYFLA", "750515031043", "SkyFlakes Condensada", "Snacks", "piece",
            10, 12, 0, 5, 20, 10, 20,
            0, 0, 0, true, true, 20, 1, true, [baseUnit]);
        var viewModel = new StockReceivingViewModel(api, new TestNotificationService());

        viewModel.CatalogLookupSelection = product;

        Assert.AreEqual(12m, viewModel.EmployeePrice);
    }

    [TestMethod]
    public async Task InvalidCountShowsErrorAndDoesNotCallApi()
    {
        var requests = 0;
        var api = new StoreApiClient(new AuthApiClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
            {
                requests++;
                throw new AssertFailedException("No API request expected.");
            }))
            {
                BaseAddress = new Uri("https://test/")
            },
            new AuthSession()));
        var notifications = new TestNotificationService();
        var productId = Guid.NewGuid();
        var baseUnit = new ProductUnitResponse(productId, "750515031043", "piece", 1, 12, 12, true, true);
        var product = new ProductResponse(
            productId, Guid.NewGuid(), "Shoppers", ApiInventoryItemType.Merchandise,
            "MER-SKYFLA", "750515031043", "SkyFlakes Condensada", "Snacks", "piece",
            10, 12, 12, 5, 20, 10, 20,
            0, 0, 0, true, true, 20, 1, true, [baseUnit]);
        var viewModel = new StockReceivingViewModel(api, notifications)
        {
            CatalogLookupSelection = product,
            Count = 0
        };

        await viewModel.SubmitReceiptCommand.ExecuteAsync(null);

        Assert.AreEqual(0, requests);
        Assert.AreEqual("Error", notifications.Notifications.Single().Type);
        StringAssert.Contains(notifications.Notifications.Single().Message, "greater than zero");
    }

    [TestMethod]
    public async Task SuccessfulReceiptClearsProductSelectionAndReceiptNumber()
    {
        var session = new AuthSession();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new TokenResponse(
                        "access", DateTime.UtcNow.AddMinutes(10), "refresh", DateTime.UtcNow.AddDays(1),
                        new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory", ["Inventory"], false)))
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new StockReceiptResponse(
                    Guid.NewGuid(), productId, unitId, "piece", 2, 1, 2, 0, 2, 2, DateTime.UtcNow))
            };
        });
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, session);
        await auth.LoginAsync("inventory", "password");
        var baseUnit = new ProductUnitResponse(unitId, "barcode", "piece", 1, 12, 12, true, true);
        var product = new ProductResponse(
            productId, Guid.NewGuid(), "Supplier", ApiInventoryItemType.Merchandise,
            "SKU", "barcode", "Product", "Snacks", "piece", 10, 12, 12,
            5, 10, 10, 10, 0, 0, 0, true, true, 10, 1, true, [baseUnit]);
        var viewModel = new StockReceivingViewModel(new StoreApiClient(auth), new TestNotificationService())
        {
            CatalogLookupSelection = product,
            Count = 2,
            Reference = "INV-001",
            Notes = "Delivery"
        };

        await viewModel.SubmitReceiptCommand.ExecuteAsync(null);

        Assert.IsNull(viewModel.CatalogLookupSelection);
        Assert.IsNull(viewModel.SelectedProduct);
        Assert.AreEqual(string.Empty, viewModel.Reference);
        Assert.AreEqual(string.Empty, viewModel.Notes);
    }
}
