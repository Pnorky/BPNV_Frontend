using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public sealed record MovementTypeFilter(string Label, string? Value)
{
    public override string ToString() => Label;
}

public sealed record MovementSortOption(string Label, string SortBy, bool Descending)
{
    public override string ToString() => Label;
}

public partial class ApiStockMovementsViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private bool _historyErrorNotificationShown;
    private IReadOnlyList<ProductResponse> _allProducts = [];
    [ObservableProperty] private IReadOnlyList<ProductResponse> _products = [];
    [ObservableProperty] private ProductResponse? _selectedProduct;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private string _reference = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _statusMessage = "Loading products...";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private IReadOnlyList<StockMovementResponse> _movements = [];
    [ObservableProperty] private string _historySearchText = "";
    [ObservableProperty] private string _historyReference = "";
    [ObservableProperty] private MovementTypeFilter _selectedMovementType;
    [ObservableProperty] private MovementSortOption _selectedMovementSort;
    [ObservableProperty] private DateTimeOffset? _historyFromDate;
    [ObservableProperty] private DateTimeOffset? _historyToDate;
    [ObservableProperty] private int _historyPage = 1;
    [ObservableProperty] private int _historyPageSize = 20;
    [ObservableProperty] private int _historyTotalCount;
    [ObservableProperty] private bool _isHistoryLoading;
    [ObservableProperty] private bool _isHistoryFiltered;
    [ObservableProperty] private string? _historyError;

    public IReadOnlyList<MovementTypeFilter> MovementTypes { get; } =
    [
        new("All movements", null),
        new("Receipts", "Receipt"),
        new("Bodega to Display", "TransferToDisplay"),
        new("Sales", "Sale"),
        new("Opening Display", "OpeningDisplay"),
        new("Opening Bodega", "OpeningBodega"),
        new("Accounts Receivable", "AccountsReceivable"),
        new("Display adjustments in", "DisplayAdjustmentIn"),
        new("Display adjustments out", "DisplayAdjustmentOut"),
        new("Bodega adjustments in", "BodegaAdjustmentIn"),
        new("Bodega adjustments out", "BodegaAdjustmentOut"),
        new("Display spoilage", "DisplaySpoilage"),
        new("Bodega spoilage", "BodegaSpoilage"),
        new("Display usage", "DisplayUsage"),
        new("Bodega usage", "BodegaUsage")
    ];
    public IReadOnlyList<MovementSortOption> MovementSortOptions { get; } =
    [
        new("Newest first", "occurredAt", true),
        new("Oldest first", "occurredAt", false),
        new("Product A-Z", "product", false),
        new("Product Z-A", "product", true),
        new("Movement A-Z", "movementType", false),
        new("Movement Z-A", "movementType", true)
    ];

    public ApiStockMovementsViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        _selectedMovementType = MovementTypes[0];
        _selectedMovementSort = MovementSortOptions[0];
        _ = LoadAsync();
        _ = LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var page = await _api.GetProductsAsync(page: 1, pageSize: 200);
            _allProducts = page.Items;
            ApplyFilter();
            StatusMessage = $"Loaded {_allProducts.Count} products from the database.";
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            ShowError("Products could not be loaded", exception.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TransferAsync()
    {
        if (SelectedProduct is null || Quantity <= 0) { ShowError("Stock not moved", "Select a product and enter a positive quantity."); return; }
        if (Quantity > SelectedProduct.BodegaStock) { ShowError("Stock not moved", "Transfer quantity cannot exceed Bodega stock."); return; }
        IsBusy = true;
        try
        {
            await _api.TransferToDisplayAsync(new TransferStockRequest(SelectedProduct.Id, Quantity, NullIfWhiteSpace(Reference), NullIfWhiteSpace(Notes)));
            IsBusy = false;
            await LoadAsync();
            await LoadHistoryAsync();
            StatusMessage = "Stock moved from Bodega to Display.";
            _notifications.ShowSuccess("Stock moved", StatusMessage);
            Quantity = 1; Reference = ""; Notes = "";
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException) { ShowError("Stock not moved", exception.Message); }
        finally { IsBusy = false; }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        if (IsHistoryLoading) return;
        IsHistoryLoading = true;
        HistoryError = null;
        try
        {
            var fromUtc = HistoryFromDate?.LocalDateTime.Date.ToUniversalTime();
            var toUtc = HistoryToDate?.LocalDateTime.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
            var result = await _api.GetStockMovementsAsync(
                NullIfWhiteSpace(HistorySearchText),
                SelectedMovementType.Value,
                NullIfWhiteSpace(HistoryReference),
                fromUtc,
                toUtc,
                HistoryPage,
                HistoryPageSize,
                SelectedMovementSort.SortBy,
                SelectedMovementSort.Descending ? "desc" : "asc");
            Movements = result.Items;
            _historyErrorNotificationShown = false;
            HistoryPage = result.Page;
            HistoryTotalCount = result.TotalCount;
            IsHistoryFiltered = !string.IsNullOrWhiteSpace(HistorySearchText) ||
                                SelectedMovementType.Value is not null ||
                                !string.IsNullOrWhiteSpace(HistoryReference) ||
                                HistoryFromDate.HasValue || HistoryToDate.HasValue;
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            Movements = [];
            HistoryTotalCount = 0;
            HistoryError = exception is HttpRequestException ? "Cannot reach the store API." : exception.Message;
            if (!_historyErrorNotificationShown)
            {
                _historyErrorNotificationShown = true;
                _notifications.ShowError("Movement history could not be loaded", HistoryError);
            }
        }
        finally
        {
            IsHistoryLoading = false;
        }
    }

    [RelayCommand]
    private async Task ApplyHistoryFiltersAsync()
    {
        if (HistoryFromDate.HasValue && HistoryToDate.HasValue && HistoryFromDate.Value.Date > HistoryToDate.Value.Date)
        {
            HistoryError = "The start date must be earlier than or equal to the end date.";
            _notifications.ShowError("Invalid date range", HistoryError);
            return;
        }
        HistoryPage = 1;
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task ClearHistoryFiltersAsync()
    {
        HistorySearchText = "";
        HistoryReference = "";
        SelectedMovementType = MovementTypes[0];
        SelectedMovementSort = MovementSortOptions[0];
        HistoryFromDate = null;
        HistoryToDate = null;
        HistoryPage = 1;
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task PreviousHistoryPageAsync()
    {
        if (HistoryPage <= 1) return;
        HistoryPage--;
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task NextHistoryPageAsync()
    {
        if (HistoryPage * HistoryPageSize >= HistoryTotalCount) return;
        HistoryPage++;
        await LoadHistoryAsync();
    }

    partial void OnHistoryPageSizeChanged(int value)
    {
        if (value <= 0) return;
        HistoryPage = 1;
        _ = LoadHistoryAsync();
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        Products = string.IsNullOrEmpty(search)
            ? _allProducts
            : _allProducts.Where(product =>
                $"{product.Name} {product.Sku} {product.SupplierName} {product.Barcode} {string.Join(' ', product.Units.Select(unit => unit.Barcode))}"
                    .Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (SelectedProduct is not null && !Products.Contains(SelectedProduct)) SelectedProduct = null;
    }
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }
}
