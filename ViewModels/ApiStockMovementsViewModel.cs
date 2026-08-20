using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class ApiStockMovementsViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private IReadOnlyList<ProductResponse> _allProducts = [];
    [ObservableProperty] private IReadOnlyList<ProductResponse> _products = [];
    [ObservableProperty] private ProductResponse? _selectedProduct;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private string _reference = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _statusMessage = "Loading products...";
    [ObservableProperty] private bool _isBusy;

    public ApiStockMovementsViewModel(StoreApiClient api)
    {
        _api = api;
        _ = LoadAsync();
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
            StatusMessage = exception.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TransferAsync()
    {
        if (SelectedProduct is null || Quantity <= 0) { StatusMessage = "Select a product and enter a positive quantity."; return; }
        if (Quantity > SelectedProduct.BodegaStock) { StatusMessage = "Transfer quantity cannot exceed Bodega stock."; return; }
        IsBusy = true;
        try
        {
            await _api.TransferToDisplayAsync(new TransferStockRequest(SelectedProduct.Id, Quantity, NullIfWhiteSpace(Reference), NullIfWhiteSpace(Notes)));
            IsBusy = false;
            await LoadAsync();
            StatusMessage = "Stock moved from Bodega to Display.";
            Quantity = 1; Reference = ""; Notes = "";
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException) { StatusMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
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
}
