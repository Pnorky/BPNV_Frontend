using System.Collections.ObjectModel;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class BatchNewProductViewModel : ObservableObject
{
    private bool _updatingSku;
    private bool _skuWasEdited;

    [ObservableProperty] private SupplierResponse? _selectedSupplier;
    [ObservableProperty] private ApiInventoryItemType _itemType = ApiInventoryItemType.Merchandise;
    [ObservableProperty] private string _sku = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _unit = "piece";
    [ObservableProperty] private decimal _costPrice;
    [ObservableProperty] private decimal _regularPrice;
    [ObservableProperty] private decimal _employeePrice;
    [ObservableProperty] private decimal _criticalReorderLevel;
    [ObservableProperty] private decimal _criticalOrderQuantity = 1;
    [ObservableProperty] private decimal _warningReorderLevel = 1;
    [ObservableProperty] private decimal _warningOrderQuantity = 1;
    [ObservableProperty] private string _validationMessage = "";

    public string ReceiptBarcode { get; }
    public string SupplierLibrary { get; }
    public string PieceBarcode => ReceiptBarcode;
    public ObservableCollection<SupplierResponse> Suppliers { get; } = [];
    public ObservableCollection<ProductPackageDraft> Packages { get; } = [];
    public IReadOnlyList<ApiInventoryItemType> ItemTypes { get; } = Enum.GetValues<ApiInventoryItemType>();
    public IReadOnlyList<string> Categories { get; } =
    [
        "Beverages", "Snacks", "Grocery", "Personal Care", "Household",
        "Condiments", "Frozen", "Tobacco", "Lubricants", "Consumables", "Supplies", "Other"
    ];

    public BatchNewProductViewModel(
        IReadOnlyList<SupplierResponse> suppliers,
        string receiptBarcode,
        string supplierLibrary,
        BatchReceiptNewProductRequest? existing = null)
    {
        ReceiptBarcode = receiptBarcode;
        SupplierLibrary = supplierLibrary;
        foreach (var supplier in suppliers.Where(supplier => supplier.IsActive).OrderBy(supplier => supplier.Name))
            Suppliers.Add(supplier);

        if (existing is null)
        {
            GenerateSku();
            return;
        }

        SelectedSupplier = Suppliers.FirstOrDefault(supplier => supplier.Id == existing.SupplierId);
        ItemType = existing.ItemType;
        SetSku(existing.Sku);
        Name = existing.Name;
        Category = existing.Category;
        Unit = existing.Unit;
        CostPrice = existing.CostPrice;
        RegularPrice = existing.RegularPrice;
        EmployeePrice = existing.EmployeePrice;
        CriticalReorderLevel = existing.CriticalReorderLevel;
        CriticalOrderQuantity = existing.CriticalOrderQuantity;
        WarningReorderLevel = existing.WarningReorderLevel;
        WarningOrderQuantity = existing.WarningOrderQuantity;
        foreach (var package in existing.Packages ?? [])
        {
            Packages.Add(new ProductPackageDraft(RegularPrice, EmployeePrice)
            {
                Barcode = package.Barcode,
                Label = package.Label,
                PiecesPerUnit = package.PiecesPerUnit,
                RegularPrice = package.RegularPrice,
                EmployeePrice = package.EmployeePrice
            });
        }
    }

    partial void OnRegularPriceChanged(decimal value) => UpdatePackageSuggestions();
    partial void OnEmployeePriceChanged(decimal value) => UpdatePackageSuggestions();
    partial void OnNameChanged(string value) { if (!_skuWasEdited) GenerateSku(); }
    partial void OnItemTypeChanged(ApiInventoryItemType value) { if (!_skuWasEdited) GenerateSku(); }
    partial void OnSkuChanged(string value)
    {
        if (!_updatingSku && !string.IsNullOrWhiteSpace(value)) _skuWasEdited = true;
    }

    [RelayCommand]
    private void GenerateSkuFromProduct()
    {
        _skuWasEdited = false;
        GenerateSku();
    }

    [RelayCommand]
    private void AddPackage() => Packages.Add(new ProductPackageDraft(RegularPrice, EmployeePrice));

    [RelayCommand]
    private void RemovePackage(ProductPackageDraft? package)
    {
        if (package is not null) Packages.Remove(package);
    }

    public bool TryBuildRequest(Guid correlationId, out BatchReceiptNewProductRequest? request, out string error)
    {
        request = null;
        error = "";
        ValidationMessage = "";
        if (SelectedSupplier is null) return Fail("Select an active supplier.", out error);
        if (string.IsNullOrWhiteSpace(Sku)) return Fail("SKU is required.", out error);
        if (string.IsNullOrWhiteSpace(Name)) return Fail("Product name is required.", out error);
        if (string.IsNullOrWhiteSpace(Category)) return Fail("Category is required.", out error);
        if (string.IsNullOrWhiteSpace(Unit)) return Fail("Base piece unit label is required.", out error);
        if (!Money(CostPrice) || !Money(RegularPrice) || !Money(EmployeePrice))
            return Fail("Prices must be non-negative values with no more than two decimal places.", out error);
        if (!WholeNumber(CriticalReorderLevel) || !WholeNumber(WarningReorderLevel) ||
            CriticalReorderLevel < 0 || WarningReorderLevel <= CriticalReorderLevel)
            return Fail("Critical and warning levels must be whole numbers, and warning must be greater than critical.", out error);
        if (!WholeNumber(CriticalOrderQuantity) || !WholeNumber(WarningOrderQuantity) ||
            CriticalOrderQuantity <= 0 || WarningOrderQuantity <= 0)
            return Fail("Critical and warning order quantities must be whole numbers greater than zero.", out error);

        var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ReceiptBarcode };
        var packages = new List<CreateProductUnitRequest>();
        foreach (var package in Packages)
        {
            if (string.IsNullOrWhiteSpace(package.Barcode) || string.IsNullOrWhiteSpace(package.Label))
                return Fail("Every package requires a barcode and label.", out error);
            if (!WholeNumber(package.PiecesPerUnit) || package.PiecesPerUnit <= 1)
                return Fail("Package pieces per unit must be a whole number greater than one.", out error);
            if (!Money(package.RegularPrice) || !Money(package.EmployeePrice))
                return Fail("Package prices must be non-negative values with no more than two decimal places.", out error);
            if (!barcodes.Add(package.Barcode.Trim()))
                return Fail("Piece and package barcodes must be unique.", out error);
            packages.Add(new CreateProductUnitRequest(
                package.Barcode.Trim(),
                package.Label.Trim(),
                (int)package.PiecesPerUnit,
                package.RegularPrice,
                package.EmployeePrice));
        }

        request = new BatchReceiptNewProductRequest(
            correlationId,
            ReceiptBarcode,
            SelectedSupplier.Id,
            ItemType,
            Sku.Trim(),
            ReceiptBarcode,
            Name.Trim(),
            Category.Trim(),
            Unit.Trim(),
            CostPrice,
            RegularPrice,
            EmployeePrice,
            (int)CriticalReorderLevel,
            (int)CriticalOrderQuantity,
            (int)WarningReorderLevel,
            (int)WarningOrderQuantity,
            packages);
        return true;
    }

    private void GenerateSku()
    {
        var name = new string(Name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var prefix = ItemType switch
        {
            ApiInventoryItemType.Consumable => "CON",
            ApiInventoryItemType.Supply => "SUP",
            _ => "MER"
        };
        var productCode = string.IsNullOrWhiteSpace(name) ? "ITEM" : name[..Math.Min(name.Length, 6)];
        var barcode = new string(ReceiptBarcode.Where(char.IsLetterOrDigit).ToArray());
        var barcodeCode = string.IsNullOrWhiteSpace(barcode) ? "BARCODE" : barcode[Math.Max(0, barcode.Length - 6)..];
        SetSku($"{prefix}-{productCode}-{barcodeCode}");
    }

    private void SetSku(string value)
    {
        _updatingSku = true;
        Sku = value;
        _updatingSku = false;
    }

    private void UpdatePackageSuggestions()
    {
        foreach (var package in Packages) package.UpdateBasePrices(RegularPrice, EmployeePrice);
    }

    private bool Fail(string message, out string error)
    {
        error = message;
        ValidationMessage = message;
        return false;
    }

    private static bool WholeNumber(decimal value) => value == decimal.Truncate(value) && value <= int.MaxValue;
    private static bool Money(decimal value) => value >= 0 && decimal.Round(value, 2) == value;
}
