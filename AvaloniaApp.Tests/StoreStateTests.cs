using AvaloniaApp.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class StoreStateTests
{
    private string _path = null!;
    private StoreState _store = null!;

    [TestInitialize]
    public void Initialize()
    {
        _path = Path.Combine(Path.GetTempPath(), "BNPV.Stockroom.Tests", $"{Guid.NewGuid():N}.json");
        _store = new StoreState(new StorePersistenceService(_path), seedPrototypeData: false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        var directory = Path.GetDirectoryName(_path);
        if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [TestMethod]
    public void AddingProductCalculatesTotalAndRecordsOpeningBalances()
    {
        var product = AddProduct(openingShelf: 10, openingBodega: 25, reorderLevel: 12);

        Assert.AreEqual(35, product.TotalStock);
        Assert.AreEqual("In stock", product.StockStatus);
        Assert.AreEqual(2, _store.Movements.Count);
        Assert.IsTrue(_store.Movements.Any(item => item.Type == StockMovementType.OpeningShelf && item.Quantity == 10));
        Assert.IsTrue(_store.Movements.Any(item => item.Type == StockMovementType.OpeningBodega && item.Quantity == 25));
    }

    [TestMethod]
    public void TransferMovesStockWithoutChangingTotal()
    {
        var product = AddProduct(openingShelf: 5, openingBodega: 20);

        var changed = _store.ApplyStockMovement(product, StockMovementType.TransferToShelf, 8, "Refill", out _);

        Assert.IsTrue(changed);
        Assert.AreEqual(13, product.ShelfStock);
        Assert.AreEqual(12, product.BodegaStock);
        Assert.AreEqual(25, product.TotalStock);
        Assert.AreEqual(StockMovementType.TransferToShelf, _store.Movements[0].Type);
    }

    [TestMethod]
    public void InvalidStockRemovalDoesNotChangeBalance()
    {
        var product = AddProduct(openingShelf: 3, openingBodega: 4);

        var changed = _store.ApplyStockMovement(product, StockMovementType.Spoilage, 5, "Damaged", out var message);

        Assert.IsFalse(changed);
        Assert.AreEqual(3, product.ShelfStock);
        StringAssert.Contains(message, "Only 3");
    }

    [TestMethod]
    public void SaleDeductsShelfStockAndRecordsMovement()
    {
        var product = AddProduct(openingShelf: 10, openingBodega: 0, regularPrice: 20);
        var cart = new[] { new CartLine { Product = product, Quantity = 3, UnitPrice = 20 } };

        var recorded = _store.RecordSale("Regular", cart, out _);

        Assert.IsTrue(recorded);
        Assert.AreEqual(7, product.ShelfStock);
        Assert.AreEqual(1, _store.Sales.Count);
        Assert.AreEqual(60m, _store.Sales[0].Total);
        Assert.AreEqual(StockMovementType.Sale, _store.Movements[0].Type);
        Assert.AreEqual(_store.Sales[0].SaleNumber, _store.Movements[0].Reference);
    }

    [TestMethod]
    public void PersistedStoreRoundTripsSuppliersProductsAndMovements()
    {
        var product = AddProduct(openingShelf: 2, openingBodega: 7, reorderLevel: 4);
        Assert.IsTrue(_store.ApplyStockMovement(product, StockMovementType.Receipt, 5, "DR-100", out _));

        var reloaded = new StoreState(new StorePersistenceService(_path), seedPrototypeData: false);

        Assert.AreEqual(1, reloaded.Suppliers.Count);
        Assert.AreEqual(1, reloaded.Products.Count);
        Assert.AreEqual(14, reloaded.Products[0].TotalStock);
        Assert.AreEqual(3, reloaded.Movements.Count);
        Assert.AreEqual("DR-100", reloaded.Movements[0].Notes);
    }

    [TestMethod]
    public void DuplicateSupplierAndSkuAreRejected()
    {
        Assert.IsTrue(_store.AddSupplier("Shoppers", "", "", out _));
        Assert.IsFalse(_store.AddSupplier(" shoppers ", "", "", out _));
        var supplier = _store.Suppliers[0];
        var input = new ProductInput(supplier.Id, InventoryItemType.Merchandise, "SHP-001", "Boy Bawang", "Snacks", "pcs", 0, 10, 0, null, null, 0, 0);
        Assert.IsTrue(_store.AddProduct(input, out _, out _));

        var duplicate = input with { Name = "Different product" };
        Assert.IsFalse(_store.AddProduct(duplicate, out _, out _));
    }

    [TestMethod]
    public void PrototypeDataSeedsOnlyAnEmptyStore()
    {
        var seeded = new StoreState(new StorePersistenceService(_path));

        Assert.AreEqual(5, seeded.Suppliers.Count);
        Assert.AreEqual(18, seeded.Products.Count);
        Assert.AreEqual(3, seeded.Products.Count(product => product.ItemType == InventoryItemType.Consumable));
        Assert.AreEqual(2, seeded.Products.Count(product => product.ItemType == InventoryItemType.Supply));
        Assert.AreEqual(5, seeded.Sales.Count);
        Assert.AreEqual(12, seeded.Sales.Sum(sale => sale.ItemCount));
        Assert.IsTrue(seeded.Products.All(product => product.TotalStock >= 0));

        var reloaded = new StoreState(new StorePersistenceService(_path));
        Assert.AreEqual(18, reloaded.Products.Count);
        Assert.AreEqual(5, reloaded.Sales.Count);
    }

    [TestMethod]
    public void SupplyUsageAndSpoilageDeductFromBodega()
    {
        Assert.IsTrue(_store.AddSupplier("Supplies Vendor", "", "", out _));
        var input = new ProductInput(
            _store.Suppliers[0].Id,
            InventoryItemType.Supply,
            "SUP-100",
            "Disposable Cup",
            "Consumables",
            "pcs",
            0,
            0,
            0,
            10,
            20,
            0,
            20);
        Assert.IsTrue(_store.AddProduct(input, out var product, out _));

        Assert.IsTrue(_store.ApplyStockMovement(product, StockMovementType.BodegaUsage, 4, "Used at counter", out _));
        Assert.IsTrue(_store.ApplyStockMovement(product, StockMovementType.BodegaSpoilageBo, 2, "Damaged", out _));

        Assert.AreEqual(14, product!.BodegaStock);
        Assert.IsFalse(product.IsSellable);
        Assert.AreEqual("Bodega spoilage / BO", _store.Movements[0].TypeDisplay);
    }

    [TestMethod]
    public void SuggestedOrderRestoresLowStockProductToTarget()
    {
        var product = AddProduct(openingShelf: 2, openingBodega: 3, reorderLevel: 10);

        Assert.AreEqual(15, product.SuggestedOrderQuantity);
        Assert.IsTrue(_store.ApplyStockMovement(product, StockMovementType.Receipt, 6, "Delivery", out _));
        Assert.AreEqual(0, product.SuggestedOrderQuantity);
    }

    private ProductItem AddProduct(int openingShelf, int openingBodega, int? reorderLevel = null, decimal regularPrice = 15)
    {
        if (_store.Suppliers.Count == 0) Assert.IsTrue(_store.AddSupplier("Shoppers", "", "", out _));
        var input = new ProductInput(
            _store.Suppliers[0].Id,
            InventoryItemType.Merchandise,
            "SHP-001",
            "Boy Bawang",
            "Snacks",
            "pcs",
            8,
            regularPrice,
            12,
            reorderLevel,
            reorderLevel is null ? null : reorderLevel * 2,
            openingShelf,
            openingBodega);
        Assert.IsTrue(_store.AddProduct(input, out var product, out var message), message);
        return product!;
    }
}
