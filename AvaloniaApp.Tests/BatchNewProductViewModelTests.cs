using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class BatchNewProductViewModelTests
{
    [TestMethod]
    public void RequiresExplicitSupplierAndBuildsAtomicDraftWithScannedBarcode()
    {
        var supplier = new SupplierResponse(Guid.NewGuid(), "Supplier A", null, null, true);
        var model = ValidModel(supplier);

        Assert.IsFalse(model.TryBuildRequest(Guid.NewGuid(), out _, out var missingSupplier));
        StringAssert.Contains(missingSupplier, "supplier");

        model.SelectedSupplier = supplier;
        var correlationId = Guid.NewGuid();
        Assert.IsTrue(model.TryBuildRequest(correlationId, out var request, out var error), error);
        Assert.AreEqual(correlationId, request!.CorrelationId);
        Assert.AreEqual("000123", request.ReceiptBarcode);
        Assert.AreEqual("000123", request.PieceBarcode);
        Assert.AreEqual(supplier.Id, request.SupplierId);
        Assert.AreEqual("Peanuts", request.Name);
    }

    [TestMethod]
    public void RejectsPackageThatDuplicatesScannedPieceBarcode()
    {
        var supplier = new SupplierResponse(Guid.NewGuid(), "Supplier A", null, null, true);
        var model = ValidModel(supplier);
        model.SelectedSupplier = supplier;
        model.Packages.Add(new ProductPackageDraft
        {
            Barcode = "000123",
            Label = "Case",
            PiecesPerUnit = 12,
            RegularPrice = 200,
            EmployeePrice = 180
        });

        Assert.IsFalse(model.TryBuildRequest(Guid.NewGuid(), out _, out var error));
        StringAssert.Contains(error, "unique");
    }

    private static BatchNewProductViewModel ValidModel(SupplierResponse supplier) => new([supplier], "000123", "Supplier A")
    {
        Name = "Peanuts",
        Category = "Snacks",
        Unit = "piece",
        CostPrice = 10,
        RegularPrice = 15,
        EmployeePrice = 12,
        CriticalReorderLevel = 5,
        CriticalOrderQuantity = 10,
        WarningReorderLevel = 10,
        WarningOrderQuantity = 20
    };
}
