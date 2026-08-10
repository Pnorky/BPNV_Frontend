using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public sealed record StockMovementOption(string Name, StockMovementType Type)
{
    public override string ToString() => Name;
}

public sealed record InventoryItemTypeOption(string Name, InventoryItemType Type)
{
    public override string ToString() => Name;
}

public sealed record ReorderProductSummary(ProductItem Product)
{
    public string Name => Product.Name;
    public string Sku => Product.Sku;
    public int OnHand => Product.TotalStock;
    public string Tier => Product.ReorderTier;
    public string CriticalLevel => Product.CriticalReorderDisplay;
    public string WarningLevel => Product.WarningReorderDisplay;
    public int OrderQuantity => Product.SuggestedOrderQuantity;
}

public sealed record SupplierOrderSummary(string SupplierName, IReadOnlyList<ReorderProductSummary> Products)
{
    public int ProductCount => Products.Count;
    public int TotalOrderQuantity => Products.Sum(product => product.OrderQuantity);
    public string Summary => $"{ProductCount} product{(ProductCount == 1 ? "" : "s")} · {TotalOrderQuantity} total units";
}

public partial class InventoryViewModel : ObservableObject
{
    private readonly StoreState _store;

    public int SelectedSectionIndex { get; }
    public bool IsProductsSection => SelectedSectionIndex == 0;
    public string SectionTitle => SelectedSectionIndex switch
    {
        1 => "Suppliers",
        2 => "Stock movements",
        _ => "Products"
    };
    public string SectionDescription => SelectedSectionIndex switch
    {
        1 => "Manage the required suppliers assigned to inventory items",
        2 => "Receive, transfer, consume, and adjust Display or Bodega stock",
        _ => "Track Display and Bodega stock; total inventory and status are calculated automatically"
    };

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private SupplierItem? _filterSupplier;
    [ObservableProperty] private InventoryItemTypeOption? _selectedProductType;
    [ObservableProperty] private IReadOnlyList<ProductItem> _filteredProducts;
    [ObservableProperty] private IReadOnlyList<StockMovement> _recentMovements = [];
    [ObservableProperty] private IReadOnlyList<SupplierOrderSummary> _orderSummaries = [];
    [ObservableProperty] private string _statusMessage = "Set up suppliers and products, then record movements. Totals are calculated automatically.";

    [ObservableProperty] private string _newSupplierName = "";
    [ObservableProperty] private string _newSupplierContact = "";
    [ObservableProperty] private string _newSupplierPhone = "";

    [ObservableProperty] private SupplierItem? _newProductSupplier;
    [ObservableProperty] private InventoryItemTypeOption? _newProductType;
    [ObservableProperty] private string _newProductSku = "";
    [ObservableProperty] private string _newProductName = "";
    [ObservableProperty] private string _newProductCategory = "";
    [ObservableProperty] private string _newProductUnit = "pcs";
    [ObservableProperty] private decimal _newCostPrice;
    [ObservableProperty] private decimal _newRegularPrice;
    [ObservableProperty] private decimal _newEmployeePrice;
    [ObservableProperty] private decimal _newCriticalReorderLevel;
    [ObservableProperty] private decimal _newCriticalOrderQuantity = 1;
    [ObservableProperty] private decimal _newWarningReorderLevel = 1;
    [ObservableProperty] private decimal _newWarningOrderQuantity = 1;
    [ObservableProperty] private decimal _newOpeningShelf;
    [ObservableProperty] private decimal _newOpeningBodega;

    [ObservableProperty] private ProductItem? _movementProduct;
    [ObservableProperty] private StockMovementOption? _selectedMovement;
    [ObservableProperty] private decimal _movementQuantity = 1;
    [ObservableProperty] private string _movementNotes = "";

    public ObservableCollection<SupplierItem> Suppliers => _store.Suppliers;
    public ObservableCollection<ProductItem> Products => _store.Products;
    public IReadOnlyList<StockMovementOption> MovementOptions { get; } =
    [
        new("Receive stock into bodega", StockMovementType.Receipt),
        new("Move bodega stock to display", StockMovementType.TransferToShelf),
        new("Record AR from display", StockMovementType.AccountsReceivable),
        new("Record display spoilage / BO", StockMovementType.Spoilage),
        new("Record bodega spoilage / BO", StockMovementType.BodegaSpoilageBo),
        new("Use supply from display", StockMovementType.DisplayUsage),
        new("Use supply from bodega", StockMovementType.BodegaUsage),
        new("Correct display stock (+)", StockMovementType.ShelfAdjustmentIn),
        new("Correct display stock (-)", StockMovementType.ShelfAdjustmentOut),
        new("Correct bodega stock (+)", StockMovementType.BodegaAdjustmentIn),
        new("Correct bodega stock (-)", StockMovementType.BodegaAdjustmentOut)
    ];
    public IReadOnlyList<InventoryItemTypeOption> ItemTypes { get; } =
    [
        new("Merchandise (sold to customers)", InventoryItemType.Merchandise),
        new("Consumable", InventoryItemType.Consumable),
        new("Supply", InventoryItemType.Supply)
    ];
    public IReadOnlyList<InventoryItemTypeOption> ProductTypes { get; } =
    [
        new("Merchandise", InventoryItemType.Merchandise),
        new("Consumables", InventoryItemType.Consumable),
        new("Supplies", InventoryItemType.Supply)
    ];
    public int ShelfUnits => Products.Sum(product => product.ShelfStock);
    public int BodegaUnits => Products.Sum(product => product.BodegaStock);
    public int LowStockCount => Products.Count(product => product.IsLowStock);
    public int MissingReorderCount => Products.Count(product => product.EffectiveWarningReorderLevel is null);

    public InventoryViewModel(StoreState store, int selectedSectionIndex = 0)
    {
        _store = store;
        SelectedSectionIndex = selectedSectionIndex;
        _filteredProducts = store.Products;
        _selectedMovement = MovementOptions[0];
        _newProductType = ItemTypes[0];
        _selectedProductType = ProductTypes[0];
        _recentMovements = store.Movements.Take(50).ToList();
        _store.StateChanged += OnStoreChanged;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterSupplierChanged(SupplierItem? value) => ApplyFilter();
    partial void OnSelectedProductTypeChanged(InventoryItemTypeOption? value) => ApplyFilter();

    [RelayCommand]
    private void ClearFilter()
    {
        SearchText = "";
        FilterSupplier = null;
    }

    [RelayCommand]
    private async Task ShowOrderSummary()
    {
        OrderSummaries = Products
            .Where(product => product.SuggestedOrderQuantity > 0)
            .GroupBy(product => product.SupplierName)
            .OrderBy(group => group.Key)
            .Select(group => new SupplierOrderSummary(
                group.Key,
                group.OrderBy(product => product.Name).Select(product => new ReorderProductSummary(product)).ToList()))
            .ToList();

        if (OrderSummaries.Count == 0)
        {
            StatusMessage = "No products currently meet a critical or warning reorder threshold.";
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            return;

        await new ReorderSummaryDialog { DataContext = this }.ShowDialog(owner);
    }

    [RelayCommand]
    private void AddSupplier()
    {
        if (!_store.AddSupplier(NewSupplierName, NewSupplierContact, NewSupplierPhone, out var message))
        {
            StatusMessage = message;
            return;
        }

        NewProductSupplier = Suppliers[^1];
        NewSupplierName = "";
        NewSupplierContact = "";
        NewSupplierPhone = "";
        StatusMessage = message;
    }

    [RelayCommand]
    private void AddProduct()
    {
        var input = new ProductInput(
            NewProductSupplier?.Id ?? Guid.Empty,
            NewProductType?.Type ?? InventoryItemType.Merchandise,
            NewProductSku,
            NewProductName,
            NewProductCategory,
            NewProductUnit,
            NewCostPrice,
            NewRegularPrice,
            NewEmployeePrice,
            (int)NewCriticalReorderLevel,
            (int)NewCriticalOrderQuantity,
            (int)NewWarningReorderLevel,
            (int)NewWarningOrderQuantity,
            (int)NewOpeningShelf,
            (int)NewOpeningBodega);

        if (!_store.AddProduct(input, out var product, out var message))
        {
            StatusMessage = message;
            return;
        }

        MovementProduct = product;
        SelectedProductType = ProductTypes.First(type => type.Type == product!.ItemType);
        NewProductSku = "";
        NewProductName = "";
        NewProductCategory = "";
        NewCostPrice = 0;
        NewRegularPrice = 0;
        NewEmployeePrice = 0;
        NewCriticalReorderLevel = 0;
        NewCriticalOrderQuantity = 1;
        NewWarningReorderLevel = 1;
        NewWarningOrderQuantity = 1;
        NewOpeningShelf = 0;
        NewOpeningBodega = 0;
        StatusMessage = message;
    }

    [RelayCommand]
    private void ApplyMovement()
    {
        if (SelectedMovement is null)
        {
            StatusMessage = "Select a movement type.";
            return;
        }

        if (!_store.ApplyStockMovement(MovementProduct, SelectedMovement.Type, (int)MovementQuantity, MovementNotes, out var message))
        {
            StatusMessage = message;
            return;
        }

        MovementQuantity = 1;
        MovementNotes = "";
        StatusMessage = message;
    }

    [RelayCommand]
    private void TransferToShelf(ProductItem? product)
    {
        if (product is null) return;
        _store.TransferToShelf(product, (int)product.TransferQuantity, out var message);
        product.TransferQuantity = product.BodegaStock > 0 ? Math.Min(product.TransferQuantity, product.BodegaStock) : 0;
        StatusMessage = message;
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        ApplyFilter();
        RecentMovements = _store.Movements.Take(50).ToList();
        OnPropertyChanged(nameof(ShelfUnits));
        OnPropertyChanged(nameof(BodegaUnits));
        OnPropertyChanged(nameof(LowStockCount));
        OnPropertyChanged(nameof(MissingReorderCount));
    }

    private void ApplyFilter()
    {
        IEnumerable<ProductItem> products = Products;
        if (SelectedProductType is not null) products = products.Where(product => product.ItemType == SelectedProductType.Type);
        if (FilterSupplier is not null) products = products.Where(product => product.SupplierId == FilterSupplier.Id);
        if (!string.IsNullOrWhiteSpace(SearchText))
            products = products.Where(product =>
                product.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                product.Sku.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                product.SupplierName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                product.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        FilteredProducts = products.OrderBy(product => product.SupplierName).ThenBy(product => product.Name).ToList();
    }
}
