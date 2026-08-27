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
    [ObservableProperty] private string _exportStatus = "";
    [ObservableProperty] private string _statusMessage = "Loading reports...";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DateTimeOffset? _fromDate;
    [ObservableProperty] private DateTimeOffset? _toDate;
    [ObservableProperty] private string _selectedCustomerType = "All sales";

    public IReadOnlyList<string> CustomerTypeOptions { get; } = ["All sales", "Regular", "Employee"];

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

    public ReportsViewModel(StoreApiClient api)
    {
        _api = api;
        _ = RefreshAsync();
    }

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
            await Task.WhenAll(salesTask, inventoryTask, ordersTask);

            Snapshot = new ApiReportSnapshot(
                await salesTask,
                await inventoryTask,
                await ordersTask);
            TopProducts = Snapshot.Sales.TopProducts;
            RecentSales = Snapshot.Sales.RecentSales;
            InventoryItems = Snapshot.Inventory.Products;
            OrderSummaries = Snapshot.Orders.Suppliers;
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

        var file = await SelectExportFileAsync("Export PDF report", "BPNV-store-report.pdf", "PDF report", "*.pdf");
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            ReportExportService.ExportPdf(Snapshot, stream);
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

        var file = await SelectExportFileAsync("Export Excel report", "BPNV-store-report.xlsx", "Excel workbook", "*.xlsx");
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            ReportExportService.ExportExcel(Snapshot, stream);
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
