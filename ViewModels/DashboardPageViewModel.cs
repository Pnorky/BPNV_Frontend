using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class DashboardPageViewModel : ObservableObject
{
    private readonly StoreState _store;

    [ObservableProperty]
    private IReadOnlyList<ProductItem> _attentionItems = [];

    [ObservableProperty]
    private IReadOnlyList<SaleRecord> _recentSales = [];

    public string TodaySalesDisplay => $"₱{_store.Sales.Where(sale => sale.SoldAt.Date == DateTime.Today).Sum(sale => sale.Total):N2}";
    public int TodayTransactions => _store.Sales.Count(sale => sale.SoldAt.Date == DateTime.Today);
    public int ShelfUnits => _store.Products.Sum(product => product.ShelfStock);
    public int BodegaUnits => _store.Products.Sum(product => product.BodegaStock);
    public int AttentionCount => AttentionItems.Count;

    public DashboardPageViewModel(StoreState store)
    {
        _store = store;
        _store.StateChanged += (_, _) => Refresh();
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        AttentionItems = _store.Products
            .Where(product => product.ShelfStock == 0 || product.IsLowStock)
            .OrderBy(product => product.TotalStock)
            .ToList();
        RecentSales = _store.Sales.OrderByDescending(sale => sale.SoldAt).Take(5).ToList();
        OnPropertyChanged(nameof(TodaySalesDisplay));
        OnPropertyChanged(nameof(TodayTransactions));
        OnPropertyChanged(nameof(ShelfUnits));
        OnPropertyChanged(nameof(BodegaUnits));
        OnPropertyChanged(nameof(AttentionCount));
    }
}
