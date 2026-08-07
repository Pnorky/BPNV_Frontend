using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public sealed record ProductSalesSummary(string Product, int Quantity, decimal Sales)
{
    public string SalesDisplay => $"₱{Sales:N2}";
}

public partial class ReportsViewModel : ObservableObject
{
    private readonly StoreState _store;

    [ObservableProperty]
    private IReadOnlyList<ProductSalesSummary> _topProducts = [];

    [ObservableProperty]
    private IReadOnlyList<SaleRecord> _recentSales = [];

    [ObservableProperty]
    private IReadOnlyList<ProductItem> _inventoryItems = [];

    [ObservableProperty]
    private IReadOnlyList<SupplierOrderSummary> _orderSummaries = [];

    [ObservableProperty]
    private string _exportStatus = "";

    public string GrossSalesDisplay => $"₱{_store.Sales.Sum(sale => sale.Total):N2}";
    public string TodaySalesDisplay => $"₱{_store.Sales.Where(sale => sale.SoldAt.Date == DateTime.Today).Sum(sale => sale.Total):N2}";
    public int Transactions => _store.Sales.Count;
    public int UnitsSold => _store.Sales.Sum(sale => sale.ItemCount);
    public int LowStockItems => _store.Products.Count(product => product.IsLowStock);
    public string InventoryValueDisplay => $"₱{_store.Products.Sum(product => product.TotalStock * product.RegularPrice):N2}";
    public int DisplayUnits => _store.Products.Sum(product => product.ShelfStock);
    public int BodegaUnits => _store.Products.Sum(product => product.BodegaStock);
    public int TotalInventoryUnits => DisplayUnits + BodegaUnits;
    public int MerchandiseCount => _store.Products.Count(product => product.ItemType == InventoryItemType.Merchandise);
    public int ConsumableCount => _store.Products.Count(product => product.ItemType == InventoryItemType.Consumable);
    public int SupplyCount => _store.Products.Count(product => product.ItemType == InventoryItemType.Supply);
    public int SuppliersToOrder => OrderSummaries.Count;
    public int ProductsToOrder => OrderSummaries.Sum(summary => summary.ProductCount);
    public int SuggestedOrderUnits => OrderSummaries.Sum(summary => summary.TotalOrderQuantity);

    public ReportsViewModel(StoreState store)
    {
        _store = store;
        _store.StateChanged += (_, _) => Refresh();
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        TopProducts = _store.Sales
            .SelectMany(sale => sale.Lines)
            .GroupBy(line => line.ProductName)
            .Select(group => new ProductSalesSummary(group.Key, group.Sum(line => line.Quantity), group.Sum(line => line.Amount)))
            .OrderByDescending(item => item.Sales)
            .Take(5)
            .ToList();
        RecentSales = _store.Sales.OrderByDescending(sale => sale.SoldAt).Take(8).ToList();
        InventoryItems = _store.Products.OrderBy(product => product.SupplierName).ThenBy(product => product.Name).ToList();
        OrderSummaries = _store.Products
            .Where(product => product.SuggestedOrderQuantity > 0)
            .GroupBy(product => product.SupplierName)
            .OrderBy(group => group.Key)
            .Select(group => new SupplierOrderSummary(
                group.Key,
                group.OrderBy(product => product.Name).Select(product => new ReorderProductSummary(product)).ToList()))
            .ToList();
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

    [RelayCommand]
    private async Task ExportPdf()
    {
        var file = await SelectExportFileAsync("Export PDF report", "BPNV-store-report.pdf", "PDF report", "*.pdf");
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            ReportExportService.ExportPdf(_store, stream);
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
        var file = await SelectExportFileAsync("Export Excel report", "BPNV-store-report.xlsx", "Excel workbook", "*.xlsx");
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            ReportExportService.ExportExcel(_store, stream);
            ExportStatus = "Excel report exported successfully.";
        }
        catch (Exception exception)
        {
            ExportStatus = $"Excel export failed: {exception.Message}";
        }
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
