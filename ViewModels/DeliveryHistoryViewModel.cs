using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public sealed record DeliverySortOption(string Label, string SortBy, bool Descending)
{
    public override string ToString() => Label;
}

public partial class DeliveryHistoryRowViewModel(DeliveryHistoryItemResponse delivery) : ObservableObject
{
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isDetailLoading;
    [ObservableProperty] private string? _detailError;
    [ObservableProperty] private DeliveryHistoryDetailResponse? _detail;

    public DeliveryHistoryItemResponse Delivery { get; } = delivery;
    public string DetailsActionLabel => IsExpanded ? "Hide details" : "View details";
    public string ChevronKind => IsExpanded ? "ChevronDown" : "ChevronRight";
    public bool HasDetail => Detail is not null;
    public bool HasDetailError => !string.IsNullOrWhiteSpace(DetailError);

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(DetailsActionLabel));
        OnPropertyChanged(nameof(ChevronKind));
    }

    partial void OnDetailChanged(DeliveryHistoryDetailResponse? value) => OnPropertyChanged(nameof(HasDetail));
    partial void OnDetailErrorChanged(string? value) => OnPropertyChanged(nameof(HasDetailError));
}

public partial class DeliveryHistoryViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private CancellationTokenSource? _listCancellation;
    private CancellationTokenSource? _detailCancellation;
    private int _listGeneration;
    private int _pageGeneration;
    private bool _suppressPageSizeChange;
    private string? _lastNotifiedError;
    private DeliveryHistoryRowViewModel? _expandedRow;
    private AppliedFilters _appliedFilters;

    [ObservableProperty] private IReadOnlyList<DeliveryHistoryRowViewModel> _rows = [];
    [ObservableProperty] private IReadOnlyList<SupplierResponse> _suppliers = [];
    [ObservableProperty] private string _receiptNumberSearch = "";
    [ObservableProperty] private SupplierResponse? _selectedSupplier;
    [ObservableProperty] private DateTimeOffset? _fromDate;
    [ObservableProperty] private DateTimeOffset? _toDate;
    [ObservableProperty] private DeliverySortOption _selectedSort;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _selectedPageSize = 20;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isFiltered;
    [ObservableProperty] private string? _listError;

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50];
    public IReadOnlyList<DeliverySortOption> SortOptions { get; } =
    [
        new("Newest delivery first", "deliveryAt", true),
        new("Oldest delivery first", "deliveryAt", false),
        new("Newest recorded first", "completedAt", true),
        new("Receipt number A-Z", "receiptNumber", false),
        new("Receipt number Z-A", "receiptNumber", true),
        new("Highest total cost", "totalCost", true),
        new("Lowest total cost", "totalCost", false)
    ];

    public bool HasRows => Rows.Count > 0;
    public bool HasListError => HasRows && !string.IsNullOrWhiteSpace(ListError);
    public bool ShowState => !HasRows;
    public bool ShowStateIcon => !IsLoading;
    public string StateIcon => ListError is not null ? "CircleAlert" : IsFiltered ? "SearchX" : "History";
    public string StateTitle => IsLoading
        ? "Loading deliveries"
        : ListError is not null
            ? "Unable to load deliveries"
            : IsFiltered ? "No matching deliveries" : "No deliveries available";
    public string StateMessage => IsLoading
        ? "Retrieving completed Batch Receive deliveries."
        : ListError is not null
            ? ListError
            : IsFiltered
                ? "Adjust or clear the filters to see other deliveries."
                : "Complete a Batch Receive delivery to create the first history record.";
    public string StateActionText => ListError is not null ? "Try Again" : "Clear Filters";
    public bool HasStateAction => ListError is not null || IsFiltered;
    public IRelayCommand StateActionCommand => ListError is not null ? LoadCommand : ClearFiltersCommand;
    public string PageSummary
    {
        get
        {
            if (TotalCount == 0) return "0 of 0";
            var start = (Page - 1) * SelectedPageSize + 1;
            var end = Math.Min(Page * SelectedPageSize, TotalCount);
            return $"{start:N0}-{end:N0} of {TotalCount:N0}";
        }
    }
    public string SortSummary => $"Sorted by {_appliedFilters.Sort.Label}";
    private bool CanPreviousPage => !IsLoading && Page > 1;
    private bool CanNextPage => !IsLoading && Page * SelectedPageSize < TotalCount;

    public DeliveryHistoryViewModel(
        StoreApiClient api,
        INotificationService notifications,
        bool loadOnCreate = true)
    {
        _api = api;
        _notifications = notifications;
        _selectedSort = SortOptions[0];
        _appliedFilters = new(null, null, null, null, _selectedSort);
        if (loadOnCreate) _ = InitializeAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        await LoadPageAsync(false);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;
        await LoadPageAsync(false);
        await LoadSuppliersAsync();
    }

    [RelayCommand]
    public async Task ApplyFiltersAsync()
    {
        if (FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date)
        {
            ListError = "The start date must be earlier than or equal to the end date.";
            NotifyState();
            _notifications.ShowError("Invalid delivery date range", ListError);
            return;
        }

        _appliedFilters = new(
            NullIfWhiteSpace(ReceiptNumberSearch),
            SelectedSupplier?.Id,
            FromDate,
            ToDate,
            SelectedSort);
        IsFiltered = _appliedFilters.IsFiltered;
        Page = 1;
        ClearCurrentPage();
        await LoadPageAsync(false);
    }

    [RelayCommand]
    public async Task ClearFiltersAsync()
    {
        ReceiptNumberSearch = "";
        SelectedSupplier = null;
        FromDate = null;
        ToDate = null;
        SelectedSort = SortOptions[0];
        _appliedFilters = new(null, null, null, null, SortOptions[0]);
        IsFiltered = false;
        Page = 1;
        ClearCurrentPage();
        await LoadPageAsync(false);
    }

    [RelayCommand(CanExecute = nameof(CanPreviousPage))]
    private async Task PreviousPageAsync()
    {
        var previousPage = Page;
        Page--;
        CollapseDetails();
        await LoadPageAsync(false);
        if (ListError is not null) Page = previousPage;
    }

    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private async Task NextPageAsync()
    {
        var previousPage = Page;
        Page++;
        CollapseDetails();
        await LoadPageAsync(false);
        if (ListError is not null) Page = previousPage;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleDetailsAsync(DeliveryHistoryRowViewModel? row)
    {
        if (row is null) return;
        if (ReferenceEquals(_expandedRow, row))
        {
            row.IsExpanded = false;
            _expandedRow = null;
            return;
        }

        if (_expandedRow is not null) _expandedRow.IsExpanded = false;
        _expandedRow = row;
        row.IsExpanded = true;
        if (row.Detail is null && !row.IsDetailLoading) await LoadDetailAsync(row);
    }

    [RelayCommand]
    private Task RetryDetailAsync(DeliveryHistoryRowViewModel? row) =>
        row is null ? Task.CompletedTask : LoadDetailAsync(row);

    partial void OnSelectedPageSizeChanged(int oldValue, int newValue)
    {
        if (_suppressPageSizeChange || !PageSizeOptions.Contains(newValue)) return;
        _ = ChangePageSizeAsync(oldValue);
    }

    private async Task ChangePageSizeAsync(int previousPageSize)
    {
        var previousPage = Page;
        Page = 1;
        CollapseDetails();
        await LoadPageAsync(false);
        if (ListError is null) return;
        _suppressPageSizeChange = true;
        SelectedPageSize = previousPageSize;
        _suppressPageSizeChange = false;
        Page = previousPage;
    }

    partial void OnRowsChanged(IReadOnlyList<DeliveryHistoryRowViewModel> value) => NotifyState();
    partial void OnPageChanged(int value) => NotifyPaging();
    partial void OnTotalCountChanged(int value) => NotifyPaging();
    partial void OnIsLoadingChanged(bool value) => NotifyState();
    partial void OnIsFilteredChanged(bool value) => NotifyState();
    partial void OnListErrorChanged(string? value) => NotifyState();

    private async Task InitializeAsync()
    {
        await Task.WhenAll(LoadSuppliersAsync(), LoadPageAsync(false));
    }

    private async Task LoadSuppliersAsync()
    {
        try
        {
            var selectedId = SelectedSupplier?.Id;
            Suppliers = (await _api.GetSuppliersAsync(includeInactive: true))
                .OrderBy(supplier => supplier.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            SelectedSupplier = selectedId.HasValue
                ? Suppliers.FirstOrDefault(supplier => supplier.Id == selectedId.Value)
                : null;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            _notifications.ShowError("Supplier filters could not be loaded", FailureMessage(exception));
        }
    }

    private async Task LoadPageAsync(bool replacePage)
    {
        var generation = ++_listGeneration;
        _listCancellation?.Cancel();
        _listCancellation?.Dispose();
        _listCancellation = new CancellationTokenSource();
        var cancellationToken = _listCancellation.Token;
        if (replacePage) Rows = [];
        IsLoading = true;
        ListError = null;
        try
        {
            var (fromUtc, toUtcExclusive) = StoreDateTime.GetUtcDateRange(_appliedFilters.FromDate, _appliedFilters.ToDate);
            var result = await _api.GetDeliveryHistoryAsync(
                _appliedFilters.ReceiptNumber,
                _appliedFilters.SupplierId,
                null,
                fromUtc,
                toUtcExclusive,
                Page,
                SelectedPageSize,
                _appliedFilters.Sort.SortBy,
                _appliedFilters.Sort.Descending ? "desc" : "asc",
                cancellationToken);
            if (generation != _listGeneration) return;

            Rows = result.Items.Select(item => new DeliveryHistoryRowViewModel(item)).ToArray();
            Page = result.Page;
            TotalCount = result.TotalCount;
            _lastNotifiedError = null;
            _pageGeneration++;
            CollapseDetails();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            if (generation != _listGeneration) return;
            ListError = FailureMessage(exception);
            if (!HasRows) TotalCount = 0;
            if (!string.Equals(_lastNotifiedError, ListError, StringComparison.Ordinal))
            {
                _lastNotifiedError = ListError;
                _notifications.ShowError("Delivery history could not be loaded", ListError);
            }
        }
        finally
        {
            if (generation == _listGeneration) IsLoading = false;
        }
    }

    private async Task LoadDetailAsync(DeliveryHistoryRowViewModel row)
    {
        _detailCancellation?.Cancel();
        _detailCancellation?.Dispose();
        _detailCancellation = new CancellationTokenSource();
        var cancellationToken = _detailCancellation.Token;
        var pageGeneration = _pageGeneration;
        row.IsDetailLoading = true;
        row.DetailError = null;
        try
        {
            var detail = await _api.GetDeliveryHistoryDetailAsync(row.Delivery.BatchId, cancellationToken);
            if (pageGeneration == _pageGeneration && Rows.Contains(row)) row.Detail = detail;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            if (pageGeneration == _pageGeneration && Rows.Contains(row))
                row.DetailError = FailureMessage(exception);
        }
        finally
        {
            if (pageGeneration == _pageGeneration && Rows.Contains(row)) row.IsDetailLoading = false;
        }
    }

    private void ClearCurrentPage()
    {
        _pageGeneration++;
        _detailCancellation?.Cancel();
        CollapseDetails();
        Rows = [];
        TotalCount = 0;
        ListError = null;
    }

    private void CollapseDetails()
    {
        if (_expandedRow is not null) _expandedRow.IsExpanded = false;
        _expandedRow = null;
    }

    private void NotifyPaging()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(SortSummary));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasListError));
        OnPropertyChanged(nameof(ShowState));
        OnPropertyChanged(nameof(ShowStateIcon));
        OnPropertyChanged(nameof(StateIcon));
        OnPropertyChanged(nameof(StateTitle));
        OnPropertyChanged(nameof(StateMessage));
        OnPropertyChanged(nameof(StateActionText));
        OnPropertyChanged(nameof(HasStateAction));
        OnPropertyChanged(nameof(StateActionCommand));
        NotifyPaging();
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException
        ? "Cannot reach the store API."
        : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;

    private sealed record AppliedFilters(
        string? ReceiptNumber,
        Guid? SupplierId,
        DateTimeOffset? FromDate,
        DateTimeOffset? ToDate,
        DeliverySortOption Sort)
    {
        public bool IsFiltered => ReceiptNumber is not null || SupplierId.HasValue || FromDate.HasValue || ToDate.HasValue;
    }
}
