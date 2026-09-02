using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class ProductEditViewModelTests
{
    [TestMethod]
    public void BuildsTrimmedUpdateRequestAndPreservesPackageIdentityAndVersion()
    {
        var (viewModel, packageId) = CreateViewModel();
        viewModel.Name = "  Updated product  ";
        viewModel.Packages[0].Barcode = " 0024 ";
        viewModel.Packages[0].IsActive = false;

        var valid = viewModel.TryBuildRequest(out var request, out var error);

        Assert.IsTrue(valid, error);
        Assert.IsNotNull(request);
        Assert.AreEqual("Updated product", request.Name);
        Assert.AreEqual("0001", request.PieceBarcode);
        Assert.AreEqual(7UL, request.Version);
        var package = request.Packages!.Single();
        Assert.AreEqual(packageId, package.Id);
        Assert.AreEqual("0024", package.Barcode);
        Assert.IsFalse(package.IsActive);
    }

    [TestMethod]
    public void RejectsRequiredBarcodePriceConversionAndReorderViolations()
    {
        var cases = new (Action<ProductEditViewModel> Mutate, string Message)[]
        {
            (viewModel => viewModel.Name = " ", "Product name is required"),
            (viewModel => viewModel.PieceBarcode = " ", "required for Merchandise"),
            (viewModel => viewModel.Packages[0].Barcode = "0001", "barcodes must be unique"),
            (viewModel => viewModel.Packages[0].PiecesPerUnit = 1, "greater than 1"),
            (viewModel => viewModel.Packages[0].RegularPrice = -1, "prices cannot be negative"),
            (viewModel => viewModel.WarningReorderLevel = viewModel.CriticalReorderLevel, "warning must be greater"),
            (viewModel => viewModel.WarningOrderQuantity = 0, "quantities must be whole numbers greater than zero")
        };

        foreach (var testCase in cases)
        {
            var (viewModel, _) = CreateViewModel();
            testCase.Mutate(viewModel);

            Assert.IsFalse(viewModel.TryBuildRequest(out var request, out var error));
            Assert.IsNull(request);
            StringAssert.Contains(error, testCase.Message);
            Assert.AreEqual(error, viewModel.ValidationMessage);
        }
    }

    [TestMethod]
    public void BarcodeLessSupplyBuildsUpdateRequestWithNullBarcode()
    {
        var source = Product();
        var product = source with
        {
            ItemType = ApiInventoryItemType.Supply,
            Barcode = null,
            Units = source.Units.Select(unit => unit.IsBasePiece ? unit with { Barcode = null } : unit).ToArray()
        };
        var supplier = new SupplierResponse(product.SupplierId, "Supplier", null, null, true);
        var viewModel = new ProductEditViewModel(product, [supplier]);

        var valid = viewModel.TryBuildRequest(out var request, out var error);

        Assert.IsTrue(valid, error);
        Assert.IsNotNull(request);
        Assert.IsNull(request.PieceBarcode);
    }

    [TestMethod]
    public void LoadsOnlyActiveSuppliersAndRequiresAnAvailableSelection()
    {
        var product = Product();
        var suppliers = new[]
        {
            new SupplierResponse(product.SupplierId, "Inactive supplier", null, null, false),
            new SupplierResponse(Guid.NewGuid(), "Active supplier", null, null, true)
        };
        var viewModel = new ProductEditViewModel(product, suppliers);

        Assert.AreEqual(1, viewModel.Suppliers.Count);
        Assert.IsNull(viewModel.SelectedSupplier);
        Assert.IsFalse(viewModel.TryBuildRequest(out _, out var error));
        Assert.AreEqual("Select an active supplier.", error);
    }

    private static (ProductEditViewModel ViewModel, Guid PackageId) CreateViewModel()
    {
        var product = Product();
        var supplier = new SupplierResponse(product.SupplierId, "Supplier", null, null, true);
        var viewModel = new ProductEditViewModel(product, [supplier]);
        return (viewModel, product.Units.Single(unit => !unit.IsBasePiece).Id);
    }

    private static ProductResponse Product()
    {
        var productId = Guid.NewGuid();
        return new ProductResponse(
            productId, Guid.NewGuid(), "Supplier", ApiInventoryItemType.Merchandise,
            " SKU-1 ", "0001", "Product", "Snacks", "piece",
            8, 10, 9, 2, 3, 5, 4,
            2, 3, 5, false, false, 0, 7, true,
            [
                new ProductUnitResponse(productId, "0001", "piece", 1, 10, 9, true, true),
                new ProductUnitResponse(Guid.NewGuid(), "0024", "Case", 24, 220, 200, false, true)
            ]);
    }
}
