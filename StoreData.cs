using System.Collections.ObjectModel;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApp;

public enum InventoryItemType
{
    Merchandise = 0,
    Supply = 1,
    Consumable = 2
}

public sealed class SupplierItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public string ContactPerson { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Details => string.Join(" · ", new[] { ContactPerson, Phone }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public override string ToString() => Name;
}

public partial class ProductItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SupplierId { get; init; }
    public InventoryItemType ItemType { get; set; }
    public required string SupplierName { get; init; }
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Unit { get; init; }
    public decimal CostPrice { get; init; }
    public decimal RegularPrice { get; init; }
    public decimal EmployeePrice { get; init; }
    public int? CriticalReorderLevel { get; init; }
    public int? CriticalOrderQuantity { get; init; }
    public int? WarningReorderLevel { get; init; }
    public int? WarningOrderQuantity { get; init; }
    public int? ReorderLevel { get; init; }
    public int? TargetStockLevel { get; set; }
    public bool IsActive { get; init; } = true;

    [ObservableProperty]
    private int _shelfStock;

    [ObservableProperty]
    private int _bodegaStock;

    [ObservableProperty]
    private decimal _transferQuantity = 1;

    public int TotalStock => ShelfStock + BodegaStock;
    public string ShelfDisplay => $"{ShelfStock} {Unit}";
    public string BodegaDisplay => $"{BodegaStock} {Unit}";
    public string TotalDisplay => $"{TotalStock} {Unit}";
    public string CostPriceDisplay => CostPrice > 0 ? $"₱{CostPrice:N2}" : "Not set";
    public string RegularPriceDisplay => RegularPrice > 0 ? $"₱{RegularPrice:N2}" : "Not set";
    public string EmployeePriceDisplay => EmployeePrice > 0 ? $"₱{EmployeePrice:N2}" : "Not set";
    public int? EffectiveWarningReorderLevel => WarningReorderLevel ?? ReorderLevel;
    public int? EffectiveCriticalReorderLevel => CriticalReorderLevel ??
        (EffectiveWarningReorderLevel is int warning ? warning / 2 : null);
    public int? EffectiveCriticalOrderQuantity => CriticalOrderQuantity ?? LegacyOrderQuantity(EffectiveCriticalReorderLevel);
    public int? EffectiveWarningOrderQuantity => WarningOrderQuantity ?? LegacyOrderQuantity(EffectiveWarningReorderLevel);
    public string CriticalReorderDisplay => EffectiveCriticalReorderLevel?.ToString() ?? "Not set";
    public string WarningReorderDisplay => EffectiveWarningReorderLevel?.ToString() ?? "Not set";
    public string ItemTypeDisplay => ItemType switch
    {
        InventoryItemType.Merchandise => "Merchandise",
        InventoryItemType.Consumable => "Consumable",
        _ => "Supply"
    };
    public bool IsSellable => ItemType == InventoryItemType.Merchandise;
    public string CatalogDetails => $"{Sku}  ·  {SupplierName}  ·  Display: {ShelfStock} {Unit}";
    public string MovementSelectorText => $"{Name} · {Sku} · {SupplierName}";
    public string MovementSelectorDetails => $"{Sku} · {SupplierName}";
    public bool IsCriticalStock => EffectiveCriticalReorderLevel is int level && TotalStock <= level;
    public bool IsLowStock => IsCriticalStock || EffectiveWarningReorderLevel is int level && TotalStock <= level;
    public int SuggestedOrderQuantity => IsCriticalStock
        ? EffectiveCriticalOrderQuantity.GetValueOrDefault()
        : EffectiveWarningReorderLevel is int warning && TotalStock <= warning
            ? EffectiveWarningOrderQuantity.GetValueOrDefault()
            : 0;
    public string ReorderTier => IsCriticalStock ? "Critical" : IsLowStock ? "Warning" : "Healthy";
    public string StockStatus => TotalStock == 0
        ? "Out of stock"
        : IsLowStock
            ? "Low stock"
            : ShelfStock == 0
                ? "Refill display"
                : EffectiveWarningReorderLevel is null
                    ? "Reorder not set"
                    : "In stock";

    partial void OnShelfStockChanged(int value) => NotifyStockChanged();
    partial void OnBodegaStockChanged(int value) => NotifyStockChanged();

    private void NotifyStockChanged()
    {
        OnPropertyChanged(nameof(TotalStock));
        OnPropertyChanged(nameof(ShelfDisplay));
        OnPropertyChanged(nameof(BodegaDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(CatalogDetails));
        OnPropertyChanged(nameof(IsLowStock));
        OnPropertyChanged(nameof(IsCriticalStock));
        OnPropertyChanged(nameof(SuggestedOrderQuantity));
        OnPropertyChanged(nameof(ReorderTier));
        OnPropertyChanged(nameof(StockStatus));
    }

    private int? LegacyOrderQuantity(int? level) => level is int threshold && TargetStockLevel is int target
        ? Math.Max(1, target - threshold)
        : null;

    public override string ToString() => $"{Name} ({Sku})";
}

public enum StockMovementType
{
    OpeningShelf,
    OpeningBodega,
    Receipt,
    TransferToShelf,
    Sale,
    AccountsReceivable,
    ShelfAdjustmentIn,
    ShelfAdjustmentOut,
    BodegaAdjustmentIn,
    BodegaAdjustmentOut,
    Spoilage,
    BodegaSpoilageBo,
    DisplayUsage,
    BodegaUsage
}

public sealed class StockMovement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProductId { get; init; }
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public required string SupplierName { get; init; }
    public required StockMovementType Type { get; init; }
    public required int Quantity { get; init; }
    public required DateTime OccurredAt { get; init; }
    public string Reference { get; init; } = "";
    public string Notes { get; init; } = "";
    public string TypeDisplay => Type switch
    {
        StockMovementType.OpeningShelf => "Opening display",
        StockMovementType.OpeningBodega => "Opening bodega",
        StockMovementType.Receipt => "Received",
        StockMovementType.TransferToShelf => "Bodega → display",
        StockMovementType.Sale => "Sale",
        StockMovementType.AccountsReceivable => "AR issue",
        StockMovementType.ShelfAdjustmentIn => "Display correction +",
        StockMovementType.ShelfAdjustmentOut => "Display correction -",
        StockMovementType.BodegaAdjustmentIn => "Bodega correction +",
        StockMovementType.BodegaAdjustmentOut => "Bodega correction -",
        StockMovementType.Spoilage => "Display spoilage / BO",
        StockMovementType.BodegaSpoilageBo => "Bodega spoilage / BO",
        StockMovementType.DisplayUsage => "Display usage",
        StockMovementType.BodegaUsage => "Bodega usage",
        _ => Type.ToString()
    };
    public string QuantityDisplay => Type is StockMovementType.Receipt or StockMovementType.OpeningShelf or
        StockMovementType.OpeningBodega or StockMovementType.ShelfAdjustmentIn or StockMovementType.BodegaAdjustmentIn
            ? $"+{Quantity}"
            : Type == StockMovementType.TransferToShelf
                ? Quantity.ToString()
                : $"-{Quantity}";
    public string TimeDisplay => StoreDateTime.FormatEvent(OccurredAt);
}

public sealed record ProductInput(
    Guid SupplierId,
    InventoryItemType ItemType,
    string Sku,
    string Name,
    string Category,
    string Unit,
    decimal CostPrice,
    decimal RegularPrice,
    decimal EmployeePrice,
    int? CriticalReorderLevel,
    int? CriticalOrderQuantity,
    int? WarningReorderLevel,
    int? WarningOrderQuantity,
    int OpeningShelf,
    int OpeningBodega);

public sealed record SaleLineRecord(Guid ProductId, string Sku, string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal Amount => Quantity * UnitPrice;
}

public sealed class SaleRecord
{
    public required string SaleNumber { get; init; }
    public required DateTime SoldAt { get; init; }
    public required string CustomerType { get; init; }
    public required IReadOnlyList<SaleLineRecord> Lines { get; init; }
    public decimal Total => Lines.Sum(line => line.Amount);
    public int ItemCount => Lines.Sum(line => line.Quantity);
    public string TimeDisplay => StoreDateTime.FormatEvent(SoldAt);
    public string TotalDisplay => $"₱{Total:N2}";
}

public sealed class StoreState
{
    private readonly StorePersistenceService _persistence;
    private int _nextSaleNumber = 1;

    public ObservableCollection<SupplierItem> Suppliers { get; } = [];
    public ObservableCollection<ProductItem> Products { get; } = [];
    public ObservableCollection<SaleRecord> Sales { get; } = [];
    public ObservableCollection<StockMovement> Movements { get; } = [];
    public event EventHandler? StateChanged;

    public StoreState(StorePersistenceService? persistence = null, bool seedPrototypeData = true)
    {
        _persistence = persistence ?? new StorePersistenceService();
        var document = _persistence.Load();
        foreach (var supplier in document.Suppliers) Suppliers.Add(supplier);
        foreach (var product in document.Products) Products.Add(product);
        foreach (var sale in document.Sales.OrderByDescending(item => item.SoldAt)) Sales.Add(sale);
        foreach (var movement in document.Movements.OrderByDescending(item => item.OccurredAt)) Movements.Add(movement);
        _nextSaleNumber = Math.Max(1, document.NextSaleNumber);
        var migratedPrototypeData = false;
        foreach (var product in Products.Where(product =>
                     product.SupplierName == "PROTOTYPE SUPPLIES VENDOR" &&
                     product.Sku is "SUP-001" or "SUP-002" or "SUP-003" &&
                     product.ItemType == InventoryItemType.Supply))
        {
            product.ItemType = InventoryItemType.Consumable;
            migratedPrototypeData = true;
        }
        foreach (var product in Products.Where(product =>
                     product.SupplierName is "SHOPPERS" or "DOUBLE DRAGON" or "MEGABUCKS" or "LUBRICANTS" or "PROTOTYPE SUPPLIES VENDOR" &&
                     product.ReorderLevel is not null && product.TargetStockLevel is null))
        {
            product.TargetStockLevel = product.ReorderLevel.GetValueOrDefault() * 2;
            migratedPrototypeData = true;
        }
        if (seedPrototypeData && Sales.Count == 0 && Products.Any(product => product.Sku == "SHP-001"))
        {
            SeedPrototypeSales();
            migratedPrototypeData = true;
        }
        if (seedPrototypeData && Suppliers.Count == 0 && Products.Count == 0)
            SeedPrototypeData();
        else if (seedPrototypeData && Products.Any(product => product.Sku == "SHP-001") && Products.All(product => product.ItemType != InventoryItemType.Supply))
        {
            AddPrototypeSupplies();
            Commit();
        }
        else if (migratedPrototypeData)
            Commit();

        if (!seedPrototypeData && RemovePrototypeData())
            Commit();
    }

    private bool RemovePrototypeData()
    {
        var prototypeSuppliers = Suppliers
            .Where(supplier => supplier.ContactPerson.Equals("Sample supplier", StringComparison.OrdinalIgnoreCase))
            .Select(supplier => supplier.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prototypeProducts = Products
            .Where(product => prototypeSuppliers.Contains(product.SupplierName) || product.Sku.StartsWith("SHP-", StringComparison.OrdinalIgnoreCase) && prototypeSuppliers.Contains(product.SupplierName) ||
                              product.Sku.StartsWith("DD-", StringComparison.OrdinalIgnoreCase) && prototypeSuppliers.Contains(product.SupplierName) ||
                              product.Sku.StartsWith("MGB-", StringComparison.OrdinalIgnoreCase) && prototypeSuppliers.Contains(product.SupplierName) ||
                              product.Sku.StartsWith("LUB-", StringComparison.OrdinalIgnoreCase) && prototypeSuppliers.Contains(product.SupplierName) ||
                              product.Sku.StartsWith("SUP-", StringComparison.OrdinalIgnoreCase) && prototypeSuppliers.Contains(product.SupplierName))
            .ToArray();
        if (prototypeProducts.Length == 0 && !Movements.Any(movement => movement.Notes.Contains("Prototype", StringComparison.OrdinalIgnoreCase)))
            return false;

        var productIds = prototypeProducts.Select(product => product.Id).ToHashSet();
        foreach (var movement in Movements.Where(movement => productIds.Contains(movement.ProductId) || movement.Notes.Contains("Prototype", StringComparison.OrdinalIgnoreCase)).ToArray())
            Movements.Remove(movement);
        foreach (var sale in Sales.Where(sale => sale.Lines.Any(line => productIds.Contains(line.ProductId) || line.Sku.StartsWith("SHP-", StringComparison.OrdinalIgnoreCase))).ToArray())
            Sales.Remove(sale);
        foreach (var product in prototypeProducts) Products.Remove(product);
        foreach (var supplier in Suppliers.Where(supplier => prototypeSuppliers.Contains(supplier.Name)).ToArray()) Suppliers.Remove(supplier);
        return true;
    }

    public bool AddSupplier(string name, string contactPerson, string phone, out string message)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            message = "Supplier name is required.";
            return false;
        }

        if (Suppliers.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            message = $"Supplier {name} already exists.";
            return false;
        }

        Suppliers.Add(new SupplierItem { Name = name, ContactPerson = contactPerson.Trim(), Phone = phone.Trim() });
        Commit();
        message = $"Supplier {name} added.";
        return true;
    }

    public bool AddProduct(ProductInput input, out ProductItem? product, out string message)
    {
        product = null;
        var supplier = Suppliers.FirstOrDefault(item => item.Id == input.SupplierId);
        if (supplier is null)
        {
            message = "Select a supplier first.";
            return false;
        }

        var name = input.Name.Trim();
        if (name.Length == 0)
        {
            message = "Product name is required.";
            return false;
        }

        if (input.OpeningShelf < 0 || input.OpeningBodega < 0 || input.CriticalReorderLevel < 0 ||
            input.CriticalOrderQuantity < 0 || input.WarningReorderLevel < 0 || input.WarningOrderQuantity < 0 ||
            input.CostPrice < 0 || input.RegularPrice < 0 || input.EmployeePrice < 0)
        {
            message = "Stock, prices, and reorder level cannot be negative.";
            return false;
        }

        if (input.CriticalReorderLevel is not int critical || input.WarningReorderLevel is not int warning || warning <= critical)
        {
            message = "Critical and warning reorder levels are required, and warning must be greater than critical.";
            return false;
        }

        if (input.CriticalOrderQuantity is not int criticalQuantity || criticalQuantity <= 0 ||
            input.WarningOrderQuantity is not int warningQuantity || warningQuantity <= 0)
        {
            message = "Critical and warning order quantities must be greater than zero.";
            return false;
        }

        var sku = string.IsNullOrWhiteSpace(input.Sku) ? GenerateSku(supplier.Name) : input.Sku.Trim().ToUpperInvariant();
        if (Products.Any(item => item.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)))
        {
            message = $"SKU {sku} is already in use.";
            return false;
        }

        if (Products.Any(item => item.SupplierId == supplier.Id && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            message = $"{name} is already listed under {supplier.Name}.";
            return false;
        }

        product = new ProductItem
        {
            SupplierId = supplier.Id,
            ItemType = input.ItemType,
            SupplierName = supplier.Name,
            Sku = sku,
            Name = name,
            Category = string.IsNullOrWhiteSpace(input.Category) ? "Uncategorized" : input.Category.Trim(),
            Unit = string.IsNullOrWhiteSpace(input.Unit) ? "pcs" : input.Unit.Trim(),
            CostPrice = input.CostPrice,
            RegularPrice = input.RegularPrice,
            EmployeePrice = input.EmployeePrice,
            CriticalReorderLevel = input.CriticalReorderLevel,
            CriticalOrderQuantity = input.CriticalOrderQuantity,
            WarningReorderLevel = input.WarningReorderLevel,
            WarningOrderQuantity = input.WarningOrderQuantity,
            ShelfStock = input.OpeningShelf,
            BodegaStock = input.OpeningBodega
        };
        Products.Add(product);
        if (input.OpeningShelf > 0) AddMovement(product, StockMovementType.OpeningShelf, input.OpeningShelf, notes: "Opening balance");
        if (input.OpeningBodega > 0) AddMovement(product, StockMovementType.OpeningBodega, input.OpeningBodega, notes: "Opening balance");
        Commit();
        message = $"{name} added with calculated total stock of {product.TotalStock} {product.Unit}.";
        return true;
    }

    public bool ApplyStockMovement(ProductItem? product, StockMovementType type, int quantity, string notes, out string message)
    {
        if (product is null)
        {
            message = "Select a product.";
            return false;
        }

        if (quantity <= 0)
        {
            message = "Quantity must be greater than zero.";
            return false;
        }

        switch (type)
        {
            case StockMovementType.Receipt:
            case StockMovementType.BodegaAdjustmentIn:
                product.BodegaStock += quantity;
                break;
            case StockMovementType.TransferToShelf:
                if (product.BodegaStock < quantity)
                {
                    message = $"Only {product.BodegaStock} {product.Unit} are available in the bodega.";
                    return false;
                }
                product.BodegaStock -= quantity;
                product.ShelfStock += quantity;
                break;
            case StockMovementType.ShelfAdjustmentIn:
                product.ShelfStock += quantity;
                break;
            case StockMovementType.ShelfAdjustmentOut:
            case StockMovementType.Spoilage:
            case StockMovementType.AccountsReceivable:
            case StockMovementType.DisplayUsage:
                if (product.ShelfStock < quantity)
                {
                    message = $"Only {product.ShelfStock} {product.Unit} are available on display.";
                    return false;
                }
                product.ShelfStock -= quantity;
                break;
            case StockMovementType.BodegaAdjustmentOut:
            case StockMovementType.BodegaSpoilageBo:
            case StockMovementType.BodegaUsage:
                if (product.BodegaStock < quantity)
                {
                    message = $"Only {product.BodegaStock} {product.Unit} are available in the bodega.";
                    return false;
                }
                product.BodegaStock -= quantity;
                break;
            default:
                message = "That movement type cannot be entered manually.";
                return false;
        }

        AddMovement(product, type, quantity, notes: notes.Trim());
        Commit();
        message = $"{type.ToString().Replace("Adjustment", " adjustment")} recorded for {product.Name}. Total stock: {product.TotalStock} {product.Unit}.";
        return true;
    }

    public bool TransferToShelf(ProductItem product, int quantity, out string message) =>
        ApplyStockMovement(product, StockMovementType.TransferToShelf, quantity, "Display replenishment", out message);

    public bool RecordSale(string customerType, IEnumerable<CartLine> cart, out string message)
    {
        var lines = cart.ToList();
        if (lines.Count == 0)
        {
            message = "Add at least one product before completing the sale.";
            return false;
        }

        var unavailable = lines.FirstOrDefault(line => line.Quantity > line.Product.ShelfStock);
        if (unavailable is not null)
        {
            message = $"Only {unavailable.Product.ShelfStock} {unavailable.Product.Unit} of {unavailable.Product.Name} are on display.";
            return false;
        }

        if (lines.Any(line => line.UnitPrice <= 0))
        {
            message = "Every product in the sale must have a selling price.";
            return false;
        }

        var number = $"S-{_nextSaleNumber:000000}";
        var soldAt = StoreDateTime.UtcNow;
        foreach (var line in lines)
        {
            line.Product.ShelfStock -= line.Quantity;
            AddMovement(line.Product, StockMovementType.Sale, line.Quantity, number, customerType, soldAt);
        }

        Sales.Insert(0, new SaleRecord
        {
            SaleNumber = number,
            SoldAt = soldAt,
            CustomerType = customerType,
            Lines = lines.Select(line => new SaleLineRecord(line.Product.Id, line.Product.Sku, line.Product.Name, line.Quantity, line.UnitPrice)).ToList()
        });
        _nextSaleNumber++;
        Commit();
        message = $"Sale {number} recorded successfully.";
        return true;
    }

    private void AddMovement(
        ProductItem product,
        StockMovementType type,
        int quantity,
        string reference = "",
        string notes = "",
        DateTime? occurredAtUtc = null) =>
        Movements.Insert(0, new StockMovement
        {
            ProductId = product.Id,
            Sku = product.Sku,
            ProductName = product.Name,
            SupplierName = product.SupplierName,
            Type = type,
            Quantity = quantity,
            OccurredAt = occurredAtUtc ?? StoreDateTime.UtcNow,
            Reference = reference,
            Notes = notes
        });

    private string GenerateSku(string supplierName)
    {
        var prefix = new string(supplierName.Where(char.IsLetterOrDigit).Take(3).Select(char.ToUpperInvariant).ToArray());
        if (prefix.Length == 0) prefix = "PRD";
        var number = 1;
        string sku;
        do sku = $"{prefix}-{number++:000}";
        while (Products.Any(product => product.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)));
        return sku;
    }

    private void SeedPrototypeData()
    {
        var shoppers = new SupplierItem { Name = "SHOPPERS", ContactPerson = "Sample supplier" };
        var doubleDragon = new SupplierItem { Name = "DOUBLE DRAGON", ContactPerson = "Sample supplier" };
        var megabucks = new SupplierItem { Name = "MEGABUCKS", ContactPerson = "Sample supplier" };
        var lubricants = new SupplierItem { Name = "LUBRICANTS", ContactPerson = "Sample supplier" };
        Suppliers.Add(shoppers);
        Suppliers.Add(doubleDragon);
        Suppliers.Add(megabucks);
        Suppliers.Add(lubricants);

        AddPrototypeProduct(shoppers, "SHP-001", "Boy Bawang Lechon Manok", "Snacks", "packs", 18, 25, 22, 10, 10, 5);
        AddPrototypeProduct(shoppers, "SHP-002", "Boy Bawang Garlic", "Snacks", "packs", 18, 25, 22, 10, 10, 0);
        AddPrototypeProduct(shoppers, "SHP-003", "Bread Pan", "Snacks", "packs", 14, 20, 18, 15, 4, 0);
        AddPrototypeProduct(shoppers, "SHP-004", "Choco Mucho Dark", "Chocolate", "bars", 12, 18, 16, 5, 14, 0);
        AddPrototypeProduct(shoppers, "SHP-005", "Fita Crackers", "Biscuits", "packs", 9, 15, 13, 20, 30, 90);
        AddPrototypeProduct(doubleDragon, "DD-001", "Coke Mismo", "Beverages", "bottles", 16, 25, 22, 24, 12, 48);
        AddPrototypeProduct(doubleDragon, "DD-002", "Wilkins 500ml", "Beverages", "bottles", 12, 20, 18, 24, 18, 72);
        AddPrototypeProduct(doubleDragon, "DD-003", "Wilkins 1 Liter", "Beverages", "bottles", 22, 35, 32, 18, 10, 36);
        AddPrototypeProduct(megabucks, "MGB-001", "Nestea Lemon", "Beverages", "bottles", 14, 22, 20, 12, 10, 24);
        AddPrototypeProduct(megabucks, "MGB-002", "Summit", "Beverages", "bottles", 10, 18, 16, 12, 1, 0);
        AddPrototypeProduct(lubricants, "LUB-001", "2T 1L", "Lubricants", "bottles", 105, 145, 135, 10, 4, 65);
        AddPrototypeProduct(lubricants, "LUB-002", "RX-400 1L", "Lubricants", "bottles", 155, 210, 195, 24, 6, 28);
        AddPrototypeProduct(lubricants, "LUB-003", "Coolant 1L Green", "Lubricants", "bottles", 85, 120, 110, 12, 4, 22);
        AddPrototypeSupplies();
        SeedPrototypeSales();
        Commit();
    }

    private void SeedPrototypeSales()
    {
        var today = StoreDateTime.StoreToday;
        AddPrototypeSale("SHP-001", 2, 25, "Regular", StoreDateTime.StoreTimeToUtc(today.AddHours(9).AddMinutes(15)));
        AddPrototypeSale("DD-002", 3, 18, "Employee", StoreDateTime.StoreTimeToUtc(today.AddHours(10).AddMinutes(5)));
        AddPrototypeSale("SHP-004", 4, 18, "Regular", StoreDateTime.StoreTimeToUtc(today.AddHours(11).AddMinutes(20)));
        AddPrototypeSale("DD-001", 2, 25, "Regular", StoreDateTime.StoreTimeToUtc(today.AddDays(-1).AddHours(15).AddMinutes(10)));
        AddPrototypeSale("LUB-002", 1, 195, "Employee", StoreDateTime.StoreTimeToUtc(today.AddDays(-1).AddHours(16).AddMinutes(30)));
    }

    private void AddPrototypeSale(string sku, int quantity, decimal unitPrice, string customerType, DateTime soldAt)
    {
        var product = Products.FirstOrDefault(item => item.Sku == sku);
        if (product is null || product.ShelfStock < quantity) return;

        var saleNumber = $"S-{_nextSaleNumber:000000}";
        product.ShelfStock -= quantity;
        Sales.Add(new SaleRecord
        {
            SaleNumber = saleNumber,
            SoldAt = soldAt,
            CustomerType = customerType,
            Lines = [new SaleLineRecord(product.Id, product.Sku, product.Name, quantity, unitPrice)]
        });
        Movements.Insert(0, new StockMovement
        {
            ProductId = product.Id,
            Sku = product.Sku,
            ProductName = product.Name,
            SupplierName = product.SupplierName,
            Type = StockMovementType.Sale,
            Quantity = quantity,
            OccurredAt = soldAt,
            Reference = saleNumber,
            Notes = "Prototype sale"
        });
        _nextSaleNumber++;
    }

    private void AddPrototypeSupplies()
    {
        var supplier = new SupplierItem { Name = "PROTOTYPE SUPPLIES VENDOR", ContactPerson = "Sample supplier" };
        Suppliers.Add(supplier);
        AddPrototypeProduct(supplier, "SUP-001", "Disposable Cup", "Consumables", "pcs", 0, 0, 0, 2000, 0, 3950, InventoryItemType.Consumable);
        AddPrototypeProduct(supplier, "SUP-002", "Lids for Cup", "Consumables", "pcs", 0, 0, 0, 2000, 0, 3800, InventoryItemType.Consumable);
        AddPrototypeProduct(supplier, "SUP-003", "Coffee Filters 1x70", "Consumables", "packs", 0, 0, 0, 10, 0, 33, InventoryItemType.Consumable);
        AddPrototypeProduct(supplier, "SUP-004", "Tissue", "Consumables", "packs", 0, 0, 0, 10, 0, 10, InventoryItemType.Supply);
        AddPrototypeProduct(supplier, "SUP-005", "Hotdog Box 1x25", "Consumables", "packs", 0, 0, 0, 20, 0, 0, InventoryItemType.Supply);
    }

    private void AddPrototypeProduct(
        SupplierItem supplier,
        string sku,
        string name,
        string category,
        string unit,
        decimal costPrice,
        decimal regularPrice,
        decimal employeePrice,
        int reorderLevel,
        int shelfStock,
        int bodegaStock,
        InventoryItemType itemType = InventoryItemType.Merchandise)
    {
        var product = new ProductItem
        {
            SupplierId = supplier.Id,
            ItemType = itemType,
            SupplierName = supplier.Name,
            Sku = sku,
            Name = name,
            Category = category,
            Unit = unit,
            CostPrice = costPrice,
            RegularPrice = regularPrice,
            EmployeePrice = employeePrice,
            CriticalReorderLevel = reorderLevel / 2,
            CriticalOrderQuantity = Math.Max(1, reorderLevel * 2 - reorderLevel / 2),
            WarningReorderLevel = Math.Max(1, reorderLevel),
            WarningOrderQuantity = Math.Max(1, reorderLevel),
            ShelfStock = shelfStock,
            BodegaStock = bodegaStock
        };
        Products.Add(product);
        if (shelfStock > 0) AddMovement(product, StockMovementType.OpeningShelf, shelfStock, notes: "Prototype opening balance");
        if (bodegaStock > 0) AddMovement(product, StockMovementType.OpeningBodega, bodegaStock, notes: "Prototype opening balance");
    }

    private void Commit()
    {
        _persistence.Save(this, _nextSaleNumber);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

public partial class CartLine : ObservableObject
{
    public required ProductItem Product { get; init; }

    [ObservableProperty]
    private int _quantity = 1;

    [ObservableProperty]
    private decimal _unitPrice;

    public decimal Amount => Quantity * UnitPrice;
    public string UnitPriceDisplay => $"₱{UnitPrice:N2}";
    public string AmountDisplay => $"₱{Amount:N2}";

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(AmountDisplay));
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(UnitPriceDisplay));
        OnPropertyChanged(nameof(AmountDisplay));
    }
}
