using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class BatchReceivingViewModelTests
{
    [TestMethod]
    public async Task DraftEditsInvalidatePreviewWithoutChangingIdempotencyKey()
    {
        var key = Guid.Empty;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            var body = request.Content!.ReadFromJsonAsync<BatchReceiptRequest>().GetAwaiter().GetResult()!;
            key = body.IdempotencyKey;
            var row = new BatchReceiptPreviewRowResponse(
                [1], "Supplier A", "0001", Guid.NewGuid(), "Supplier A", Guid.NewGuid(), "Coffee", "SKU-1",
                Guid.NewGuid(), "piece", 2, 1, 2, 5, 7, "Valid", []);
            return Json(new BatchReceiptValidationResponse(
                body.IdempotencyKey, body.Reference, body.Notes, true, [row], [],
                new BatchReceiptValidationSummaryResponse(1, 1, 1, 2, 0, 0, 0)));
        });
        var session = new AuthSession();
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, session);
        await auth.LoginAsync("inventory", "password");
        var viewModel = new BatchReceivingViewModel(new StoreApiClient(auth), new TestNotificationService())
        {
            CaptureText = "Supplier A\t0001\t2"
        };
        var previewAssignments = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(BatchReceivingViewModel.PreviewRows)) previewAssignments++;
        };
        var draftKey = viewModel.IdempotencyKey;

        await viewModel.ReviewBatchAsync();

        Assert.IsTrue(viewModel.CanCommit);
        Assert.IsTrue(viewModel.HasPreview);
        Assert.AreEqual(draftKey, key);
        Assert.AreEqual(1, viewModel.PreviewRows.Count);
        Assert.AreEqual(1, previewAssignments);

        viewModel.Reference = "DR-2";

        Assert.IsFalse(viewModel.CanCommit);
        Assert.AreEqual(0, viewModel.PreviewRows.Count);
        Assert.AreEqual(2, previewAssignments);
        Assert.AreEqual(draftKey, viewModel.IdempotencyKey);
    }

    [TestMethod]
    public async Task AutomaticDeliveryTimeSendsNullOverride()
    {
        BatchReceiptRequest? sent = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            sent = request.Content!.ReadFromJsonAsync<BatchReceiptRequest>().GetAwaiter().GetResult();
            return Json(ValidResponse(sent!));
        });
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.CaptureText = "Supplier A\t0001\t2";

        await viewModel.ReviewBatchAsync();

        Assert.IsTrue(viewModel.UseCommitTime);
        Assert.IsNull(sent!.DeliveryAtUtc);

        viewModel.UseCommitTime = false;

        Assert.IsNotNull(viewModel.DeliveryDate);
        Assert.IsNotNull(viewModel.DeliveryTime);
        Assert.IsFalse(viewModel.CanCommit);
        Assert.IsFalse(viewModel.HasPreview);
    }

    [TestMethod]
    public async Task ManualDeliveryTimeIsFrozenThroughCommitAndSuccessUsesServerTimes()
    {
        var requests = new List<BatchReceiptRequest>();
        var deliveredAt = new DateTime(2025, 8, 26, 0, 0, 0, DateTimeKind.Utc);
        var recordedAt = deliveredAt.AddMinutes(2);
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            var body = request.Content!.ReadFromJsonAsync<BatchReceiptRequest>().GetAwaiter().GetResult()!;
            requests.Add(body);
            if (request.RequestUri.AbsolutePath.EndsWith("/validate")) return Json(ValidResponse(body));
            return Json(new BatchReceiptResponse(
                Guid.NewGuid(), body.IdempotencyKey, "DR-1", 1, 1, 1, 2, ["Supplier A"],
                recordedAt, deliveredAt, false));
        });
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.CaptureText = "Supplier A\t0001\t2";
        viewModel.UseCommitTime = false;
        Assert.IsNotNull(viewModel.DeliveryDate);
        Assert.IsNotNull(viewModel.DeliveryTime);
        viewModel.DeliveryDate = StoreDateTime.AtStoreMidnight(new DateTime(2025, 8, 26));
        viewModel.DeliveryTime = new TimeSpan(8, 0, 0);

        await viewModel.ReviewBatchAsync();
        await viewModel.SubmitValidatedBatchAsync();

        Assert.HasCount(2, requests);
        Assert.AreEqual(new DateTimeOffset(deliveredAt), requests[0].DeliveryAtUtc);
        Assert.AreEqual(requests[0].DeliveryAtUtc, requests[1].DeliveryAtUtc);
        Assert.IsTrue(viewModel.UseCommitTime);
        Assert.AreEqual("August 26, 2025 8:00 AM", viewModel.Result!.DeliveryAtDisplay);
        Assert.AreEqual("August 26, 2025 8:02 AM", viewModel.Result.CompletedAtDisplay);
    }

    [TestMethod]
    public async Task MissingManualDeliveryDateBlocksReview()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/login")
            ? Json(Tokens())
            : throw new InvalidOperationException("An incomplete manual timestamp must not call the batch API."));
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.CaptureText = "Supplier A\t0001\t2";
        viewModel.UseCommitTime = false;
        viewModel.DeliveryDate = null;

        await viewModel.ReviewBatchAsync();

        Assert.IsFalse(viewModel.CanCommit);
        StringAssert.Contains(viewModel.StatusMessage, "Select both the Philippine delivery date and time");
    }

    [TestMethod]
    public async Task InvalidCaptureAppendedDuringValidationCannotPublishPreview()
    {
        var validationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseValidation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new AsyncStubHttpMessageHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            var body = await request.Content!.ReadFromJsonAsync<BatchReceiptRequest>() ?? throw new InvalidOperationException();
            validationStarted.SetResult();
            await releaseValidation.Task;
            return Json(ValidResponse(body));
        });
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.CaptureText = "Supplier A\t0001\t2";

        var review = viewModel.ReviewBatchAsync();
        await validationStarted.Task;

        Assert.IsTrue(viewModel.IsBusy);
        Assert.IsFalse(viewModel.CanEdit);
        viewModel.CaptureText += "\tBROKEN";
        releaseValidation.SetResult();
        await review;

        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsTrue(viewModel.CanEdit);
        Assert.IsFalse(viewModel.CanCommit);
        Assert.IsFalse(viewModel.HasPreview);
        Assert.AreEqual(0, viewModel.PreviewRows.Count);
        StringAssert.Contains(viewModel.CaptureText, "BROKEN");
    }

    [TestMethod]
    public async Task CommitRetryKeepsKeyAndPostAttemptEditRotatesKey()
    {
        var commitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attemptedKeys = new List<Guid>();
        var handler = new AsyncStubHttpMessageHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            var body = await request.Content!.ReadFromJsonAsync<BatchReceiptRequest>() ?? throw new InvalidOperationException();
            if (request.RequestUri.AbsolutePath.EndsWith("/validate")) return Json(ValidResponse(body));

            attemptedKeys.Add(body.IdempotencyKey);
            if (attemptedKeys.Count == 1)
            {
                commitStarted.SetResult();
                await releaseCommit.Task;
            }
            throw new HttpRequestException("Connection lost after submission.");
        });
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.CaptureText = "Supplier A\t0001\t2";
        await viewModel.ReviewBatchAsync();
        var validatedKey = viewModel.IdempotencyKey;

        var firstAttempt = viewModel.SubmitValidatedBatchAsync();
        await commitStarted.Task;
        Assert.IsFalse(viewModel.CanEdit);
        releaseCommit.SetResult();
        await firstAttempt;

        Assert.IsTrue(viewModel.CanCommit);
        Assert.IsFalse(viewModel.CanReview);
        Assert.AreEqual(validatedKey, viewModel.IdempotencyKey);
        await viewModel.SubmitValidatedBatchAsync();

        Assert.AreEqual(2, attemptedKeys.Count);
        Assert.IsTrue(attemptedKeys.All(key => key == validatedKey));
        Assert.AreEqual(validatedKey, viewModel.IdempotencyKey);
        Assert.IsTrue(viewModel.CanCommit);
        Assert.IsFalse(viewModel.CanReview);

        viewModel.Notes = "Changed after uncertain submission";

        Assert.AreNotEqual(validatedKey, viewModel.IdempotencyKey);
        Assert.IsFalse(viewModel.CanCommit);
        Assert.IsTrue(viewModel.CanReview);
    }

    [TestMethod]
    public async Task DefiniteApiCommitFailurePermitsReviewWithPreservedKey()
    {
        var validationCalls = 0;
        var handler = new AsyncStubHttpMessageHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            var body = await request.Content!.ReadFromJsonAsync<BatchReceiptRequest>() ?? throw new InvalidOperationException();
            if (request.RequestUri.AbsolutePath.EndsWith("/validate"))
            {
                validationCalls++;
                return Json(ValidResponse(body));
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new ApiProblemDetails(
                    null, "Validation failed", 400, "Stock changed before commit.", null, null))
            };
        });
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.CaptureText = "Supplier A\t0001\t2";
        await viewModel.ReviewBatchAsync();
        var validatedKey = viewModel.IdempotencyKey;

        await viewModel.SubmitValidatedBatchAsync();

        Assert.IsTrue(viewModel.CanReview);
        Assert.IsFalse(viewModel.CanCommit);
        Assert.AreEqual(validatedKey, viewModel.IdempotencyKey);

        await viewModel.ReviewBatchAsync();

        Assert.AreEqual(2, validationCalls);
        Assert.IsTrue(viewModel.CanCommit);
        Assert.AreEqual(validatedKey, viewModel.IdempotencyKey);
    }

    [TestMethod]
    public async Task LocalParseFailureUsesBlockingErrorNotification()
    {
        var notifications = new TestNotificationService();
        var viewModel = await CreateViewModelAsync(new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/login")
                ? Json(Tokens())
                : throw new InvalidOperationException("Invalid capture must not call the batch API.")), notifications);
        viewModel.CaptureText = "SHOPPERS\t4.80002E+12\t2";

        await viewModel.ReviewBatchAsync();

        Assert.IsFalse(viewModel.CanCommit);
        Assert.AreEqual("Error", viewModel.Issues.Single().Severity);
        Assert.AreEqual("Error", notifications.Notifications.Single().Type);
    }

    [TestMethod]
    public async Task WarningOnlyValidationKeepsCommitEnabledAndDisplaysSeverity()
    {
        var notifications = new TestNotificationService();
        var warning = new BatchReceiptIssueResponse(
            "supplierLibraryMismatch", "supplierLibrary", 1,
            "Scanner library differs; receipt uses registered supplier MEGABUCKS.", "Warning");
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            var body = request.Content!.ReadFromJsonAsync<BatchReceiptRequest>().GetAwaiter().GetResult()!;
            var row = new BatchReceiptPreviewRowResponse(
                [1], "SHOPPERS", "0001", Guid.NewGuid(), "MEGABUCKS", Guid.NewGuid(), "Coffee", "SKU-1",
                Guid.NewGuid(), "piece", 2, 1, 2, 5, 7, "Warning", [warning]);
            return Json(new BatchReceiptValidationResponse(
                body.IdempotencyKey, body.Reference, body.Notes, true, [row], [warning],
                new BatchReceiptValidationSummaryResponse(1, 1, 1, 2, 1, 0, 1)));
        });
        var viewModel = await CreateViewModelAsync(handler, notifications);
        viewModel.CaptureText = "SHOPPERS\t0001\t2";

        await viewModel.ReviewBatchAsync();

        Assert.IsTrue(viewModel.CanCommit);
        Assert.AreEqual("Warning", viewModel.Issues.Single().Severity);
        Assert.AreEqual("SHOPPERS", viewModel.PreviewRows.Single().SupplierLibrary);
        Assert.AreEqual("MEGABUCKS", viewModel.PreviewRows.Single().SupplierName);
        Assert.AreEqual("Warning", viewModel.PreviewRows.Single().Status);
        StringAssert.Contains(viewModel.StatusMessage, "passed with 1 warning");
        Assert.AreEqual("Warning", notifications.Notifications.Single().Type);
    }

    [TestMethod]
    public async Task MixedWarningAndErrorValidationDisablesCommitAndDisplaysSeverities()
    {
        var notifications = new TestNotificationService();
        var warning = new BatchReceiptIssueResponse(
            "supplierLibraryNotFound", "supplierLibrary", 1, "Scanner library was not found.", "Warning");
        var error = new BatchReceiptIssueResponse(
            "unknownBarcode", "barcode", 2, "Barcode does not match a product unit.", "Error");
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            var body = request.Content!.ReadFromJsonAsync<BatchReceiptRequest>().GetAwaiter().GetResult()!;
            var row = new BatchReceiptPreviewRowResponse(
                [2], "UNKNOWN", "0002", null, null, null, null, null, null, null,
                1, null, null, null, null, "Invalid", [error]);
            return Json(new BatchReceiptValidationResponse(
                body.IdempotencyKey, body.Reference, body.Notes, false, [row], [warning, error],
                new BatchReceiptValidationSummaryResponse(2, 1, 0, null, 1, 1, 2)));
        });
        var viewModel = await CreateViewModelAsync(handler, notifications);
        const string capture = "UNKNOWN\t0001\t1\nUNKNOWN\t0002\t1";
        viewModel.CaptureText = capture;

        await viewModel.ReviewBatchAsync();

        Assert.IsFalse(viewModel.CanCommit);
        CollectionAssert.AreEqual(new[] { "Warning", "Error" }, viewModel.Issues.Select(issue => issue.Severity).ToArray());
        Assert.AreEqual("Invalid", viewModel.PreviewRows.Single().Status);
        Assert.AreEqual(capture, viewModel.CaptureText);
        StringAssert.Contains(viewModel.StatusMessage, "1 blocking error");
        Assert.AreEqual("Error", notifications.Notifications.Single().Type);
    }

    [TestMethod]
    public async Task EditingPriceKeepsPreviewAndSendsVersionedUpdateOnReview()
    {
        var productId = Guid.NewGuid();
        BatchReceiptRequest? latestRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            latestRequest = request.Content!.ReadFromJsonAsync<BatchReceiptRequest>(EnumJsonOptions()).GetAwaiter().GetResult()!;
            var proposed = latestRequest.PriceUpdates?.SingleOrDefault();
            var row = new BatchReceiptPreviewRowResponse(
                [1], "Supplier A", "0001", Guid.NewGuid(), "Supplier A", productId, "Peanuts", "SKU-1",
                Guid.NewGuid(), "piece", 2, 1, 2, 5, 7, "Valid", [], false, null,
                10m, proposed?.CostPrice ?? 10m,
                20m, proposed?.RegularPrice ?? 20m,
                18m, proposed?.EmployeePrice ?? 18m,
                2 * (proposed?.CostPrice ?? 10m), 7);
            return Json(new BatchReceiptValidationResponse(
                latestRequest.IdempotencyKey, null, null, true, [row], [],
                new BatchReceiptValidationSummaryResponse(1, 1, 1, 2, 0, 0, 0, row.TotalCost)));
        });
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.CaptureText = "Supplier A\t0001\t2";
        await viewModel.ReviewBatchAsync();

        viewModel.PreviewRows.Single().CostPrice = 15m;

        Assert.IsFalse(viewModel.CanCommit);
        Assert.AreEqual(1, viewModel.PreviewRows.Count);
        Assert.AreEqual("₱30.00", viewModel.PreviewRows.Single().TotalCostDisplay);

        await viewModel.ReviewBatchAsync();

        var update = AssertExactlyOne(latestRequest!.PriceUpdates!);
        Assert.AreEqual(productId, update.ProductId);
        Assert.AreEqual(15m, update.CostPrice);
        Assert.AreEqual(7UL, update.ExpectedProductVersion);
        Assert.IsTrue(viewModel.CanCommit);
    }

    [TestMethod]
    public async Task NewProductDraftIsIncludedAndAutomaticallyRevalidated()
    {
        var supplierId = Guid.NewGuid();
        var validationCalls = 0;
        BatchReceiptRequest? latestRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            latestRequest = request.Content!.ReadFromJsonAsync<BatchReceiptRequest>(EnumJsonOptions()).GetAwaiter().GetResult()!;
            validationCalls++;
            if (latestRequest.NewProducts is null || latestRequest.NewProducts.Count == 0)
            {
                var unknown = new BatchReceiptPreviewRowResponse(
                    [1], "Supplier A", "new-code", null, null, null, null, null, null, null,
                    3, null, null, null, null, "Invalid",
                    [new("unknownBarcode", "barcode", 1, "Unknown barcode", "Error")]);
                return Json(new BatchReceiptValidationResponse(
                    latestRequest.IdempotencyKey, null, null, false, [unknown], unknown.Issues,
                    new BatchReceiptValidationSummaryResponse(1, 1, 0, null, 0, 1, 1)));
            }

            var draft = latestRequest.NewProducts.Single();
            var created = new BatchReceiptPreviewRowResponse(
                [1], "Supplier A", "new-code", supplierId, "Supplier A", null, draft.Name, draft.Sku, null, "piece",
                3, 1, 3, 0, 3, "Valid", [], true, draft.CorrelationId,
                null, draft.CostPrice, null, draft.RegularPrice, null, draft.EmployeePrice, 24m, 1);
            return Json(new BatchReceiptValidationResponse(
                latestRequest.IdempotencyKey, null, null, true, [created], [],
                new BatchReceiptValidationSummaryResponse(1, 1, 1, 3, 0, 0, 0, 24m)));
        });
        var viewModel = await CreateViewModelAsync(handler);
        viewModel.CaptureText = "Supplier A\tnew-code\t3";
        await viewModel.ReviewBatchAsync();
        var correlationId = Guid.NewGuid();
        var draftRequest = new BatchReceiptNewProductRequest(
            correlationId, "new-code", supplierId, ApiInventoryItemType.Merchandise,
            "NEW-1", "new-code", "New peanuts", "Snacks", "piece",
            8m, 12m, 10m, 5, 10, 10, 20, []);

        await viewModel.ApplyNewProductDraftAndReviewAsync(draftRequest);

        Assert.AreEqual(2, validationCalls);
        Assert.AreEqual(correlationId, latestRequest!.NewProducts!.Single().CorrelationId);
        Assert.IsTrue(viewModel.PreviewRows.Single().IsNewProduct);
        Assert.IsTrue(viewModel.CanCommit);
    }

    [TestMethod]
    public async Task SuccessfulCommitPublishesServerPriceChangeWarning()
    {
        var notifications = new TestNotificationService();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens());
            var body = request.Content!.ReadFromJsonAsync<BatchReceiptRequest>().GetAwaiter().GetResult()!;
            if (request.RequestUri.AbsolutePath.EndsWith("/validate")) return Json(ValidResponse(body));
            var recordedAt = new DateTime(2025, 8, 26, 5, 0, 0, DateTimeKind.Utc);
            return Json(new BatchReceiptResponse(
                Guid.NewGuid(), body.IdempotencyKey, "DR-1", 1, 1, 1, 2, ["Supplier A"], recordedAt, recordedAt, false,
                30m,
                [new(Guid.NewGuid(), "Peanuts", "SKU-1", 10m, 15m, 20m, 25m, 18m, 18m)],
                []));
        });
        var viewModel = await CreateViewModelAsync(handler, notifications);
        viewModel.CaptureText = "Supplier A\t0001\t2";
        await viewModel.ReviewBatchAsync();

        await viewModel.SubmitValidatedBatchAsync();

        var warning = AssertExactlyOne(notifications.Notifications.Where(item => item.Type == "Warning"));
        Assert.AreEqual("Prices updated: Peanuts", warning.Title);
        StringAssert.Contains(warning.Message, "Cost increased: ₱10.00 -> ₱15.00");
        StringAssert.Contains(warning.Message, "Selling increased: ₱20.00 -> ₱25.00");
        Assert.AreEqual(30m, viewModel.Result!.TotalCost);
    }

    private static BatchReceiptValidationResponse ValidResponse(BatchReceiptRequest request)
    {
        var row = new BatchReceiptPreviewRowResponse(
            [1], "Supplier A", "0001", Guid.NewGuid(), "Supplier A", Guid.NewGuid(), "Coffee", "SKU-1",
            Guid.NewGuid(), "piece", 2, 1, 2, 5, 7, "Valid", [], false, null,
            10m, 10m, 20m, 20m, 18m, 18m, 20m, 1);
        return new BatchReceiptValidationResponse(
            request.IdempotencyKey, request.Reference, request.Notes, true, [row], [],
            new BatchReceiptValidationSummaryResponse(1, 1, 1, 2, 0, 0, 0, 20m));
    }

    private static async Task<BatchReceivingViewModel> CreateViewModelAsync(
        HttpMessageHandler handler,
        TestNotificationService? notifications = null)
    {
        var session = new AuthSession();
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, session);
        await auth.LoginAsync("inventory", "password");
        return new BatchReceivingViewModel(new StoreApiClient(auth), notifications ?? new TestNotificationService());
    }

    private static TokenResponse Tokens() => new(
        "access", DateTime.UtcNow.AddMinutes(10), "refresh", DateTime.UtcNow.AddDays(1),
        new AuthenticatedUser(Guid.NewGuid(), "inventory", "Inventory User", ["Inventory"], false));

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private static T AssertExactlyOne<T>(IEnumerable<T> values)
    {
        var items = values.ToArray();
        Assert.HasCount(1, items);
        return items[0];
    }

    private static JsonSerializerOptions EnumJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
