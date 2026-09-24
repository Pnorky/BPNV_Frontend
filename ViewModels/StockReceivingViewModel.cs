using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class StockReceivingViewModel(StoreApiClient api, INotificationService notifications) : ObservableObject
{
    private bool _isLoadingCatalog;

    [ObservableProperty] private string _scannerText = "";
    [ObservableProperty] private PosProductResponse? _selectedProduct;
    [ObservableProperty] private ProductUnitResponse? _selectedUnit;
    [ObservableProperty] private ProductResponse? _selectedCatalogProduct;
    [ObservableProperty] private ProductResponse? _catalogLookupSelection;
    [ObservableProperty] private IReadOnlyList<ProductResponse> _catalogProducts = [];
    [ObservableProperty] private decimal _count = 1;
    [ObservableProperty] private decimal _unitCost;
    [ObservableProperty] private decimal _sellingPrice;
    [ObservableProperty] private decimal _employeePrice;
    [ObservableProperty] private string _reference = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _lotCode = "";
    [ObservableProperty] private DateTimeOffset? _receivedDate;
    [ObservableProperty] private TimeSpan? _receivedTime;
    [ObservableProperty] private DateTimeOffset? _productionDate;
    [ObservableProperty] private TimeSpan? _productionTime;
    [ObservableProperty] private DateTimeOffset? _expiryDate;
    [ObservableProperty] private TimeSpan? _expiryTime;
    [ObservableProperty] private string _statusMessage = "Scan a piece or package barcode and press Enter.";
    [ObservableProperty] private bool _isBusy;

    public event EventHandler? ScannerFocusRequested;
    public string SelectedName => SelectedProduct?.Name ?? "No unit selected";
    public string UnitDetails => SelectedUnit is null || SelectedProduct is null
        ? "The scanned unit conversion and balances will appear here."
        : $"{SelectedUnit.Label}: {SelectedUnit.PiecesPerUnit} base piece{(SelectedUnit.PiecesPerUnit == 1 ? "" : "s")} · Display {SelectedCatalogProduct?.DisplayStock ?? SelectedProduct.DisplayStock} · Bodega {SelectedCatalogProduct?.BodegaStock.ToString() ?? "loading"}.";
    public string TotalCostDisplay => SelectedUnit is null || !WholeNumber(Count) || Count <= 0
        ? "-"
        : $"₱{UnitCost * Count:N2}";
    public string ConversionPreview => SelectedUnit is null || !WholeNumber(Count) || Count <= 0 || Count > int.MaxValue
        ? ""
        : $"Will receive {(long)Count * SelectedUnit.PiecesPerUnit:N0} base pieces into bodega.";
    public bool IsPerishable => SelectedProduct?.IsPerishable ?? SelectedCatalogProduct?.IsPerishable ?? false;

    partial void OnSelectedProductChanged(PosProductResponse? value) => NotifySelection();
    partial void OnSelectedUnitChanged(ProductUnitResponse? value) => NotifySelection();
    partial void OnSelectedCatalogProductChanged(ProductResponse? value) => NotifySelection();
    partial void OnCatalogLookupSelectionChanged(ProductResponse? value)
    {
        if (value is not null) SelectCatalogProduct(value);
    }
    partial void OnCountChanged(decimal value)
    {
        OnPropertyChanged(nameof(ConversionPreview));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }
    partial void OnUnitCostChanged(decimal value) => OnPropertyChanged(nameof(TotalCostDisplay));

    public async Task LoadCatalogAsync()
    {
        if (_isLoadingCatalog || CatalogProducts.Count > 0) return;
        _isLoadingCatalog = true;
        try
        {
            var page = await api.GetProductsAsync(pageSize: 200);
            CatalogProducts = page.Items
                .Where(product => product.IsActive)
                .OrderBy(product => product.Name)
                .ToArray();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Product catalog unavailable", FailureMessage(exception));
        }
        finally
        {
            _isLoadingCatalog = false;
        }
    }

    public async Task LookupBarcodeAsync()
    {
        if (IsBusy) return;
        var barcode = ScannerText.Trim();
        if (barcode.Length == 0)
        {
            StatusMessage = "Scan or enter a barcode first.";
            RequestScannerFocus();
            return;
        }

        StatusMessage = "Looking up the exact barcode...";
        IsBusy = true;
        try
        {
            CatalogLookupSelection = null;
            var product = await api.GetProductForReceivingByBarcodeAsync(barcode);
            SelectedProduct = product;
            SelectedUnit = product.SelectedUnit;
            var catalog = await api.GetProductsAsync(search: barcode, pageSize: 50);
            SelectedCatalogProduct = catalog.Items.FirstOrDefault(item => item.Id == product.Id);
            InitializePrices();
            StatusMessage = product.SelectedUnit is null
                ? "The API did not identify the scanned unit."
                : $"Selected {product.Name}, {product.SelectedUnit.Label}.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            SelectedProduct = null;
            SelectedUnit = null;
            SelectedCatalogProduct = null;
            CatalogLookupSelection = null;
            ShowError("Product could not be found", FailureMessage(exception));
        }
        finally
        {
            IsBusy = false;
            ScannerText = "";
            RequestScannerFocus();
        }
    }

    [RelayCommand]
    private async Task SubmitReceiptAsync()
    {
        if (IsBusy) return;
        if (SelectedProduct is null || SelectedUnit is null)
        {
            ShowError("Stock could not be received", "Scan a product unit before receiving stock.");
            RequestScannerFocus();
            return;
        }
        if (!WholeNumber(Count) || Count <= 0 || Count > int.MaxValue)
        {
            ShowError("Stock could not be received", "Enter a whole number greater than zero.");
            return;
        }
        if (!TryUtc(ReceivedDate, ReceivedTime, required: true, out var receivedAtUtc) ||
            !TryUtc(ProductionDate, ProductionTime, required: false, out var productionAtUtc))
        {
            ShowError("Stock could not be received", "Enter both date and time for each supplied receipt or production timestamp.");
            return;
        }
        DateTimeOffset? expiresAtUtc = null;
        if (IsPerishable && !TryUtc(ExpiryDate, ExpiryTime, required: true, out expiresAtUtc))
        {
            ShowError("Stock could not be received", "Expiry/discard date and time are required for perishable products.");
            return;
        }
        if (!IsPerishable) expiresAtUtc = null;
        if (productionAtUtc.HasValue && receivedAtUtc.HasValue && productionAtUtc > receivedAtUtc)
        {
            ShowError("Stock could not be received", "Production time cannot be after received time.");
            return;
        }
        if (expiresAtUtc.HasValue && receivedAtUtc.HasValue && expiresAtUtc <= receivedAtUtc)
        {
            ShowError("Stock could not be received", "Expiry/discard time must be after received time.");
            return;
        }

        StatusMessage = "Receiving stock into bodega...";
        IsBusy = true;
        try
        {
            var result = await api.ReceiveStockAsync(new ReceiveStockRequest(
                SelectedProduct.Id, SelectedUnit.Id, (int)Count,
                UnitCost, SellingPrice, EmployeePrice,
                NullIfWhiteSpace(Reference), NullIfWhiteSpace(Notes), NullIfWhiteSpace(LotCode),
                receivedAtUtc, productionAtUtc, expiresAtUtc));
            StatusMessage = $"Received {result.Count} {result.UnitLabel} = {result.BasePieceQuantity} base pieces. Bodega balance: {result.BodegaStock}; display: {result.DisplayStock}.";
            notifications.ShowSuccess("Stock received", StatusMessage);
            SelectedProduct = null;
            SelectedUnit = null;
            SelectedCatalogProduct = null;
            CatalogLookupSelection = null;
            Count = 1;
            UnitCost = 0;
            SellingPrice = 0;
            EmployeePrice = 0;
            Reference = "";
            Notes = "";
            LotCode = "";
            ReceivedDate = null;
            ReceivedTime = null;
            ProductionDate = null;
            ProductionTime = null;
            ExpiryDate = null;
            ExpiryTime = null;
            ScannerText = "";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Stock could not be received", FailureMessage(exception));
        }
        finally
        {
            IsBusy = false;
            RequestScannerFocus();
        }
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(UnitDetails));
        OnPropertyChanged(nameof(ConversionPreview));
        OnPropertyChanged(nameof(TotalCostDisplay));
        OnPropertyChanged(nameof(IsPerishable));
    }

    private void InitializePrices()
    {
        if (SelectedUnit is null) return;
        var now = StoreDateTime.StoreNow;
        ReceivedDate ??= StoreDateTime.AtStoreMidnight(now);
        ReceivedTime ??= now.TimeOfDay;
        UnitCost = (SelectedCatalogProduct?.CostPrice ?? 0) * SelectedUnit.PiecesPerUnit;
        SellingPrice = SelectedUnit.RegularPrice;
        // A zero employee price means employees use the regular selling price.
        EmployeePrice = SelectedUnit.EmployeePrice > 0
            ? SelectedUnit.EmployeePrice
            : SelectedUnit.RegularPrice;
    }

    private void SelectCatalogProduct(ProductResponse product)
    {
        var baseUnit = product.Units.FirstOrDefault(unit => unit.IsBasePiece && unit.IsActive);
        if (baseUnit is null)
        {
            ShowError("Product cannot be received", $"{product.Name} does not have an active base-piece unit.");
            return;
        }

        SelectedCatalogProduct = product;
        SelectedUnit = baseUnit;
        SelectedProduct = new PosProductResponse(
            product.Id,
            product.SupplierName,
            product.Sku,
            product.Barcode,
            product.Name,
            product.Unit,
            product.RegularPrice,
            product.EmployeePrice,
            product.DisplayStock,
            product.Version,
            product.Units,
            baseUnit,
            product.IsPerishable);
        ScannerText = "";
        InitializePrices();
        StatusMessage = $"Selected {product.Name}, {baseUnit.Label}, by catalog search.";
    }

    private void RequestScannerFocus() => ScannerFocusRequested?.Invoke(this, EventArgs.Empty);
    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        notifications.ShowError(title, message);
    }
    private static bool WholeNumber(decimal value) => value == decimal.Truncate(value);
    private static bool TryUtc(DateTimeOffset? date, TimeSpan? time, bool required, out DateTimeOffset? value)
    {
        value = null;
        if (!date.HasValue && !time.HasValue) return !required;
        if (!date.HasValue || !time.HasValue) return false;
        value = StoreDateTime.CombineStoreDateAndTimeToUtc(date.Value, time.Value);
        return true;
    }
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException
        ? "Cannot reach the store API."
        : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;
}
