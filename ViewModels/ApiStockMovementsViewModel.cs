using AvaloniaApp.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Views.Dialogs;
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

public sealed record StockLocationOption(string Label, ApiInventoryStockLocation Value)
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
    [ObservableProperty] private ProductResponse? _bodegaBalanceProduct;
    [ObservableProperty] private decimal _bodegaCountedQuantity;
    [ObservableProperty] private string _bodegaCountNotes = "";
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
    [ObservableProperty] private ProductResponse? _spoilageProduct;
    [ObservableProperty] private ProductUnitResponse? _spoilageUnit;
    [ObservableProperty] private StockLocationOption? _spoilageLocation;
    [ObservableProperty] private InventoryLotBalanceResponse? _spoilageLot;
    [ObservableProperty] private int _spoilageCount = 1;
    [ObservableProperty] private ApiSpoilageReason _spoilageReason = ApiSpoilageReason.Expired;
    [ObservableProperty] private string _spoilageNotes = "";
    [ObservableProperty] private IReadOnlyList<ProductUnitResponse> _spoilageUnits = [];
    [ObservableProperty] private IReadOnlyList<InventoryLotBalanceResponse> _spoilageLots = [];
    [ObservableProperty] private bool _isLoadingLots;

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
    public IReadOnlyList<StockLocationOption> StockLocations { get; } =
    [
        new("Display", ApiInventoryStockLocation.Display),
        new("Bodega", ApiInventoryStockLocation.Bodega)
    ];
    public IReadOnlyList<ApiSpoilageReason> SpoilageReasons { get; } = Enum.GetValues<ApiSpoilageReason>();
    public bool SpoilageProductIsPerishable => SpoilageProduct?.IsPerishable == true;
    public bool RequiresSpoilageNotes => SpoilageReason == ApiSpoilageReason.Other;
    public int? BodegaVariance => WholeBodegaCount(out var counted) && BodegaBalanceProduct is not null
        ? counted - BodegaBalanceProduct.BodegaStock
        : null;
    public string BodegaCurrentBalanceDisplay => BodegaBalanceProduct is null
        ? "Select a product"
        : $"{BodegaBalanceProduct.BodegaStock:N0} {BodegaBalanceProduct.Unit}";
    public string BodegaDisplayBalanceDisplay => BodegaBalanceProduct is null
        ? "Select a product"
        : $"{BodegaBalanceProduct.DisplayStock:N0} {BodegaBalanceProduct.Unit}";
    public string BodegaVarianceDisplay => BodegaVariance is not int variance
        ? "Enter a whole physical quantity"
        : variance == 0
            ? "The entered quantity matches the current Bodega balance."
            : variance > 0
                ? $"Bodega will increase by {variance:N0} {BodegaBalanceProduct?.Unit}."
                : $"Bodega will decrease by {Math.Abs(variance):N0} {BodegaBalanceProduct?.Unit}.";

    public ApiStockMovementsViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        _selectedMovementType = MovementTypes[0];
        _selectedMovementSort = MovementSortOptions[0];
        _spoilageLocation = StockLocations[0];
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
            if (SelectedProduct is not null)
                SelectedProduct = _allProducts.FirstOrDefault(product => product.Id == SelectedProduct.Id);
            if (BodegaBalanceProduct is not null)
                BodegaBalanceProduct = _allProducts.FirstOrDefault(product => product.Id == BodegaBalanceProduct.Id);
            if (SpoilageProduct is not null)
                SpoilageProduct = _allProducts.FirstOrDefault(product => product.Id == SpoilageProduct.Id);
            StatusMessage = $"Loaded {_allProducts.Count} products from the database.";
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            ShowError("Products could not be loaded", FailureMessage(exception));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TransferAsync()
    {
        if (SelectedProduct is null || Quantity <= 0) { ShowError("Stock could not be moved", "Select a product and enter a quantity greater than zero."); return; }
        if (Quantity > SelectedProduct.BodegaStock) { ShowError("Stock could not be moved", "The transfer quantity cannot be greater than the available Bodega stock."); return; }
        IsBusy = true;
        try
        {
            await _api.TransferToDisplayAsync(new TransferStockRequest(SelectedProduct.Id, Quantity, NullIfWhiteSpace(Reference), NullIfWhiteSpace(Notes)));
            IsBusy = false;
            await LoadAsync();
            await LoadHistoryAsync();
            StatusMessage = "Stock moved from Bodega to Display.";
            _notifications.ShowSuccess("Stock moved successfully", StatusMessage);
            Quantity = 1; Reference = ""; Notes = "";
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException) { ShowError("Stock could not be moved", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SetBodegaBalanceAsync()
    {
        if (IsBusy) return;
        if (!TryBuildBodegaBalanceRequest(out var request, out var error) || request is null || BodegaBalanceProduct is null)
        {
            ShowError("Bodega balance not recorded", error);
            return;
        }
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            ShowError("Bodega balance not recorded", "The confirmation dialog could not be opened.");
            return;
        }

        var productName = BodegaBalanceProduct.Name;
        var previous = BodegaBalanceProduct.BodegaStock;
        var variance = request.CountedQuantity - previous;
        var confirmation = new ConfirmDialog();
        confirmation.SetConfirmation(
            "Replace the Bodega balance?",
            $"Set {productName} Bodega stock from {previous:N0} to {request.CountedQuantity:N0}? This records an audited {variance:+#;-#;0} stock movement and does not receive additional stock.",
            "Set balance");
        await confirmation.ShowDialog(owner);
        if (!confirmation.Confirmed) return;

        IsBusy = true;
        try
        {
            var result = await _api.RecordStockCountAsync(request);
            IsBusy = false;
            await LoadAsync();
            await LoadHistoryAsync();
            BodegaCountNotes = "";
            StatusMessage = $"{productName} Bodega balance changed from {result.PreviousQuantity:N0} to {result.CountedQuantity:N0}.";
            _notifications.ShowSuccess("Bodega balance recorded", StatusMessage);
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            if (exception is ApiClientException)
            {
                IsBusy = false;
                await LoadAsync();
            }
            ShowError("Bodega balance not recorded", FailureMessage(exception));
        }
        finally { IsBusy = false; }
    }

    public bool TryBuildBodegaBalanceRequest(out RecordStockCountRequest? request, out string error)
    {
        request = null;
        error = "";
        if (BodegaBalanceProduct?.CanRecordStockCount != true)
        {
            error = "Select an active non-perishable product.";
            return false;
        }
        if (!WholeBodegaCount(out var counted))
        {
            error = "The actual Bodega quantity must be a whole number from zero to 2,147,483,647.";
            return false;
        }
        if (counted == BodegaBalanceProduct.BodegaStock)
        {
            error = "The entered quantity already matches the current Bodega balance.";
            return false;
        }
        if (BodegaCountNotes.Trim().Length > 500)
        {
            error = "Notes must not exceed 500 characters.";
            return false;
        }

        request = new RecordStockCountRequest(
            BodegaBalanceProduct.Id,
            ApiInventoryStockLocation.Bodega,
            counted,
            BodegaBalanceProduct.Version,
            NullIfWhiteSpace(BodegaCountNotes));
        return true;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnBodegaBalanceProductChanged(ProductResponse? value)
    {
        BodegaCountedQuantity = value?.BodegaStock ?? 0;
        NotifyBodegaBalancePreview();
    }
    partial void OnBodegaCountedQuantityChanged(decimal value) => NotifyBodegaBalancePreview();
    partial void OnSpoilageProductChanged(ProductResponse? value)
    {
        SpoilageUnits = value?.Units.Where(unit => unit.IsActive).ToArray() ?? [];
        SpoilageUnit = SpoilageUnits.FirstOrDefault(unit => unit.IsBasePiece) ?? SpoilageUnits.FirstOrDefault();
        SpoilageLot = null;
        SpoilageLots = [];
        OnPropertyChanged(nameof(SpoilageProductIsPerishable));
        _ = LoadSpoilageLotsAsync();
    }
    partial void OnSpoilageLocationChanged(StockLocationOption? value) => _ = LoadSpoilageLotsAsync();
    partial void OnSpoilageReasonChanged(ApiSpoilageReason value) => OnPropertyChanged(nameof(RequiresSpoilageNotes));

    [RelayCommand]
    private async Task LoadSpoilageLotsAsync()
    {
        if (SpoilageProduct?.IsPerishable != true || SpoilageLocation is null)
        {
            SpoilageLots = [];
            SpoilageLot = null;
            return;
        }

        IsLoadingLots = true;
        try
        {
            SpoilageLots = await _api.GetInventoryLotsAsync(
                SpoilageProduct.Id, SpoilageLocation.Value, includeExpired: false);
            SpoilageLot = SpoilageLots.FirstOrDefault();
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            SpoilageLots = [];
            SpoilageLot = null;
            ShowError("Lots could not be loaded", FailureMessage(exception));
        }
        finally { IsLoadingLots = false; }
    }

    [RelayCommand]
    private async Task RecordSpoilageAsync()
    {
        if (IsBusy) return;
        if (SpoilageProduct is null || SpoilageUnit is null || SpoilageLocation is null || SpoilageCount <= 0)
        {
            ShowError("Spoilage could not be recorded", "Select a product, unit, location, and quantity greater than zero.");
            return;
        }
        if (SpoilageProduct.IsPerishable && SpoilageLot is null)
        {
            ShowError("Spoilage could not be recorded", "Select an eligible unexpired lot for this perishable product.");
            return;
        }
        if (RequiresSpoilageNotes && string.IsNullOrWhiteSpace(SpoilageNotes))
        {
            ShowError("Spoilage could not be recorded", "Notes are required when the reason is Other.");
            return;
        }
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner }) return;
        var confirmation = new ConfirmDialog();
        confirmation.SetConfirmation(
            "Record spoilage?",
            $"Remove {SpoilageCount:N0} {SpoilageUnit.Label} of {SpoilageProduct.Name} from {SpoilageLocation.Label} as {SpoilageReason}? This cannot be undone.",
            "Record spoilage");
        await confirmation.ShowDialog(owner);
        if (!confirmation.Confirmed) return;

        IsBusy = true;
        try
        {
            var result = await _api.RecordSpoilageAsync(new RecordSpoilageRequest(
                SpoilageProduct.Id, SpoilageUnit.Id, SpoilageLocation.Value, SpoilageLot?.LotId,
                SpoilageCount, SpoilageReason, NullIfWhiteSpace(SpoilageNotes)));
            IsBusy = false;
            await LoadAsync();
            await LoadHistoryAsync();
            await LoadSpoilageLotsAsync();
            SpoilageCount = 1;
            SpoilageNotes = "";
            StatusMessage = $"Recorded {result.BasePieceQuantity:N0} spoiled base pieces from {result.Location}.";
            _notifications.ShowSuccess("Spoilage recorded", StatusMessage);
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            ShowError("Spoilage could not be recorded", FailureMessage(exception));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        if (IsHistoryLoading) return;
        IsHistoryLoading = true;
        HistoryError = null;
        try
        {
            var (fromUtc, toUtcExclusive) = StoreDateTime.GetUtcDateRange(HistoryFromDate, HistoryToDate);
            var result = await _api.GetStockMovementsAsync(
                NullIfWhiteSpace(HistorySearchText),
                SelectedMovementType.Value,
                NullIfWhiteSpace(HistoryReference),
                fromUtc,
                toUtcExclusive,
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
    private bool WholeBodegaCount(out int value)
    {
        if (BodegaCountedQuantity != decimal.Truncate(BodegaCountedQuantity) || BodegaCountedQuantity is < 0 or > int.MaxValue)
        {
            value = 0;
            return false;
        }
        value = decimal.ToInt32(BodegaCountedQuantity);
        return true;
    }
    private void NotifyBodegaBalancePreview()
    {
        OnPropertyChanged(nameof(BodegaVariance));
        OnPropertyChanged(nameof(BodegaCurrentBalanceDisplay));
        OnPropertyChanged(nameof(BodegaDisplayBalanceDisplay));
        OnPropertyChanged(nameof(BodegaVarianceDisplay));
    }
    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }
    private static string FailureMessage(Exception exception) => exception switch
    {
        HttpRequestException => "We could not connect to the store.",
        TaskCanceledException => "The store took too long to respond. Please try again.",
        _ => exception.Message
    };
}
