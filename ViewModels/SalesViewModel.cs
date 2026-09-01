using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Services;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class ApiCartLine(PosProductResponse product, ProductUnitResponse unit, ApiCustomerType customerType) : ObservableObject
{
    [ObservableProperty] private int _count = 1;
    [ObservableProperty] private decimal _unitPrice = PriceFor(unit, customerType);

    public PosProductResponse Product { get; } = product;
    public ProductUnitResponse Unit { get; } = unit;
    public Guid UnitId => Unit.Id;
    public string ProductName => Product.Name;
    public string UnitLabel => Unit.Label;
    public int BasePieceQuantity => Count * Unit.PiecesPerUnit;
    public decimal Amount => Count * UnitPrice;
    public string UnitPriceDisplay => $"₱{UnitPrice:N2}";
    public string AmountDisplay => $"₱{Amount:N2}";
    public string ConversionDisplay => Unit.PiecesPerUnit == 1 ? "1 piece each" : $"{Unit.PiecesPerUnit} pieces each";

    partial void OnCountChanged(int value) => NotifyTotals();
    partial void OnUnitPriceChanged(decimal value) => NotifyTotals();

    public void ApplyCustomerType(ApiCustomerType customerType) => UnitPrice = PriceFor(Unit, customerType);

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(BasePieceQuantity));
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(UnitPriceDisplay));
        OnPropertyChanged(nameof(AmountDisplay));
    }

    public static decimal PriceFor(ProductUnitResponse unit, ApiCustomerType customerType) =>
        customerType == ApiCustomerType.Employee && unit.EmployeePrice > 0
            ? unit.EmployeePrice
            : unit.RegularPrice;
}

public partial class SalesViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private IReadOnlyList<PosProductResponse> _products = [];
    private Guid? _idempotencyKey;
    private bool _suppressCartMutation;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _scannerText = "";
    [ObservableProperty] private ApiCustomerType _selectedCustomerType = ApiCustomerType.Regular;
    [ObservableProperty] private EmployeeResponse? _selectedEmployee;
    [ObservableProperty] private IReadOnlyList<EmployeeResponse> _employees = [];
    [ObservableProperty] private IReadOnlyList<PosProductResponse> _filteredProducts = [];
    [ObservableProperty] private string _statusMessage = "Loading the current POS catalog...";
    [ObservableProperty] private bool _isBusy;

    public IReadOnlyList<ApiCustomerType> CustomerTypes { get; } = Enum.GetValues<ApiCustomerType>();
    public bool IsEmployeeSale => SelectedCustomerType == ApiCustomerType.Employee;
    public ObservableCollection<ApiCartLine> Cart { get; } = [];
    public string CartSummary => Cart.Count == 0 ? "No units added" : $"{Cart.Sum(line => line.Count)} units / {Cart.Sum(line => line.BasePieceQuantity)} pieces";
    public string TotalDisplay => $"₱{Cart.Sum(line => line.Amount):N2}";
    public event EventHandler? ScannerFocusRequested;

    public SalesViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        Cart.CollectionChanged += OnCartChanged;
        _ = LoadCatalogAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCustomerTypeChanged(ApiCustomerType value)
    {
        if (value != ApiCustomerType.Employee) SelectedEmployee = null;
        foreach (var line in Cart) line.ApplyCustomerType(value);
        OnPropertyChanged(nameof(IsEmployeeSale));
        NotifyCartTotals();
        MarkCartChanged();
    }

    partial void OnSelectedEmployeeChanged(EmployeeResponse? value) => MarkCartChanged();

    [RelayCommand]
    public async Task LoadCatalogAsync()
    {
        if (IsBusy) return;
        StatusMessage = "Loading the current POS catalog...";
        IsBusy = true;
        try
        {
            var productsTask = _api.GetPosProductsAsync(pageSize: 200);
            var employeesTask = _api.GetEmployeesAsync();
            await Task.WhenAll(productsTask, employeesTask);
            var page = await productsTask;
            _products = page.Items;
            Employees = (await employeesTask).Where(employee => employee.IsActive).OrderBy(employee => employee.Name).ToArray();
            ApplyFilter();
            StatusMessage = $"Loaded {_products.Count} sellable database products. Scan a barcode or add a base piece.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = FailureMessage(exception);
        }
        finally
        {
            IsBusy = false;
            RequestScannerFocus();
        }
    }

    public async Task ScanBarcodeAsync()
    {
        if (IsBusy) return;
        var barcode = ScannerText.Trim();
        if (barcode.Length == 0)
        {
            RequestScannerFocus();
            return;
        }

        StatusMessage = "Looking up the exact barcode...";
        IsBusy = true;
        try
        {
            var product = await _api.GetProductByBarcodeAsync(barcode);
            if (product.SelectedUnit is null)
                StatusMessage = "The API did not return a selected unit for that barcode.";
            else
                AddUnit(product, product.SelectedUnit);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = FailureMessage(exception);
        }
        finally
        {
            ScannerText = "";
            IsBusy = false;
            RequestScannerFocus();
        }
    }

    [RelayCommand]
    private void AddProduct(PosProductResponse? product)
    {
        if (product is null) return;
        var piece = product.Units.FirstOrDefault(unit => unit.IsBasePiece && unit.IsActive);
        if (piece is null)
        {
            StatusMessage = $"{product.Name} has no active base-piece unit.";
            return;
        }
        AddUnit(product, piece);
        RequestScannerFocus();
    }

    [RelayCommand]
    private void DecreaseQuantity(ApiCartLine? line)
    {
        if (line is null) return;
        if (line.Count == 1) Cart.Remove(line);
        else line.Count--;
        MarkCartChanged();
        NotifyCartTotals();
        RequestScannerFocus();
    }

    [RelayCommand]
    private void IncreaseQuantity(ApiCartLine? line)
    {
        if (line is null) return;
        if (!CanSetCount(line, line.Count + 1, out var message))
        {
            StatusMessage = message;
            return;
        }
        line.Count++;
        MarkCartChanged();
        NotifyCartTotals();
        RequestScannerFocus();
    }

    [RelayCommand]
    private void RemoveLine(ApiCartLine? line)
    {
        if (line is not null) Cart.Remove(line);
        RequestScannerFocus();
    }

    [RelayCommand]
    private void ClearSale()
    {
        Cart.Clear();
        StatusMessage = "Sale cleared.";
        RequestScannerFocus();
    }

    [RelayCommand]
    private async Task CompleteSaleAsync()
    {
        if (IsBusy) return;
        if (Cart.Count == 0)
        {
            ShowError("Sale not completed", "Add at least one unit before completing the sale.");
            return;
        }

        if (IsEmployeeSale && SelectedEmployee is null)
        {
            ShowError("Employee required", "Select the employee making this purchase before completing the sale.");
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            ShowError("Payment unavailable", "Cannot open checkout payment because the main application window is unavailable.");
            return;
        }

        var payment = await new PaymentDialog
        {
            DataContext = new PaymentDialogViewModel(Cart.Sum(line => line.Amount))
        }.ShowDialog<PaymentDialogResult?>(owner);
        if (payment is null)
        {
            StatusMessage = "Payment cancelled. The sale remains in the cart.";
            RequestScannerFocus();
            return;
        }

        _idempotencyKey ??= Guid.NewGuid();
        StatusMessage = "Submitting the sale for server pricing and stock validation...";
        IsBusy = true;
        try
        {
            var sale = await _api.CreateSaleAsync(new CreateSaleRequest(
                _idempotencyKey.Value,
                SelectedCustomerType,
                payment.PaymentMethod,
                Cart.Select(line => new CreateSaleLineRequest(line.UnitId, line.Count)).ToArray(),
                IsEmployeeSale ? SelectedEmployee?.Id : null));
            _suppressCartMutation = true;
            Cart.Clear();
            _suppressCartMutation = false;
            _idempotencyKey = null;
            SelectedEmployee = null;
            StatusMessage = $"{sale.SaleNumber} completed via {sale.PaymentMethod}. Server total: ₱{sale.Total:N2}{(sale.IsIdempotentReplay ? " (confirmed retry)" : "")}.";
            _notifications.ShowSuccess("Sale completed", StatusMessage);
            await TryRefreshCatalogAsync();
        }
        catch (ApiClientException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            ShowError("Sale not completed", exception.Message);
            await TryRefreshCatalogAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Sale not completed", FailureMessage(exception));
        }
        finally
        {
            _suppressCartMutation = false;
            IsBusy = false;
            RequestScannerFocus();
        }
    }

    private void AddUnit(PosProductResponse product, ProductUnitResponse unit)
    {
        if (ApiCartLine.PriceFor(unit, SelectedCustomerType) < 0)
        {
            StatusMessage = $"{product.Name} has an invalid selling price.";
            return;
        }

        var existing = Cart.FirstOrDefault(line => line.UnitId == unit.Id);
        if (existing is not null)
        {
            if (!CanSetCount(existing, existing.Count + 1, out var message))
            {
                StatusMessage = message;
                return;
            }
            existing.Count++;
        }
        else
        {
            var line = new ApiCartLine(product, unit, SelectedCustomerType);
            if (!CanSetCount(line, 1, out var message))
            {
                StatusMessage = message;
                return;
            }
            line.PropertyChanged += OnCartLineChanged;
            Cart.Add(line);
        }
        MarkCartChanged();
        NotifyCartTotals();
        StatusMessage = $"Added {product.Name}, {unit.Label} ({unit.PiecesPerUnit} base piece{(unit.PiecesPerUnit == 1 ? "" : "s")}).";
    }

    private bool CanSetCount(ApiCartLine line, int count, out string message)
    {
        var otherPieces = Cart.Where(item => item.Product.Id == line.Product.Id && !ReferenceEquals(item, line))
            .Sum(item => item.BasePieceQuantity);
        var requested = otherPieces + checked(count * line.Unit.PiecesPerUnit);
        if (requested <= line.Product.DisplayStock)
        {
            message = "";
            return true;
        }
        message = $"{line.Product.Name} has {line.Product.DisplayStock} base pieces on display; this cart would require {requested}.";
        return false;
    }

    private async Task RefreshCatalogCoreAsync()
    {
        var selectedEmployeeId = SelectedEmployee?.Id;
        var productsTask = _api.GetPosProductsAsync(pageSize: 200);
        var employeesTask = _api.GetEmployeesAsync();
        await Task.WhenAll(productsTask, employeesTask);
        var page = await productsTask;
        _products = page.Items;
        Employees = (await employeesTask).Where(employee => employee.IsActive).OrderBy(employee => employee.Name).ToArray();
        SelectedEmployee = Employees.FirstOrDefault(employee => employee.Id == selectedEmployeeId);
        ApplyFilter();
    }

    private async Task TryRefreshCatalogAsync()
    {
        try
        {
            await RefreshCatalogCoreAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage += " Catalog refresh failed: " + FailureMessage(exception);
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        FilteredProducts = (search.Length == 0
                ? _products
                : _products.Where(product =>
                    product.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    product.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    product.SupplierName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    product.Units.Any(unit => unit.Barcode?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)))
            .OrderBy(product => product.Name)
            .ToList();
    }

    private void OnCartChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (ApiCartLine line in e.OldItems) line.PropertyChanged -= OnCartLineChanged;
        MarkCartChanged();
        NotifyCartTotals();
    }

    private void OnCartLineChanged(object? sender, PropertyChangedEventArgs e) => NotifyCartTotals();
    private void MarkCartChanged() { if (!_suppressCartMutation) _idempotencyKey = null; }
    private void NotifyCartTotals()
    {
        OnPropertyChanged(nameof(CartSummary));
        OnPropertyChanged(nameof(TotalDisplay));
    }
    private void RequestScannerFocus() => ScannerFocusRequested?.Invoke(this, EventArgs.Empty);
    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException
        ? "Cannot reach the store API."
        : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;
}
