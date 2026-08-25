using System.Net;
using System.Net.Http.Json;
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

    private static BatchReceiptValidationResponse ValidResponse(BatchReceiptRequest request)
    {
        var row = new BatchReceiptPreviewRowResponse(
            [1], "Supplier A", "0001", Guid.NewGuid(), "Supplier A", Guid.NewGuid(), "Coffee", "SKU-1",
            Guid.NewGuid(), "piece", 2, 1, 2, 5, 7, "Valid", []);
        return new BatchReceiptValidationResponse(
            request.IdempotencyKey, request.Reference, request.Notes, true, [row], [],
            new BatchReceiptValidationSummaryResponse(1, 1, 1, 2, 0, 0, 0));
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
}
