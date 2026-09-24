using System.Net;
using System.Net.Http.Json;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class StockMovementsViewModelTests
{
    [TestMethod]
    public async Task AppliesHistoryFiltersAndLoadsNextServerPage()
    {
        var movementRequests = new List<Uri>();
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/login"))
                return Json(new TokenResponse(
                    "access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
                    new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory User", ["Inventory"], false)));
            if (path.EndsWith("/products")) return Json(new PagedResponse<ProductResponse>([], 1, 200, 0));
            if (path.EndsWith("/stock-movements"))
            {
                movementRequests.Add(request.RequestUri);
                var page = request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal) ? 2 : 1;
                return Json(new PagedResponse<StockMovementResponse>([], page, 20, 21));
            }
            throw new InvalidOperationException(request.RequestUri.ToString());
        });
        var auth = new AuthApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") },
            new AuthSession());
        await auth.LoginAsync("inventory", "password");
        var viewModel = new ApiStockMovementsViewModel(new StoreApiClient(auth), new TestNotificationService());
        await WaitUntilIdle(viewModel);

        viewModel.HistorySearchText = "coffee";
        viewModel.HistoryReference = "DR-1";
        viewModel.HistoryFromDate = StoreDateTime.AtStoreMidnight(new DateTime(2025, 8, 26));
        viewModel.HistoryToDate = StoreDateTime.AtStoreMidnight(new DateTime(2025, 8, 27));
        viewModel.SelectedMovementType = viewModel.MovementTypes.Single(item => item.Value == "Receipt");
        await viewModel.ApplyHistoryFiltersCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.IsHistoryFiltered);
        StringAssert.Contains(movementRequests[^1].Query, "search=coffee");
        StringAssert.Contains(movementRequests[^1].Query, "movementType=Receipt");
        StringAssert.Contains(movementRequests[^1].Query, "reference=DR-1");
        var query = Uri.UnescapeDataString(movementRequests[^1].Query);
        StringAssert.Contains(query, "fromUtc=2025-08-25T16:00:00.0000000+00:00");
        StringAssert.Contains(query, "toUtcExclusive=2025-08-27T16:00:00.0000000+00:00");

        await viewModel.NextHistoryPageCommand.ExecuteAsync(null);

        Assert.AreEqual(2, viewModel.HistoryPage);
        StringAssert.Contains(movementRequests[^1].Query, "page=2");
    }

    [TestMethod]
    public async Task PerishableSpoilageLoadsEligibleLotsAndOtherRequiresNotes()
    {
        var productId = Guid.NewGuid();
        var unit = new ProductUnitResponse(Guid.NewGuid(), "0001", "piece", 1, 10, 10, true, true);
        var product = new ProductResponse(
            productId, Guid.NewGuid(), "Supplier", ApiInventoryItemType.Merchandise, "SKU", "0001", "Milk", "Dairy", "piece",
            8, 10, 10, 1, 1, 2, 2, 2, 3, 5, false, false, 0, 1, true, [unit], true, true);
        var lot = new InventoryLotBalanceResponse(
            Guid.NewGuid(), productId, unit.Id, "LOT-1", ApiInventoryStockLocation.Display, 2,
            DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow.AddDays(1), false, false);
        var spoilageCalls = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/login"))
                return Json(new TokenResponse("access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
                    new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory", ["Inventory"], false)));
            if (path.EndsWith("/products")) return Json(new PagedResponse<ProductResponse>([product], 1, 200, 1));
            if (path.EndsWith("/lots")) return Json<IReadOnlyList<InventoryLotBalanceResponse>>([lot]);
            if (path.EndsWith("/stock-movements")) return Json(new PagedResponse<StockMovementResponse>([], 1, 20, 0));
            if (path.EndsWith("/spoilage")) spoilageCalls++;
            throw new InvalidOperationException(path);
        });
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, new AuthSession());
        await auth.LoginAsync("inventory", "password");
        var notifications = new TestNotificationService();
        var viewModel = new ApiStockMovementsViewModel(new StoreApiClient(auth), notifications);
        await WaitUntilIdle(viewModel);

        viewModel.SpoilageProduct = product;
        while (viewModel.IsLoadingLots) await Task.Delay(5);
        viewModel.SpoilageReason = ApiSpoilageReason.Other;
        viewModel.SpoilageNotes = "";
        await viewModel.RecordSpoilageCommand.ExecuteAsync(null);

        Assert.AreEqual(lot.LotId, viewModel.SpoilageLot!.LotId);
        Assert.AreEqual(0, spoilageCalls);
        StringAssert.Contains(notifications.Notifications[^1].Message, "Notes are required");
    }

    private static async Task WaitUntilIdle(ApiStockMovementsViewModel viewModel)
    {
        while (viewModel.IsBusy || viewModel.IsHistoryLoading) await Task.Delay(5);
    }

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
