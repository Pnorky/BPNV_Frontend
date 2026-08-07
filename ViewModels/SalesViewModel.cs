using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class SalesViewModel : ObservableObject
{
    private readonly StoreState _store;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedCustomerType = "Regular";

    [ObservableProperty]
    private IReadOnlyList<ProductItem> _filteredProducts;

    [ObservableProperty]
    private string _statusMessage = "Select products from the display to begin.";

    public IReadOnlyList<string> CustomerTypes { get; } = ["Regular", "Employee"];
    public ObservableCollection<CartLine> Cart { get; } = [];
    public string CartSummary => Cart.Count == 0 ? "No items added" : $"{Cart.Sum(line => line.Quantity)} items";
    public string TotalDisplay => $"₱{Cart.Sum(line => line.Amount):N2}";

    public SalesViewModel(StoreState store)
    {
        _store = store;
        _filteredProducts = store.Products.Where(product => product.IsActive && product.IsSellable).ToList();
        Cart.CollectionChanged += OnCartChanged;
        _store.StateChanged += (_, _) => ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCustomerTypeChanged(string value)
    {
        foreach (var line in Cart)
            line.UnitPrice = PriceFor(line.Product);
        NotifyCartTotals();
    }

    [RelayCommand]
    private void AddProduct(ProductItem? product)
    {
        if (product is null) return;
        if (PriceFor(product) <= 0)
        {
            StatusMessage = $"Set a selling price for {product.Name} before adding it to a sale.";
            return;
        }
        if (product.ShelfStock == 0)
        {
            StatusMessage = $"{product.Name} needs to be moved from the bodega to the display first.";
            return;
        }

        var existing = Cart.FirstOrDefault(line => ReferenceEquals(line.Product, product));
        if (existing is null)
        {
            var line = new CartLine { Product = product, UnitPrice = PriceFor(product) };
            line.PropertyChanged += (_, _) => NotifyCartTotals();
            Cart.Add(line);
        }
        else if (existing.Quantity < product.ShelfStock)
        {
            existing.Quantity++;
        }
        else
        {
            StatusMessage = $"All display stock for {product.Name} is already in this sale.";
            return;
        }

        StatusMessage = $"Added {product.Name}.";
        NotifyCartTotals();
    }

    [RelayCommand]
    private void DecreaseQuantity(CartLine? line)
    {
        if (line is null) return;
        if (line.Quantity == 1) Cart.Remove(line);
        else line.Quantity--;
        NotifyCartTotals();
    }

    [RelayCommand]
    private void IncreaseQuantity(CartLine? line)
    {
        if (line is null) return;
        if (line.Quantity < line.Product.ShelfStock) line.Quantity++;
        else StatusMessage = $"Only {line.Product.ShelfStock} {line.Product.Unit} are available on display.";
        NotifyCartTotals();
    }

    [RelayCommand]
    private void RemoveLine(CartLine? line)
    {
        if (line is not null) Cart.Remove(line);
    }

    [RelayCommand]
    private void ClearSale()
    {
        Cart.Clear();
        StatusMessage = "Sale cleared.";
    }

    [RelayCommand]
    private void CompleteSale()
    {
        if (!_store.RecordSale(SelectedCustomerType, Cart, out var message))
        {
            StatusMessage = message;
            return;
        }

        Cart.Clear();
        ApplyFilter();
        StatusMessage = message;
    }

    private decimal PriceFor(ProductItem product) => SelectedCustomerType == "Employee" && product.EmployeePrice > 0
        ? product.EmployeePrice
        : product.RegularPrice;

    private void ApplyFilter() => FilteredProducts = string.IsNullOrWhiteSpace(SearchText)
        ? _store.Products.Where(product => product.IsActive && product.IsSellable).ToList()
        : _store.Products.Where(product => product.IsActive && product.IsSellable && (
            product.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
            product.Sku.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
            product.SupplierName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
            product.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase))).ToList();

    private void OnCartChanged(object? sender, NotifyCollectionChangedEventArgs e) => NotifyCartTotals();

    private void NotifyCartTotals()
    {
        OnPropertyChanged(nameof(CartSummary));
        OnPropertyChanged(nameof(TotalDisplay));
    }
}
