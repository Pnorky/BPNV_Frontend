using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly StoreApiClient _api;

    [ObservableProperty] private ApiReportSnapshot? _snapshot;
    [ObservableProperty] private IReadOnlyList<TopProductResponse> _topProducts = [];
    [ObservableProperty] private IReadOnlyList<ReportSaleResponse> _recentSales = [];
    [ObservableProperty] private IReadOnlyList<InventoryReportProductResponse> _inventoryItems = [];
    [ObservableProperty] private IReadOnlyList<SupplierOrderResponse> _orderSummaries = [];
    [ObservableProperty] private IReadOnlyList<EmployeePurchaseLineResponse> _employeePurchaseLines = [];
    [ObservableProperty] private IReadOnlyList<EmployeeResponse> _employees = [];
    [ObservableProperty] private EmployeeResponse? _selectedEmployee;
    [ObservableProperty] private IReadOnlyList<UserResponse> _cashiers = [];
    [ObservableProperty] private UserResponse? _selectedCashier;
    [ObservableProperty] private string _exportStatus = "";
    [ObservableProperty] private string _statusMessage = "Loading reports...";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DateTimeOffset? _fromDate;
    [ObservableProperty] private DateTimeOffset? _toDate;
    [ObservableProperty] private string _selectedCustomerType = "All sales";
    [ObservableProperty] private int _selectedReportTabIndex;

    public IReadOnlyList<string> CustomerTypeOptions { get; } = ["All sales", "Regular", "Employee"];
    public bool IsSalesReportTab => SelectedReportTabIndex == 0;
    public bool IsEmployeePurchasesTab => SelectedReportTabIndex == 1;
    public bool IsCashierRemittanceTab => SelectedReportTabIndex == 2;
    public bool IsInventoryReportTab => SelectedReportTabIndex == 3;
    public bool IsOrderReportTab => SelectedReportTabIndex == 4;
    public bool IsDateFilteredReportTab => IsSalesReportTab || IsEmployeePurchasesTab || IsCashierRemittanceTab;
    public bool HasReportFilters => IsDateFilteredReportTab;

    public string GrossSalesDisplay => $"₱{Snapshot?.Sales.Summary.GrossSales ?? 0:N2}";
    public string TodaySalesDisplay => $"₱{Snapshot?.Sales.Summary.TodaySales ?? 0:N2}";
    public int Transactions => Snapshot?.Sales.Summary.Transactions ?? 0;
    public int UnitsSold => Snapshot?.Sales.Summary.UnitsSold ?? 0;
    public int LowStockItems => Snapshot?.Inventory.Summary.LowStockItems ?? 0;
    public string InventoryValueDisplay => $"₱{Snapshot?.Inventory.Summary.InventoryValue ?? 0:N2}";
    public int DisplayUnits => Snapshot?.Inventory.Summary.DisplayUnits ?? 0;
    public int BodegaUnits => Snapshot?.Inventory.Summary.BodegaUnits ?? 0;
    public int TotalInventoryUnits => Snapshot?.Inventory.Summary.TotalInventoryUnits ?? 0;
    public int MerchandiseCount => Snapshot?.Inventory.Summary.MerchandiseCount ?? 0;
    public int ConsumableCount => Snapshot?.Inventory.Summary.ConsumableCount ?? 0;
    public int SupplyCount => Snapshot?.Inventory.Summary.SupplyCount ?? 0;
    public int SuppliersToOrder => Snapshot?.Orders.Summary.SuppliersToOrder ?? 0;
    public int ProductsToOrder => Snapshot?.Orders.Summary.ProductsToOrder ?? 0;
    public int SuggestedOrderUnits => Snapshot?.Orders.Summary.SuggestedOrderUnits ?? 0;
    public string EmployeeDeductionsDisplay => $"₱{Snapshot?.EmployeePurchases?.Summary.TotalDeductions ?? 0:N2}";
    public string EmployeeOwedDisplay => $"₱{Snapshot?.EmployeePurchases?.Summary.TotalOwed ?? 0:N2}";
    public int EmployeeTransactions => Snapshot?.EmployeePurchases?.Summary.Transactions ?? 0;
    public int EmployeesRepresented => Snapshot?.EmployeePurchases?.Summary.Employees ?? 0;

    public ReportsViewModel(StoreApiClient api)
    {
        _api = api;
        _ = RefreshAsync();
    }

    partial void OnSelectedCashierChanged(UserResponse? value) => _ = RefreshAsync();

    partial void OnSelectedReportTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSalesReportTab));
        OnPropertyChanged(nameof(IsEmployeePurchasesTab));
        OnPropertyChanged(nameof(IsCashierRemittanceTab));
        OnPropertyChanged(nameof(IsInventoryReportTab));
        OnPropertyChanged(nameof(IsOrderReportTab));
        OnPropertyChanged(nameof(IsDateFilteredReportTab));
        OnPropertyChanged(nameof(HasReportFilters));
    }

    [RelayCommand]
    private void ClearEmployeeFilter() => SelectedEmployee = null;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Loading reports...";
        try
        {
            var (fromUtc, toUtcExclusive) = StoreDateTime.GetUtcDateRange(FromDate, ToDate);
            var customerType = SelectedCustomerType switch
            {
                "Regular" => ApiCustomerType.Regular,
                "Employee" => ApiCustomerType.Employee,
                _ => (ApiCustomerType?)null
            };
            var salesTask = _api.GetSalesReportAsync(fromUtc, toUtcExclusive, customerType);
            var inventoryTask = _api.GetInventoryReportAsync();
            var ordersTask = _api.GetOrderReportAsync();
            var employeeReportTask = _api.GetEmployeePurchaseReportAsync(fromUtc, toUtcExclusive, SelectedEmployee?.Id);
            var employeesTask = _api.GetEmployeesAsync(includeInactive: true);
            var cashierReportTask = _api.GetCashierShiftReportAsync(
                DateOnly.FromDateTime(FromDate?.Date ?? StoreDateTime.StoreToday),
                DateOnly.FromDateTime((ToDate?.Date ?? FromDate?.Date ?? StoreDateTime.StoreToday).AddDays(1)),
                SelectedCashier?.Id);
            var cashiersTask = _api.GetUsersAsync(includeInactive: true);
            await Task.WhenAll(salesTask, inventoryTask, ordersTask, employeeReportTask, employeesTask);
            CashierShiftReportResponse? cashierReport = null;
            try { cashierReport = await cashierReportTask; }
            catch (Exception) { }
            IReadOnlyList<UserResponse> cashierUsers = [];
            try { cashierUsers = await cashiersTask; }
            catch (Exception) { }

            Snapshot = new ApiReportSnapshot(
                await salesTask,
                await inventoryTask,
                await ordersTask,
                await employeeReportTask,
                cashierReport);
            TopProducts = Snapshot.Sales.TopProducts;
            RecentSales = Snapshot.Sales.RecentSales;
            InventoryItems = Snapshot.Inventory.Products;
            OrderSummaries = Snapshot.Orders.Suppliers;
            EmployeePurchaseLines = Snapshot.EmployeePurchases!.Lines;
            Employees = (await employeesTask).OrderBy(employee => employee.Name).ToArray();
            Cashiers = cashierUsers.Where(user => user.Roles.Contains("Cashier", StringComparer.OrdinalIgnoreCase))
                .OrderBy(user => user.DisplayName).ToArray();
            StatusMessage = "Reports are up to date.";
            NotifySummaryChanged();
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            Snapshot = null;
            TopProducts = [];
            RecentSales = [];
            InventoryItems = [];
            OrderSummaries = [];
            EmployeePurchaseLines = [];
            ErrorMessage = exception is TaskCanceledException ? "The report request timed out." : exception.Message;
            StatusMessage = ErrorMessage;
            NotifySummaryChanged();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportPdf()
    {
        if (Snapshot is null)
        {
            ExportStatus = "Load the reports before exporting.";
            return;
        }

        var file = await SelectExportFileAsync("Export PDF report", BuildExportFileName("pdf"), "PDF report", "*.pdf");
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            ReportExportService.ExportPdf(Snapshot, stream, (ReportExportArea)SelectedReportTabIndex);
            ExportStatus = "PDF report exported successfully.";
        }
        catch (Exception exception)
        {
            ExportStatus = $"PDF export failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportExcel()
    {
        if (Snapshot is null)
        {
            ExportStatus = "Load the reports before exporting.";
            return;
        }

        var file = await SelectExportFileAsync("Export Excel report", BuildExportFileName("xlsx"), "Excel workbook", "*.xlsx");
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            ReportExportService.ExportExcel(Snapshot, stream, (ReportExportArea)SelectedReportTabIndex);
            ExportStatus = "Excel report exported successfully.";
        }
        catch (Exception exception)
        {
            ExportStatus = $"Excel export failed: {exception.Message}";
        }
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(GrossSalesDisplay));
        OnPropertyChanged(nameof(TodaySalesDisplay));
        OnPropertyChanged(nameof(Transactions));
        OnPropertyChanged(nameof(UnitsSold));
        OnPropertyChanged(nameof(LowStockItems));
        OnPropertyChanged(nameof(InventoryValueDisplay));
        OnPropertyChanged(nameof(DisplayUnits));
        OnPropertyChanged(nameof(BodegaUnits));
        OnPropertyChanged(nameof(TotalInventoryUnits));
        OnPropertyChanged(nameof(MerchandiseCount));
        OnPropertyChanged(nameof(ConsumableCount));
        OnPropertyChanged(nameof(SupplyCount));
        OnPropertyChanged(nameof(SuppliersToOrder));
        OnPropertyChanged(nameof(ProductsToOrder));
        OnPropertyChanged(nameof(SuggestedOrderUnits));
        OnPropertyChanged(nameof(EmployeeDeductionsDisplay));
        OnPropertyChanged(nameof(EmployeeOwedDisplay));
        OnPropertyChanged(nameof(EmployeeTransactions));
        OnPropertyChanged(nameof(EmployeesRepresented));
    }

    private string BuildExportFileName(string extension)
    {
        var reportName = SelectedReportTabIndex switch
        {
            1 => "employee-purchases",
            2 => "cashier-remittance",
            3 => "inventory-summary",
            4 => "order-summary",
            _ => "sales-summary"
        };
        var datePart = FromDate is null && ToDate is null
            ? "all-dates"
            : $"{FromDate?.ToString("yyyy-MM-dd") ?? "start"}-to-{ToDate?.ToString("yyyy-MM-dd") ?? "today"}";
        var filterPart = SelectedReportTabIndex switch
        {
            0 => SelectedCustomerType == "All sales" ? "all-sales" : SelectedCustomerType.ToLowerInvariant(),
            1 => SelectedEmployee?.Name is { Length: > 0 } name ? name : "all-employees",
            2 => SelectedCashier?.DisplayName is { Length: > 0 } cashier ? cashier : "all-cashiers",
            _ => "all"
        };
        var fileName = $"BPNV-{reportName}-{datePart}-{filterPart}";
        foreach (var character in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(character, '-');
        return $"{fileName}.{extension}";
    }

    private static async Task<IStorageFile?> SelectExportFileAsync(
        string title,
        string suggestedFileName,
        string fileTypeName,
        string pattern)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            return null;

        return await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = Path.GetExtension(suggestedFileName).TrimStart('.'),
            FileTypeChoices = [new FilePickerFileType(fileTypeName) { Patterns = [pattern] }]
        });
    }
}
