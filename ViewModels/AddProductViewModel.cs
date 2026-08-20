using System.Collections.ObjectModel;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class ProductPackageDraft : ObservableObject
{
    private decimal _baseRegularPrice;
    private decimal _baseEmployeePrice;
    private bool _regularPriceOverridden;
    private bool _employeePriceOverridden;
    private bool _applyingSuggestion;

    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private decimal _piecesPerUnit = 2;
    [ObservableProperty] private decimal _regularPrice;
    [ObservableProperty] private decimal _employeePrice;

    public ProductPackageDraft(decimal baseRegularPrice = 0, decimal baseEmployeePrice = 0)
    {
        _baseRegularPrice = baseRegularPrice;
        _baseEmployeePrice = baseEmployeePrice;
        ApplySuggestedPrices();
    }

    partial void OnPiecesPerUnitChanged(decimal value) => ApplySuggestedPrices();
    partial void OnRegularPriceChanged(decimal value) { if (!_applyingSuggestion) _regularPriceOverridden = true; }
    partial void OnEmployeePriceChanged(decimal value) { if (!_applyingSuggestion) _employeePriceOverridden = true; }

    public void UpdateBasePrices(decimal regularPrice, decimal employeePrice)
    {
        _baseRegularPrice = regularPrice;
        _baseEmployeePrice = employeePrice;
        ApplySuggestedPrices();
    }

    private void ApplySuggestedPrices()
    {
        _applyingSuggestion = true;
        if (!_regularPriceOverridden) RegularPrice = _baseRegularPrice * PiecesPerUnit;
        if (!_employeePriceOverridden) EmployeePrice = _baseEmployeePrice * PiecesPerUnit;
        _applyingSuggestion = false;
    }
}

public partial class AddProductViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private bool _updatingSku;
    private bool _skuWasEdited;

    [ObservableProperty] private SupplierResponse? _selectedSupplier;
    [ObservableProperty] private ApiInventoryItemType _itemType = ApiInventoryItemType.Merchandise;
    [ObservableProperty] private string _sku = "";
    [ObservableProperty] private string _pieceBarcode = "";
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
    [ObservableProperty] private string _supplierName = "";
    [ObservableProperty] private string _supplierContact = "";
    [ObservableProperty] private string _supplierPhone = "";
    [ObservableProperty] private string _statusMessage = "Loading suppliers...";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<SupplierResponse> Suppliers { get; } = [];
    public ObservableCollection<ProductPackageDraft> Packages { get; } = [];
    public IReadOnlyList<ApiInventoryItemType> ItemTypes { get; } = Enum.GetValues<ApiInventoryItemType>();
    public IReadOnlyList<string> Categories { get; } =
    [
        "Beverages", "Snacks", "Grocery", "Personal Care", "Household",
        "Condiments", "Frozen", "Tobacco", "Lubricants", "Consumables", "Supplies", "Other"
    ];

    public AddProductViewModel(StoreApiClient api)
    {
        _api = api;
        _ = LoadSuppliersAsync();
    }

    [RelayCommand]
    public async Task LoadSuppliersAsync()
    {
        if (IsBusy) return;
        StatusMessage = "Loading suppliers...";
        IsBusy = true;
        try
        {
            var selectedId = SelectedSupplier?.Id;
            var suppliers = await _api.GetSuppliersAsync();
            Suppliers.Clear();
            foreach (var supplier in suppliers.Where(item => item.IsActive).OrderBy(item => item.Name))
                Suppliers.Add(supplier);
            SelectedSupplier = Suppliers.FirstOrDefault(item => item.Id == selectedId) ?? Suppliers.FirstOrDefault();
            StatusMessage = Suppliers.Count == 0
                ? "Create a supplier below before registering a product."
                : $"Loaded {Suppliers.Count} supplier{(Suppliers.Count == 1 ? "" : "s")}. New products start with zero stock.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = FailureMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnRegularPriceChanged(decimal value) => UpdatePackageSuggestions();
    partial void OnEmployeePriceChanged(decimal value) => UpdatePackageSuggestions();

    partial void OnNameChanged(string value)
    {
        if (!_skuWasEdited) GenerateSku();
    }

    partial void OnPieceBarcodeChanged(string value)
    {
        if (!_skuWasEdited) GenerateSku();
    }

    partial void OnItemTypeChanged(ApiInventoryItemType value)
    {
        if (!_skuWasEdited) GenerateSku();
    }

    partial void OnSkuChanged(string value)
    {
        if (!_updatingSku && !string.IsNullOrWhiteSpace(value)) _skuWasEdited = true;
    }

    [RelayCommand]
    private void GenerateSkuFromProduct() => GenerateSku(force: true);

    private void GenerateSku(bool force = false)
    {
        if (!force && _skuWasEdited) return;
        var words = new string(Name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var prefix = ItemType switch
        {
            ApiInventoryItemType.Consumable => "CON",
            ApiInventoryItemType.Supply => "SUP",
            _ => "MER"
        };
        var productCode = string.IsNullOrWhiteSpace(words) ? "ITEM" : words[..Math.Min(words.Length, 6)];
        var barcode = new string(PieceBarcode.Where(char.IsLetterOrDigit).ToArray());
        var barcodeCode = string.IsNullOrWhiteSpace(barcode)
            ? "BARCODE"
            : barcode[Math.Max(0, barcode.Length - 6)..];
        _updatingSku = true;
        Sku = $"{prefix}-{productCode}-{barcodeCode}";
        _updatingSku = false;
    }

    [RelayCommand]
    private void AddPackage() => Packages.Add(new ProductPackageDraft(RegularPrice, EmployeePrice));

    [RelayCommand]
    private void RemovePackage(ProductPackageDraft? package)
    {
        if (package is not null) Packages.Remove(package);
    }

    [RelayCommand]
    private async Task CreateSupplierAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(SupplierName))
        {
            StatusMessage = "Supplier name is required.";
            return;
        }

        StatusMessage = "Creating supplier...";
        IsBusy = true;
        try
        {
            var supplier = await _api.CreateSupplierAsync(new CreateSupplierRequest(
                SupplierName.Trim(), NullIfWhiteSpace(SupplierContact), NullIfWhiteSpace(SupplierPhone)));
            Suppliers.Add(supplier);
            SelectedSupplier = supplier;
            SupplierName = "";
            SupplierContact = "";
            SupplierPhone = "";
            StatusMessage = $"Supplier {supplier.Name} created and selected.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = FailureMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateProductAsync()
    {
        if (IsBusy) return;
        if (!TryBuildRequest(out var request, out var error))
        {
            StatusMessage = error;
            return;
        }

        StatusMessage = "Creating product and unit barcodes...";
        IsBusy = true;
        try
        {
            var product = await _api.CreateProductAsync(request!);
            ClearProduct();
            StatusMessage = $"{product.Name} was created with {product.Units.Count} unit option{(product.Units.Count == 1 ? "" : "s")} and zero stock.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = FailureMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal bool TryBuildRequest(out CreateProductRequest? request, out string error)
    {
        request = null;
        error = "";
        if (SelectedSupplier is null) return Fail("Select or create a supplier.", out error);
        if (string.IsNullOrWhiteSpace(Sku)) return Fail("SKU is required.", out error);
        if (string.IsNullOrWhiteSpace(PieceBarcode)) return Fail("Piece barcode is required.", out error);
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

        var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PieceBarcode.Trim() };
        var packages = new List<CreateProductUnitRequest>();
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
            packages.Add(new CreateProductUnitRequest(
                package.Barcode.Trim(), package.Label.Trim(), (int)package.PiecesPerUnit,
                package.RegularPrice, package.EmployeePrice));
        }

        request = new CreateProductRequest(
            SelectedSupplier.Id, ItemType, Sku.Trim(), PieceBarcode.Trim(), Name.Trim(), Category.Trim(), Unit.Trim(),
            CostPrice, RegularPrice, EmployeePrice,
            (int)CriticalReorderLevel, (int)CriticalOrderQuantity,
            (int)WarningReorderLevel, (int)WarningOrderQuantity, packages);
        return true;
    }

    private void ClearProduct()
    {
        Sku = "";
        PieceBarcode = "";
        Name = "";
        Category = "";
        CostPrice = 0;
        RegularPrice = 0;
        EmployeePrice = 0;
        CriticalReorderLevel = 0;
        CriticalOrderQuantity = 1;
        WarningReorderLevel = 1;
        WarningOrderQuantity = 1;
        Packages.Clear();
    }

    private void UpdatePackageSuggestions()
    {
        foreach (var package in Packages)
            package.UpdateBasePrices(RegularPrice, EmployeePrice);
    }

    private static bool WholeNumber(decimal value) => value == decimal.Truncate(value) && value <= int.MaxValue;
    private static bool Fail(string message, out string error) { error = message; return false; }
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception switch
    {
        HttpRequestException => "Cannot reach the store API.",
        TaskCanceledException => "The store API did not respond in time.",
        _ => exception.Message
    };
}
