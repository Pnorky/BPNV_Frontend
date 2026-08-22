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
        viewModel.SelectedMovementType = viewModel.MovementTypes.Single(item => item.Value == "Receipt");
        await viewModel.ApplyHistoryFiltersCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.IsHistoryFiltered);
        StringAssert.Contains(movementRequests[^1].Query, "search=coffee");
        StringAssert.Contains(movementRequests[^1].Query, "movementType=Receipt");
        StringAssert.Contains(movementRequests[^1].Query, "reference=DR-1");

        await viewModel.NextHistoryPageCommand.ExecuteAsync(null);

        Assert.AreEqual(2, viewModel.HistoryPage);
        StringAssert.Contains(movementRequests[^1].Query, "page=2");
    }

    private static async Task WaitUntilIdle(ApiStockMovementsViewModel viewModel)
    {
        while (viewModel.IsBusy || viewModel.IsHistoryLoading) await Task.Delay(5);
    }

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
