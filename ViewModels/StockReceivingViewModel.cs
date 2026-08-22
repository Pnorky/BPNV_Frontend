using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class StockReceivingViewModel(StoreApiClient api, INotificationService notifications) : ObservableObject
{
    [ObservableProperty] private string _scannerText = "";
    [ObservableProperty] private PosProductResponse? _selectedProduct;
    [ObservableProperty] private ProductUnitResponse? _selectedUnit;
    [ObservableProperty] private ProductResponse? _selectedCatalogProduct;
    [ObservableProperty] private decimal _count = 1;
    [ObservableProperty] private string _reference = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _statusMessage = "Scan a piece or package barcode and press Enter.";
    [ObservableProperty] private bool _isBusy;

    public event EventHandler? ScannerFocusRequested;
    public string SelectedName => SelectedProduct?.Name ?? "No unit selected";
    public string UnitDetails => SelectedUnit is null || SelectedProduct is null
        ? "The scanned unit conversion and balances will appear here."
        : $"{SelectedUnit.Label}: {SelectedUnit.PiecesPerUnit} base piece{(SelectedUnit.PiecesPerUnit == 1 ? "" : "s")} · Display {SelectedCatalogProduct?.DisplayStock ?? SelectedProduct.DisplayStock} · Bodega {SelectedCatalogProduct?.BodegaStock.ToString() ?? "loading"}.";
    public string ConversionPreview => SelectedUnit is null || !WholeNumber(Count) || Count <= 0 || Count > int.MaxValue
        ? ""
        : $"Will receive {(long)Count * SelectedUnit.PiecesPerUnit:N0} base pieces into bodega.";

    partial void OnSelectedProductChanged(PosProductResponse? value) => NotifySelection();
    partial void OnSelectedUnitChanged(ProductUnitResponse? value) => NotifySelection();
    partial void OnSelectedCatalogProductChanged(ProductResponse? value) => NotifySelection();
    partial void OnCountChanged(decimal value) => OnPropertyChanged(nameof(ConversionPreview));

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
            var product = await api.GetProductForReceivingByBarcodeAsync(barcode);
            SelectedProduct = product;
            SelectedUnit = product.SelectedUnit;
            var catalog = await api.GetProductsAsync(search: barcode, pageSize: 50);
            SelectedCatalogProduct = catalog.Items.FirstOrDefault(item => item.Id == product.Id);
            StatusMessage = product.SelectedUnit is null
                ? "The API did not identify the scanned unit."
                : $"Selected {product.Name}, {product.SelectedUnit.Label}.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            SelectedProduct = null;
            SelectedUnit = null;
            SelectedCatalogProduct = null;
            ShowError("Barcode lookup failed", FailureMessage(exception));
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
            ShowError("Stock not received", "Scan a product unit before receiving stock.");
            RequestScannerFocus();
            return;
        }
        if (!WholeNumber(Count) || Count <= 0 || Count > int.MaxValue)
        {
            ShowError("Stock not received", "Count must be a whole number greater than zero.");
            return;
        }

        StatusMessage = "Receiving stock into bodega...";
        IsBusy = true;
        try
        {
            var result = await api.ReceiveStockAsync(new ReceiveStockRequest(
                SelectedProduct.Id, SelectedUnit.Id, (int)Count,
                NullIfWhiteSpace(Reference), NullIfWhiteSpace(Notes)));
            StatusMessage = $"Received {result.Count} {result.UnitLabel} = {result.BasePieceQuantity} base pieces. Bodega balance: {result.BodegaStock}; display: {result.DisplayStock}.";
            notifications.ShowSuccess("Stock received", StatusMessage);
            SelectedProduct = null;
            SelectedUnit = null;
            SelectedCatalogProduct = null;
            Count = 1;
            Reference = "";
            Notes = "";
            ScannerText = "";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Stock not received", FailureMessage(exception));
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
    }

    private void RequestScannerFocus() => ScannerFocusRequested?.Invoke(this, EventArgs.Empty);
    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        notifications.ShowError(title, message);
    }
    private static bool WholeNumber(decimal value) => value == decimal.Truncate(value);
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException
        ? "Cannot reach the store API."
        : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;
}
