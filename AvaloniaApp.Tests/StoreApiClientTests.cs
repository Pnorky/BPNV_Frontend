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
            Guid.NewGuid(), ApiCustomerType.Employee, [new CreateSaleLineRequest(Guid.NewGuid(), 2)]));

        Assert.AreEqual("SALE-1", result.SaleNumber);
        Assert.AreEqual(2, saleCalls);
        Assert.AreNotSame(requests[0], requests[1]);
        Assert.AreEqual(bodies[0], bodies[1]);
        StringAssert.Contains(bodies[0], "\"customerType\":\"Employee\"");
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
        var viewModel = new SalesViewModel(new StoreApiClient(auth));
        while (viewModel.IsBusy) await Task.Delay(5);

        viewModel.ScannerText = "0003";
        await viewModel.ScanBarcodeAsync();
        viewModel.SelectedCustomerType = ApiCustomerType.Employee;

        Assert.AreEqual(1, viewModel.Cart.Count);
        Assert.AreEqual(3, viewModel.Cart[0].BasePieceQuantity);
        Assert.AreEqual(25m, viewModel.Cart[0].UnitPrice);

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
        Guid.NewGuid(), "SALE-1", Guid.NewGuid(), ApiCustomerType.Employee, 20, 20,
        DateTime.UtcNow, Guid.NewGuid(), false, []);

    private static PosProductResponse PosProduct()
    {
        var productId = Guid.NewGuid();
        var unit = new ProductUnitResponse(productId, "0000123", "piece", 1, 10, 9, true, true);
        return new PosProductResponse(productId, "Supplier", "SKU", "0000123", "Product", "piece", 10, 9, 5, 1, [unit], unit);
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
