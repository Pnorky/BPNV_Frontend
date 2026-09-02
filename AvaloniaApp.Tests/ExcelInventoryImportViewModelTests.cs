using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class ExcelInventoryImportViewModelTests
{
    [TestMethod]
    public void BlankSupplyBarcodeWarnsWithoutBlockingAndPreviewEditsReachRequest()
    {
        var api = new StoreApiClient(new AuthApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new AssertFailedException("No API request expected.")))
            {
                BaseAddress = new Uri("https://test/")
            },
            new AuthSession()));
        var viewModel = new ExcelInventoryImportViewModel(api, new TestNotificationService());
        var draft = new ExcelInventoryImportResult { Format = ExcelInventoryWorkbookFormat.StandardTemplate };
        draft.Products.Add(new ExcelInventoryProductDraft
        {
            SourceSheet = "Products",
            SourceRow = 2,
            SupplierName = "Supplier",
            ItemType = ApiInventoryItemType.Supply,
            Sku = "SUP-1",
            PieceBarcode = "",
            Name = "Disposable cup",
            Category = "Supplies",
            Unit = "piece",
            CostPrice = 1,
            RegularPrice = 0,
            EmployeePrice = 0,
            CriticalReorderLevel = 5,
            CriticalOrderQuantity = 20,
            WarningReorderLevel = 10,
            WarningOrderQuantity = 10,
            OpeningDisplayStock = 0,
            OpeningBodegaStock = 100
        });
        viewModel.LoadDraft(draft);

        Assert.IsTrue(viewModel.CanValidate);
        Assert.IsTrue(viewModel.Issues.Any(issue => issue.Severity == "Warning" && issue.Field == "pieceBarcode"));

        viewModel.Products[0].Name = "Updated cup";
        viewModel.Products[0].PieceBarcode = "000123";
        var valid = viewModel.TryBuildRequest(out var request, out var error);

        Assert.IsTrue(valid, error);
        Assert.IsNotNull(request);
        Assert.AreEqual("Updated cup", request.Products!.Single().Name);
        Assert.AreEqual("000123", request.Products!.Single().PieceBarcode);
    }
}
