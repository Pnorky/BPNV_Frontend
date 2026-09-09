using System.Net;
using System.Net.Http.Json;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class DeliveryHistoryViewModelTests
{
    [TestMethod]
    public async Task AppliesServerFiltersPagesAndCachesExpandedDetail()
    {
        var listRequests = new List<Uri>();
        var detailCalls = 0;
        var supplierId = Guid.NewGuid();
        var first = Delivery("INV-1", supplierId);
        var second = Delivery("INV-2", supplierId);
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/login")) return Json(Tokens());
            if (path.EndsWith($"/{first.BatchId}") || path.EndsWith($"/{second.BatchId}"))
            {
                detailCalls++;
                var item = path.EndsWith($"/{first.BatchId}") ? first : second;
                return Json(new DeliveryHistoryDetailResponse(item, "Snapshot notes", [Line(supplierId)]));
            }
            if (path == "/api/stock-receipts/batches")
            {
                listRequests.Add(request.RequestUri);
                var page = request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal) ? 2 : 1;
                return Json(new PagedResponse<DeliveryHistoryItemResponse>([first, second], page, 20, 21));
            }
            throw new InvalidOperationException(request.RequestUri.ToString());
        });
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.ReceiptNumberSearch = " INV-1 ";
        viewModel.SelectedSupplier = new SupplierResponse(supplierId, "Supplier snapshot", null, null, false);
        viewModel.FromDate = StoreDateTime.AtStoreMidnight(new DateTime(2025, 8, 26));
        viewModel.ToDate = StoreDateTime.AtStoreMidnight(new DateTime(2025, 8, 27));

        await viewModel.ApplyFiltersAsync();

        Assert.IsTrue(viewModel.IsFiltered);
        var decoded = Uri.UnescapeDataString(listRequests[^1].Query);
        StringAssert.Contains(decoded, "receiptNumber=INV-1");
        StringAssert.Contains(decoded, $"supplierId={supplierId}");
        StringAssert.Contains(decoded, "fromUtc=2025-08-25T16:00:00.0000000+00:00");
        StringAssert.Contains(decoded, "toUtcExclusive=2025-08-27T16:00:00.0000000+00:00");

        var firstRow = viewModel.Rows[0];
        await viewModel.ToggleDetailsCommand.ExecuteAsync(firstRow);
        Assert.IsTrue(firstRow.IsExpanded);
        Assert.AreEqual("Snapshot notes", firstRow.Detail!.Notes);
        await viewModel.ToggleDetailsCommand.ExecuteAsync(firstRow);
        await viewModel.ToggleDetailsCommand.ExecuteAsync(firstRow);
        Assert.AreEqual(1, detailCalls);

        var secondRow = viewModel.Rows[1];
        await viewModel.ToggleDetailsCommand.ExecuteAsync(secondRow);
        Assert.IsFalse(firstRow.IsExpanded);
        Assert.IsTrue(secondRow.IsExpanded);
        Assert.AreEqual(2, detailCalls);

        await viewModel.NextPageCommand.ExecuteAsync(null);
        Assert.AreEqual(2, viewModel.Page);
        StringAssert.Contains(listRequests[^1].Query, "page=2");
        Assert.IsFalse(viewModel.Rows.Any(row => row.IsExpanded));
    }

    [TestMethod]
    public async Task DraftSearchIsNotSentUntilAppliedAndClearRemovesIt()
    {
        var listRequests = new List<Uri>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            listRequests.Add(request.RequestUri);
            return Json(new PagedResponse<DeliveryHistoryItemResponse>([], 1, 20, 0));
        });
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.ReceiptNumberSearch = "INV-9";

        await viewModel.LoadAsync();
        await viewModel.ApplyFiltersAsync();
        await viewModel.ClearFiltersAsync();

        Assert.IsFalse(listRequests[0].Query.Contains("receiptNumber", StringComparison.Ordinal));
        StringAssert.Contains(Uri.UnescapeDataString(listRequests[1].Query), "receiptNumber=INV-9");
        Assert.IsFalse(listRequests[2].Query.Contains("receiptNumber", StringComparison.Ordinal));
        Assert.IsFalse(viewModel.IsFiltered);
        Assert.AreEqual("No deliveries available", viewModel.StateTitle);
    }

    [TestMethod]
    public async Task DetailFailureKeepsDeliveryOpenAndSupportsRetry()
    {
        var detailCalls = 0;
        var delivery = Delivery("INV-1", Guid.NewGuid());
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/login")) return Json(Tokens());
            if (path.EndsWith($"/{delivery.BatchId}"))
            {
                detailCalls++;
                if (detailCalls == 1)
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        Content = JsonContent.Create(new ApiProblemDetails(null, "Unavailable", 503, "Try again.", null, null))
                    };
                return Json(new DeliveryHistoryDetailResponse(delivery, null, [Line(Guid.NewGuid())]));
            }
            return Json(new PagedResponse<DeliveryHistoryItemResponse>([delivery], 1, 20, 1));
        });
        var viewModel = await CreateViewModelAsync(handler);
        await viewModel.LoadAsync();
        var row = viewModel.Rows.Single();

        await viewModel.ToggleDetailsCommand.ExecuteAsync(row);

        Assert.IsTrue(row.IsExpanded);
        Assert.IsTrue(row.HasDetailError);
        Assert.IsNull(row.Detail);

        await viewModel.RetryDetailCommand.ExecuteAsync(row);

        Assert.IsTrue(row.IsExpanded);
        Assert.IsNotNull(row.Detail);
        Assert.IsFalse(row.HasDetailError);
        Assert.AreEqual(2, detailCalls);
    }

    [TestMethod]
    public async Task LoadingDetailCanBeCollapsedImmediately()
    {
        var delivery = Delivery("INV-1", Guid.NewGuid());
        var detailStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDetail = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new AsyncStubHttpMessageHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/login")) return Json(Tokens());
            if (path.EndsWith($"/{delivery.BatchId}"))
            {
                detailStarted.SetResult();
                await releaseDetail.Task;
                return Json(new DeliveryHistoryDetailResponse(delivery, null, []));
            }
            return Json(new PagedResponse<DeliveryHistoryItemResponse>([delivery], 1, 20, 1));
        });
        var viewModel = await CreateViewModelAsync(handler);
        await viewModel.LoadAsync();
        var row = viewModel.Rows.Single();

        var loading = viewModel.ToggleDetailsCommand.ExecuteAsync(row);
        await detailStarted.Task;
        await viewModel.ToggleDetailsCommand.ExecuteAsync(row);

        Assert.IsFalse(row.IsExpanded);
        releaseDetail.SetResult();
        await loading;
        Assert.IsNotNull(row.Detail);
    }

    [TestMethod]
    public async Task FailedNextPageKeepsCurrentRowsAndPage()
    {
        var delivery = Delivery("INV-1", Guid.NewGuid());
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            if (request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = JsonContent.Create(new ApiProblemDetails(null, "Unavailable", 503, "Try again.", null, null))
                };
            return Json(new PagedResponse<DeliveryHistoryItemResponse>([delivery], 1, 20, 21));
        });
        var viewModel = await CreateViewModelAsync(handler);
        await viewModel.LoadAsync();
        var originalRow = viewModel.Rows.Single();

        await viewModel.NextPageCommand.ExecuteAsync(null);

        Assert.AreEqual(1, viewModel.Page);
        Assert.AreSame(originalRow, viewModel.Rows.Single());
        Assert.IsTrue(viewModel.HasListError);
    }

    private static async Task<DeliveryHistoryViewModel> CreateViewModelAsync(HttpMessageHandler handler)
    {
        var auth = new AuthApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") },
            new AuthSession());
        await auth.LoginAsync("inventory", "password");
        return new DeliveryHistoryViewModel(new StoreApiClient(auth), new TestNotificationService(), false);
    }

    private static DeliveryHistoryItemResponse Delivery(string reference, Guid supplierId) => new(
        Guid.NewGuid(), reference, ["Supplier snapshot"], 2, 1, 1, 24, 120m,
        new DateTime(2025, 8, 26, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2025, 8, 26, 0, 1, 0, DateTimeKind.Utc),
        Guid.NewGuid(), "Inventory User", "Completed");

    private static DeliveryHistoryLineResponse Line(Guid supplierId) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Product snapshot", "SKU-1", supplierId,
        "Supplier snapshot", "Scanner supplier", "0001", [1, 2], 2, "case", 12, 24,
        4m, 5m, 6m, 7m, 5m, 6m, 120m, null, null, false);

    private static TokenResponse Tokens() => new(
        "access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
        new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory User", ["Inventory"], false));

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
