using System.Net;
using System.Net.Http.Json;
using System.Text;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class StoreApiClientTests
{
    [TestMethod]
    public async Task SaleRetryCreatesFreshJsonRequestAndSerializesEnumAsString()
    {
        var saleCalls = 0;
        var bodies = new List<string>();
        var requests = new List<HttpRequestMessage>();
        var session = new AuthSession();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access-1", "refresh-1"));
            if (request.RequestUri.AbsolutePath.EndsWith("/refresh")) return Json(Tokens("access-2", "refresh-2"));
            if (request.RequestUri.AbsolutePath.EndsWith("/sales"))
            {
                requests.Add(request);
                bodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                saleCalls++;
                return saleCalls == 1
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    : Json(SaleResponse());
            }
            throw new InvalidOperationException(request.RequestUri.ToString());
        });
        var auth = new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, session);
        await auth.LoginAsync("cashier", "password");
        var client = new StoreApiClient(auth);

        var result = await client.CreateSaleAsync(new CreateSaleRequest(
            Guid.NewGuid(), ApiCustomerType.Employee, ApiPaymentMethod.GCash,
            [new CreateSaleLineRequest(Guid.NewGuid(), 2)]));

        Assert.AreEqual("SALE-1", result.SaleNumber);
        Assert.AreEqual(2, saleCalls);
        Assert.AreNotSame(requests[0], requests[1]);
        Assert.AreEqual(bodies[0], bodies[1]);
        StringAssert.Contains(bodies[0], "\"customerType\":\"Employee\"");
        StringAssert.Contains(bodies[0], "\"paymentMethod\":\"GCash\"");
    }

    [TestMethod]
    public async Task ExactBarcodePreservesLeadingZeros()
    {
        Uri? requestedUri = null;
        var (auth, session) = Client(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            requestedUri = request.RequestUri;
            return Json(PosProduct());
        });
        await auth.LoginAsync("inventory", "password");

        var product = await new StoreApiClient(auth).GetProductByBarcodeAsync("0000123");

        Assert.AreEqual("Product", product.Name);
        StringAssert.EndsWith(requestedUri!.AbsolutePath, "/0000123");
        Assert.IsTrue(session.IsAuthenticated);
    }

    [TestMethod]
    public async Task ReceivingBarcodeUsesInventoryLookupAndPreservesLeadingZeros()
    {
        Uri? requestedUri = null;
        var (auth, _) = Client(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            requestedUri = request.RequestUri;
            return Json(PosProduct());
        });
        await auth.LoginAsync("inventory", "password");

        await new StoreApiClient(auth).GetProductForReceivingByBarcodeAsync("0000123");

        StringAssert.EndsWith(requestedUri!.AbsolutePath, "/products/receiving/by-barcode/0000123");
    }

    [TestMethod]
    public async Task ProductMutationEndpointsUseExactMethodsAndSerializeUpdateContract()
    {
        var product = CatalogProduct();
        var packageId = product.Units.Single(unit => !unit.IsBasePiece).Id;
        var requests = new List<(HttpMethod Method, string Path, string Query, string? Body)>();
        var (auth, _) = Client(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            requests.Add((
                request.Method,
                request.RequestUri.AbsolutePath,
                request.RequestUri.Query,
                request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));
            if (request.Method == HttpMethod.Get)
                return Json(new PagedResponse<ProductResponse>([product], 1, 200, 1));
            return request.Method == HttpMethod.Put
                ? Json(product with { Name = "Updated" })
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        await auth.LoginAsync("inventory", "password");
        var client = new StoreApiClient(auth);
        var update = new UpdateProductRequest(
            product.SupplierId, ApiInventoryItemType.Supply, "SKU-2", "0002", "Updated", "Supplies", "piece",
            12.5m, 15, 14, 2, 3, 5, 4, product.Version,
            [new UpdateProductUnitRequest(packageId, "0012", "Case", 12, 170, 160, false)]);

        var updated = await client.UpdateProductAsync(product.Id, update);
        await client.DeactivateProductAsync(product.Id);
        await client.ReactivateProductAsync(product.Id);
        await client.GetProductsAsync(includeInactive: true, pageSize: 200);

        Assert.AreEqual("Updated", updated.Name);
        CollectionAssert.AreEqual(
            new[] { HttpMethod.Put, HttpMethod.Post, HttpMethod.Post, HttpMethod.Get },
            requests.Select(request => request.Method).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                $"/api/products/{product.Id}",
                $"/api/products/{product.Id}/deactivate",
                $"/api/products/{product.Id}/reactivate",
                "/api/products"
            },
            requests.Select(request => request.Path).ToArray());
        StringAssert.Contains(requests[0].Body!, "\"itemType\":\"Supply\"");
        StringAssert.Contains(requests[0].Body!, $"\"id\":\"{packageId}\"");
        StringAssert.Contains(requests[0].Body!, "\"pieceBarcode\":\"0002\"");
        StringAssert.Contains(requests[0].Body!, "\"isActive\":false");
        StringAssert.Contains(requests[0].Body!, $"\"version\":{product.Version}");
        StringAssert.Contains(requests[3].Query, "includeInactive=true");
    }

    [TestMethod]
    public async Task ProblemDetailsAreAvailableOnApiClientException()
    {
        var (auth, _) = Client(request => request.RequestUri!.AbsolutePath.EndsWith("/login")
            ? Json(Tokens("access", "refresh"))
            : new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    "{\"title\":\"Validation failed\",\"detail\":\"Target must exceed reorder.\",\"status\":400,\"errors\":{\"targetStockLevel\":[\"Invalid target.\"]}}",
                    Encoding.UTF8,
                    "application/problem+json")
            });
        await auth.LoginAsync("inventory", "password");

        var exception = await Assert.ThrowsExactlyAsync<ApiClientException>(
            () => new StoreApiClient(auth).GetProductsAsync());

        Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.AreEqual("Target must exceed reorder.", exception.Message);
        Assert.AreEqual("Invalid target.", exception.Problem!.Errors!["targetStockLevel"][0]);
    }

    [TestMethod]
    public async Task InventoryImportEndpointsSerializeContractAndEnumAsString()
    {
        var paths = new List<string>();
        var bodies = new List<string>();
        var (auth, _) = Client(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            paths.Add(request.RequestUri.AbsolutePath);
            bodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            if (request.RequestUri.AbsolutePath.EndsWith("/validate"))
                return Json(new InventoryImportValidationResult(true, [], new InventoryImportSummary(1, 1, 1, 0, 2, 3, 0)));
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(new InventoryImportCommitResult(false, Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    new InventoryImportValidationResult(false,
                        [new InventoryImportIssue(7, "sku", "existingSku", "SKU already exists.")],
                        new InventoryImportSummary(1, 0, 1, 0, 2, 3, 1))))
            };
        });
        await auth.LoginAsync("inventory", "password");
        var client = new StoreApiClient(auth);
        var importKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var request = new InventoryImportRequest(
            importKey,
            "legacy.xlsx",
            "abc123",
            [new InventoryImportSupplierRequest("supplier-a", "Supplier A", true, null, null)],
            [new InventoryImportProductRequest(
                7, "supplier-a", ApiInventoryItemType.Consumable, "SKU-1", "0001", "Product", "Category", "piece",
                1, 2, 1.5m, 0, 4, 3, 2, 2, 3, [])]);

        var validation = await client.ValidateInventoryImportAsync(request);
        var result = await client.ImportInventoryAsync(request);

        Assert.IsTrue(validation.IsValid);
        Assert.IsFalse(result.Committed);
        Assert.AreEqual("existingSku", result.Validation.Issues.Single().Code);
        CollectionAssert.AreEqual(new[] { "/api/inventory-imports/validate", "/api/inventory-imports" }, paths);
        Assert.IsTrue(bodies.All(body => body.Contains("\"itemType\":\"Consumable\"")));
        Assert.IsTrue(bodies.All(body => body.Contains("\"pieceBarcode\":\"0001\"")));
        Assert.IsTrue(bodies.All(body => body.Contains("\"importKey\":\"11111111-1111-1111-1111-111111111111\"")));
    }

    [TestMethod]
    public async Task BatchReceiptEndpointsSerializeExactRequestAndResponses()
    {
        var paths = new List<string>();
        var bodies = new List<string>();
        var key = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var row = new BatchReceiptPreviewRowResponse(
            [1, 2], "Supplier A", "0000123", Guid.NewGuid(), "Supplier A", Guid.NewGuid(), "Product", "SKU-1",
            Guid.NewGuid(), "case", 5, 12, 60, 10, 70, "Valid", []);
        var (auth, _) = Client(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            paths.Add(request.RequestUri.AbsolutePath);
            bodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            if (request.RequestUri.AbsolutePath.EndsWith("/validate"))
                return Json(new BatchReceiptValidationResponse(key, "DR-1", "Delivery", true, [row], [],
                    new BatchReceiptValidationSummaryResponse(2, 1, 1, 60, 0, 0, 0)));
            return Json(new BatchReceiptResponse(
                Guid.NewGuid(), key, "DR-1", 2, 1, 1, 60, ["Supplier A"], DateTime.UtcNow, false));
        });
        await auth.LoginAsync("inventory", "password");
        var client = new StoreApiClient(auth);
        var request = new BatchReceiptRequest(key, "DR-1", "Delivery",
        [
            new BatchReceiptRecordRequest(1, "Supplier A", "0000123", 2),
            new BatchReceiptRecordRequest(2, "Supplier A", "0000123", 3)
        ]);

        var validation = await client.ValidateBatchReceiptAsync(request);
        var result = await client.ReceiveBatchAsync(request);

        Assert.IsTrue(validation.CanCommit);
        Assert.AreEqual(2, validation.Rows.Single().SourceRecords.Count);
        Assert.AreEqual(2, result.AcceptedRecordCount);
        CollectionAssert.AreEqual(new[] { "/api/stock-receipts/batch/validate", "/api/stock-receipts/batch" }, paths);
        Assert.IsTrue(bodies.All(body => body.Contains("\"idempotencyKey\":\"22222222-2222-2222-2222-222222222222\"")));
        Assert.IsTrue(bodies.All(body => body.Contains("\"reference\":\"DR-1\"")));
        Assert.IsTrue(bodies.All(body => body.Contains("\"notes\":\"Delivery\"")));
        Assert.IsTrue(bodies.All(body => body.Contains("\"records\":[{\"sourceRecord\":1,\"supplierLibrary\":\"Supplier A\",\"barcode\":\"0000123\",\"unitQuantity\":2}")));
    }

    [TestMethod]
    public async Task BatchValidationDeserializesWarningAndErrorSeverityJson()
    {
        const string key = "33333333-3333-3333-3333-333333333333";
        var json = "{" +
            $"\"idempotencyKey\":\"{key}\",\"reference\":null,\"notes\":null,\"canCommit\":false," +
            "\"rows\":[],\"issues\":[" +
            "{\"code\":\"supplierLibraryNotFound\",\"field\":\"supplierLibrary\",\"sourceRecord\":1,\"message\":\"Uses registered supplier.\",\"severity\":\"Warning\"}," +
            "{\"code\":\"unknownBarcode\",\"field\":\"barcode\",\"sourceRecord\":2,\"message\":\"Barcode is unknown.\",\"severity\":\"Error\"}]," +
            "\"summary\":{\"inputRecordCount\":2,\"normalizedLineCount\":2,\"affectedProductCount\":1,\"totalBasePieces\":2,\"warningCount\":1,\"errorCount\":1,\"issueCount\":2}}";
        var (auth, _) = Client(request => request.RequestUri!.AbsolutePath.EndsWith("/login")
            ? Json(Tokens("access", "refresh"))
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        await auth.LoginAsync("inventory", "password");

        var response = await new StoreApiClient(auth).ValidateBatchReceiptAsync(
            new BatchReceiptRequest(Guid.Parse(key), null, null, [new(1, "Scanner", "0001", 1)]));

        CollectionAssert.AreEqual(new[] { "Warning", "Error" }, response.Issues.Select(issue => issue.Severity).ToArray());
        Assert.AreEqual(1, response.Summary.WarningCount);
        Assert.AreEqual(1, response.Summary.ErrorCount);
        Assert.AreEqual(2, response.Summary.IssueCount);
    }

    [TestMethod]
    public async Task BatchValidationProblemResponseIsExposed()
    {
        var (auth, _) = Client(request => request.RequestUri!.AbsolutePath.EndsWith("/login")
            ? Json(Tokens("access", "refresh"))
            : new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    "{\"title\":\"Validation failed\",\"detail\":\"Batch request is invalid.\",\"status\":400,\"errors\":{\"records\":[\"At least one record is required.\"]}}",
                    Encoding.UTF8,
                    "application/problem+json")
            });
        await auth.LoginAsync("inventory", "password");

        var exception = await Assert.ThrowsExactlyAsync<ApiClientException>(() =>
            new StoreApiClient(auth).ValidateBatchReceiptAsync(new BatchReceiptRequest(Guid.NewGuid(), null, null, [])));

        Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.AreEqual("Batch request is invalid.", exception.Message);
        Assert.AreEqual("At least one record is required.", exception.Problem!.Errors!["records"][0]);
    }

    [TestMethod]
    public async Task StockMovementHistorySendsServerPagingAndAuditFilters()
    {
        Uri? requestedUri = null;
        var movement = new StockMovementResponse(
            Guid.NewGuid(), Guid.NewGuid(), null, null, "Coffee", "SKU-1", "Supplier", "Receipt",
            12, 1, "case", "0001", 12, 0, 12, 0, 12, "DR-1", "Delivery",
            DateTime.UtcNow, Guid.NewGuid(), "Inventory User");
        var (auth, _) = Client(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            requestedUri = request.RequestUri;
            return Json(new PagedResponse<StockMovementResponse>([movement], 2, 20, 21));
        });
        await auth.LoginAsync("inventory", "password");

        var result = await new StoreApiClient(auth).GetStockMovementsAsync(
            "coffee", "Receipt", "DR-1", new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), 2, 20, "product", "asc");

        Assert.AreEqual(21, result.TotalCount);
        Assert.AreEqual("Inventory User", result.Items.Single().CreatedByName);
        StringAssert.Contains(requestedUri!.Query, "search=coffee");
        StringAssert.Contains(requestedUri.Query, "movementType=Receipt");
        StringAssert.Contains(requestedUri.Query, "reference=DR-1");
        StringAssert.Contains(requestedUri.Query, "page=2");
        StringAssert.Contains(requestedUri.Query, "pageSize=20");
        StringAssert.Contains(requestedUri.Query, "sortBy=product");
        StringAssert.Contains(requestedUri.Query, "sortDirection=asc");
        StringAssert.Contains(requestedUri.Query, "toUtcExclusive=");
    }

    [TestMethod]
    public async Task ReportingEndpointsUseExpectedPathsAndUtcRange()
    {
        var uris = new List<Uri>();
        var (auth, _) = Client(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            uris.Add(request.RequestUri);
            return request.RequestUri.AbsolutePath switch
            {
                "/api/dashboard" => Json(Dashboard()),
                "/api/reports/sales" => Json(SalesReport()),
                "/api/reports/inventory" => Json(InventoryReport()),
                "/api/reports/orders" => Json(OrderReport()),
                "/api/reports/employee-purchases" => Json(EmployeePurchaseReport()),
                _ => throw new InvalidOperationException(request.RequestUri.ToString())
            };
        });
        await auth.LoginAsync("inventory", "password");
        var client = new StoreApiClient(auth);
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        await client.GetDashboardAsync();
        await client.GetSalesReportAsync(from, to);
        await client.GetInventoryReportAsync();
        await client.GetOrderReportAsync();
        await client.GetEmployeePurchaseReportAsync(from, to);

        CollectionAssert.AreEqual(
            new[] { "/api/dashboard", "/api/reports/sales", "/api/reports/inventory", "/api/reports/orders", "/api/reports/employee-purchases" },
            uris.Select(uri => uri.AbsolutePath).ToArray());
        StringAssert.Contains(uris[1].Query, "fromUtc=");
        StringAssert.Contains(uris[1].Query, "toUtcExclusive=");
    }

    [TestMethod]
    public async Task DashboardViewModelMapsApiSnapshotAndClearsDataOnFailure()
    {
        var dashboardCalls = 0;
        var (auth, _) = Client(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            dashboardCalls++;
            return dashboardCalls == 1
                ? Json(Dashboard())
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"title\":\"Unavailable\",\"detail\":\"Database unavailable.\",\"status\":503}", Encoding.UTF8, "application/problem+json")
                };
        });
        await auth.LoginAsync("inventory", "password");
        var notifications = new TestNotificationService();
        var viewModel = new DashboardPageViewModel(new StoreApiClient(auth), notifications);
        while (viewModel.IsLoading) await Task.Delay(5);

        Assert.AreEqual("₱125.50", viewModel.TodaySalesDisplay);
        Assert.AreEqual(2, viewModel.TodayTransactions);
        Assert.AreEqual(7, viewModel.ShelfUnits);
        Assert.AreEqual("Product", viewModel.AttentionItems.Single().Name);
        Assert.AreEqual("August 26, 2025 8:00 AM", viewModel.RecentSales.Single().TimeDisplay);

        await viewModel.RefreshAsync();

        Assert.AreEqual(0, viewModel.TodayTransactions);
        Assert.AreEqual(0, viewModel.AttentionItems.Count);
        Assert.AreEqual("Database unavailable.", viewModel.ErrorMessage);
        Assert.AreEqual("Error", notifications.Notifications.Single().Type);
    }

    [TestMethod]
    public async Task ReportsViewModelCombinesApiSnapshotsAndExposesFailureState()
    {
        var fail = false;
        var (auth, _) = Client(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            if (fail) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"title\":\"Unavailable\",\"detail\":\"Reports unavailable.\",\"status\":503}", Encoding.UTF8, "application/problem+json")
            };
            return request.RequestUri.AbsolutePath switch
            {
                "/api/reports/sales" => Json(SalesReport()),
                "/api/reports/inventory" => Json(InventoryReport()),
                "/api/reports/orders" => Json(OrderReport()),
                "/api/reports/employee-purchases" => Json(EmployeePurchaseReport()),
                "/api/employees" => Json(Array.Empty<EmployeeResponse>()),
                _ => throw new InvalidOperationException(request.RequestUri.ToString())
            };
        });
        await auth.LoginAsync("inventory", "password");
        var viewModel = new ReportsViewModel(new StoreApiClient(auth));
        while (viewModel.IsLoading) await Task.Delay(5);

        Assert.IsNotNull(viewModel.Snapshot);
        Assert.AreEqual("₱500.00", viewModel.GrossSalesDisplay);
        Assert.AreEqual(3, viewModel.UnitsSold);
        Assert.AreEqual(15, viewModel.TotalInventoryUnits);
        Assert.AreEqual(4, viewModel.SuggestedOrderUnits);
        Assert.AreEqual("Product", viewModel.TopProducts.Single().ProductName);

        fail = true;
        await viewModel.RefreshAsync();

        Assert.IsNull(viewModel.Snapshot);
        Assert.AreEqual(0, viewModel.InventoryItems.Count);
        Assert.AreEqual("Reports unavailable.", viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task PosScannerUsesSelectedPackageAndEnforcesBasePieceDisplayLimit()
    {
        var productId = Guid.NewGuid();
        var piece = new ProductUnitResponse(productId, "0001", "piece", 1, 10, 9, true, true);
        var package = new ProductUnitResponse(Guid.NewGuid(), "0003", "3-pack", 3, 27, 25, false, true);
        var catalogProduct = new PosProductResponse(
            productId, "Supplier", "SKU", "0001", "Product", "piece", 10, 9, 5, 1, [piece, package], null);
        var scannedProduct = catalogProduct with { SelectedUnit = package };
        var (auth, _) = Client(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/login")) return Json(Tokens("access", "refresh"));
            if (path.EndsWith("/pos")) return Json(new PagedResponse<PosProductResponse>([catalogProduct], 1, 200, 1));
            if (path.Contains("/by-barcode/")) return Json(scannedProduct);
            throw new InvalidOperationException(path);
        });
        await auth.LoginAsync("cashier", "password");
        var viewModel = new SalesViewModel(new StoreApiClient(auth), new TestNotificationService());
        while (viewModel.IsBusy) await Task.Delay(5);

        viewModel.ScannerText = "0003";
        await viewModel.ScanBarcodeAsync();
        viewModel.SelectedCustomerType = ApiCustomerType.Employee;

        Assert.AreEqual(1, viewModel.Cart.Count);
        Assert.AreEqual(3, viewModel.Cart[0].BasePieceQuantity);
        Assert.AreEqual(25m, viewModel.Cart[0].UnitPrice);

        viewModel.AddProductCommand.Execute(catalogProduct);

        Assert.AreEqual(2, viewModel.Cart.Count);
        Assert.AreEqual(4, viewModel.Cart.Sum(line => line.BasePieceQuantity));
        Assert.AreEqual(34m, viewModel.Cart.Sum(line => line.Amount));

        viewModel.ScannerText = "0003";
        await viewModel.ScanBarcodeAsync();

        Assert.AreEqual(1, viewModel.Cart[0].Count);
        StringAssert.Contains(viewModel.StatusMessage, "5 base pieces");
    }

    [TestMethod]
    public void PackageSuggestionTracksConversionUntilPriceIsOverridden()
    {
        var package = new ProductPackageDraft(10, 8);

        package.PiecesPerUnit = 24;
        Assert.AreEqual(240m, package.RegularPrice);
        Assert.AreEqual(192m, package.EmployeePrice);

        package.RegularPrice = 220;
        package.PiecesPerUnit = 12;

        Assert.AreEqual(220m, package.RegularPrice);
        Assert.AreEqual(96m, package.EmployeePrice);
    }

    [TestMethod]
    public void EmployeeUnitPriceFallsBackToRegularWhenUnset()
    {
        var unit = new ProductUnitResponse(Guid.NewGuid(), "0001", "piece", 1, 10, 0, true, true);

        Assert.AreEqual(10m, ApiCartLine.PriceFor(unit, ApiCustomerType.Employee));
    }

    private static (AuthApiClient Auth, AuthSession Session) Client(Func<HttpRequestMessage, HttpResponseMessage> factory)
    {
        var session = new AuthSession();
        return (new AuthApiClient(new HttpClient(new StubHttpMessageHandler(factory)) { BaseAddress = new Uri("https://test/") }, session), session);
    }

    private static TokenResponse Tokens(string access, string refresh) => new(
        access, DateTime.UtcNow.AddMinutes(10), refresh, DateTime.UtcNow.AddDays(1),
        new AuthenticatedUser(Guid.NewGuid(), "user", "User", ["Admin"], false));

    private static SaleResponse SaleResponse() => new(
        Guid.NewGuid(), "SALE-1", Guid.NewGuid(), ApiCustomerType.Employee, ApiPaymentMethod.GCash, 20, 20,
        DateTime.UtcNow, Guid.NewGuid(), false, []);

    private static PosProductResponse PosProduct()
    {
        var productId = Guid.NewGuid();
        var unit = new ProductUnitResponse(productId, "0000123", "piece", 1, 10, 9, true, true);
        return new PosProductResponse(productId, "Supplier", "SKU", "0000123", "Product", "piece", 10, 9, 5, 1, [unit], unit);
    }

    private static ProductResponse CatalogProduct()
    {
        var productId = Guid.NewGuid();
        return new ProductResponse(
            productId, Guid.NewGuid(), "Supplier", ApiInventoryItemType.Merchandise,
            "SKU", "0001", "Product", "Category", "piece", 8, 10, 9,
            2, 3, 5, 4, 2, 3, 5, false, false, 0, 7, true,
            [
                new ProductUnitResponse(productId, "0001", "piece", 1, 10, 9, true, true),
                new ProductUnitResponse(Guid.NewGuid(), "0012", "Case", 12, 110, 100, false, true)
            ]);
    }

    private static DashboardResponse Dashboard() => new(125.50m, 2, 7, 8, [InventoryProduct()], [ReportSale()]);

    private static SalesReportResponse SalesReport() => new(
        new SalesReportSummaryResponse(500, 125.50m, 2, 3),
        [new TopProductResponse(Guid.NewGuid(), "SKU", "Product", 3, 125.50m)],
        [ReportSale()],
        [ReportSale()]);

    private static InventoryReportResponse InventoryReport() => new(
        new InventoryReportSummaryResponse(1, 150, 7, 8, 15, 1, 0, 0),
        [InventoryProduct()]);

    private static OrderReportResponse OrderReport() => new(
        new OrderReportSummaryResponse(1, 1, 4),
        [new SupplierOrderResponse(Guid.NewGuid(), "Supplier", 1, 4,
            [new OrderProductResponse(Guid.NewGuid(), "SKU", "Product", 7, 8, 15, 5, 10, "Warning", 4)])]);

    private static EmployeePurchaseReportResponse EmployeePurchaseReport() =>
        new(new EmployeePurchaseSummaryResponse(0, 0, 0), []);

    private static InventoryReportProductResponse InventoryProduct() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Supplier", ApiInventoryItemType.Merchandise, "SKU", "Product", "Category", "piece",
        5, 10, 9, 7, 8, 15, 5, 4, 10, 4, "Warning", 4, "Warning", true);

    private static ReportSaleResponse ReportSale() => new(
        Guid.NewGuid(), "SALE-1", ApiCustomerType.Regular, ApiPaymentMethod.Cash, 125.50m, 125.50m,
        new DateTime(2025, 8, 26, 0, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), "Cashier",
        [new ReportSaleLineResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU", "Product", "piece", null, 1, 3, 3, 10, 30)]);

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
