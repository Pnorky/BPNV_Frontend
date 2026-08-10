using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class ProductCatalogViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private IReadOnlyList<ProductResponse> _products = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private IReadOnlyList<ProductResponse> _filteredProducts = [];
    [ObservableProperty] private string _statusMessage = "Loading database products...";
    [ObservableProperty] private bool _isBusy;

    public ProductCatalogViewModel(StoreApiClient api)
    {
        _api = api;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        StatusMessage = "Loading database products...";
        IsBusy = true;
        try
        {
            var page = await _api.GetProductsAsync(pageSize: 200);
            _products = page.Items;
            ApplyFilter();
            StatusMessage = $"Showing {_products.Count} of {page.TotalCount} database products.";
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = exception is HttpRequestException ? "Cannot reach the store API." : exception.Message;
            _products = [];
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
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
                    product.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    product.Units.Any(unit => unit.Barcode?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)))
            .OrderBy(product => product.Name)
            .ToList();
    }
}
