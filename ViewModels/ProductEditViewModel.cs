using System.Collections.ObjectModel;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class ProductEditPackageDraft : ObservableObject
{
    public Guid? Id { get; }
    public bool CanRemove => Id is null;

    [ObservableProperty] private string _barcode;
    [ObservableProperty] private string _label;
    [ObservableProperty] private decimal _piecesPerUnit;
    [ObservableProperty] private decimal _regularPrice;
    [ObservableProperty] private decimal _employeePrice;
    [ObservableProperty] private bool _isActive;

    public ProductEditPackageDraft(
        Guid? id = null,
        string barcode = "",
        string label = "",
        decimal piecesPerUnit = 2,
        decimal regularPrice = 0,
        decimal employeePrice = 0,
        bool isActive = true)
    {
        Id = id;
        _barcode = barcode;
        _label = label;
        _piecesPerUnit = piecesPerUnit;
        _regularPrice = regularPrice;
        _employeePrice = employeePrice;
        _isActive = isActive;
    }
}

public partial class ProductEditViewModel : ObservableObject
{
    [ObservableProperty] private SupplierResponse? _selectedSupplier;
    [ObservableProperty] private ApiInventoryItemType _itemType;
    [ObservableProperty] private string _sku;
    [ObservableProperty] private string _pieceBarcode;
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _category;
    [ObservableProperty] private string _unit;
    [ObservableProperty] private decimal _costPrice;
    [ObservableProperty] private decimal _regularPrice;
    [ObservableProperty] private decimal _employeePrice;
    [ObservableProperty] private decimal _criticalReorderLevel;
    [ObservableProperty] private decimal _criticalOrderQuantity;
    [ObservableProperty] private decimal _warningReorderLevel;
    [ObservableProperty] private decimal _warningOrderQuantity;
    [ObservableProperty] private string _validationMessage = "";

    public ulong Version { get; }
    public IReadOnlyList<SupplierResponse> Suppliers { get; }
    public IReadOnlyList<ApiInventoryItemType> ItemTypes { get; } = Enum.GetValues<ApiInventoryItemType>();
    public IReadOnlyList<string> Categories { get; }
    public ObservableCollection<ProductEditPackageDraft> Packages { get; } = [];

    public ProductEditViewModel(ProductResponse product, IReadOnlyList<SupplierResponse> suppliers)
    {
        Suppliers = suppliers.Where(supplier => supplier.IsActive).OrderBy(supplier => supplier.Name).ToArray();
        SelectedSupplier = Suppliers.FirstOrDefault(supplier => supplier.Id == product.SupplierId);
        ItemType = product.ItemType;
        Sku = product.Sku;
        PieceBarcode = product.Barcode ?? product.Units.FirstOrDefault(unit => unit.IsBasePiece)?.Barcode ?? "";
        Name = product.Name;
        Category = product.Category;
        Unit = product.Unit;
        CostPrice = product.CostPrice;
        RegularPrice = product.RegularPrice;
        EmployeePrice = product.EmployeePrice;
        CriticalReorderLevel = product.CriticalReorderLevel;
        CriticalOrderQuantity = product.CriticalOrderQuantity;
        WarningReorderLevel = product.WarningReorderLevel;
        WarningOrderQuantity = product.WarningOrderQuantity;
        Version = product.Version;

        foreach (var package in product.Units.Where(unit => !unit.IsBasePiece))
        {
            Packages.Add(new ProductEditPackageDraft(
                package.Id, package.Barcode ?? "", package.Label, package.PiecesPerUnit,
                package.RegularPrice, package.EmployeePrice, package.IsActive));
        }

        var categories = new[]
        {
            "Beverages", "Snacks", "Grocery", "Personal Care", "Household",
            "Condiments", "Frozen", "Tobacco", "Lubricants", "Consumables", "Supplies", "Other"
        };
        Categories = categories.Append(product.Category).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    [RelayCommand]
    private void AddPackage() => Packages.Add(new ProductEditPackageDraft(
        regularPrice: RegularPrice * 2,
        employeePrice: EmployeePrice * 2));

    [RelayCommand]
    private void RemovePackage(ProductEditPackageDraft? package)
    {
        if (package?.CanRemove == true) Packages.Remove(package);
    }

    public bool TryBuildRequest(out UpdateProductRequest? request, out string error)
    {
        request = null;
        error = "";
        ValidationMessage = "";
        if (SelectedSupplier is null) return Fail("Select an active supplier.", out error);
        if (string.IsNullOrWhiteSpace(Sku)) return Fail("SKU is required.", out error);
        if (ItemType == ApiInventoryItemType.Merchandise && string.IsNullOrWhiteSpace(PieceBarcode))
            return Fail("Piece barcode is required for Merchandise.", out error);
        if (string.IsNullOrWhiteSpace(Name)) return Fail("Product name is required.", out error);
        if (string.IsNullOrWhiteSpace(Category)) return Fail("Category is required.", out error);
        if (string.IsNullOrWhiteSpace(Unit)) return Fail("Base piece unit label is required.", out error);
        if (CostPrice < 0 || RegularPrice < 0 || EmployeePrice < 0)
            return Fail("Purchase and selling prices cannot be negative.", out error);
        if (!WholeNumber(CriticalReorderLevel) || !WholeNumber(WarningReorderLevel) ||
            CriticalReorderLevel < 0 || WarningReorderLevel <= CriticalReorderLevel)
            return Fail("Critical and warning levels must be whole numbers, and warning must be greater than critical.", out error);
        if (!WholeNumber(CriticalOrderQuantity) || !WholeNumber(WarningOrderQuantity) ||
            CriticalOrderQuantity <= 0 || WarningOrderQuantity <= 0)
            return Fail("Critical and warning order quantities must be whole numbers greater than zero.", out error);

        var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(PieceBarcode)) barcodes.Add(PieceBarcode.Trim());
        var packages = new List<UpdateProductUnitRequest>();
        foreach (var package in Packages)
        {
            if (string.IsNullOrWhiteSpace(package.Barcode) || string.IsNullOrWhiteSpace(package.Label))
                return Fail("Every package requires a barcode and label.", out error);
            if (!WholeNumber(package.PiecesPerUnit) || package.PiecesPerUnit <= 1)
                return Fail("Package pieces per unit must be a whole number greater than 1.", out error);
            if (package.RegularPrice < 0 || package.EmployeePrice < 0)
                return Fail("Package prices cannot be negative.", out error);
            if (!barcodes.Add(package.Barcode.Trim()))
                return Fail("Piece and package barcodes must be unique.", out error);
            packages.Add(new UpdateProductUnitRequest(
                package.Id, package.Barcode.Trim(), package.Label.Trim(), (int)package.PiecesPerUnit,
                package.RegularPrice, package.EmployeePrice, package.IsActive));
        }

        request = new UpdateProductRequest(
            SelectedSupplier.Id, ItemType, Sku.Trim(), NullIfWhiteSpace(PieceBarcode), Name.Trim(), Category.Trim(), Unit.Trim(),
            CostPrice, RegularPrice, EmployeePrice,
            (int)CriticalReorderLevel, (int)CriticalOrderQuantity,
            (int)WarningReorderLevel, (int)WarningOrderQuantity, Version, packages);
        return true;
    }

    private bool Fail(string message, out string error)
    {
        error = message;
        ValidationMessage = message;
        return false;
    }

    private static bool WholeNumber(decimal value) =>
        value == decimal.Truncate(value) && value <= int.MaxValue;
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
