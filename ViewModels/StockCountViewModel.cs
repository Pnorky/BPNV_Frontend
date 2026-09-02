using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApp.ViewModels;

public sealed record StockCountLocationOption(string Label, ApiInventoryStockLocation Location)
{
    public override string ToString() => Label;
}

public partial class StockCountViewModel : ObservableObject
{
    [ObservableProperty] private ProductResponse? _selectedProduct;
    [ObservableProperty] private StockCountLocationOption? _selectedLocation;
    [ObservableProperty] private decimal _countedQuantity;
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _validationMessage = "";

    public IReadOnlyList<ProductResponse> Products { get; }
    public IReadOnlyList<StockCountLocationOption> Locations { get; } =
    [
        new("Bodega", ApiInventoryStockLocation.Bodega),
        new("Display", ApiInventoryStockLocation.Display)
    ];
    public int CurrentQuantity => SelectedProduct is null || SelectedLocation is null
        ? 0
        : SelectedLocation.Location == ApiInventoryStockLocation.Display
            ? SelectedProduct.DisplayStock
            : SelectedProduct.BodegaStock;
    public int? Variance => WholeCount(out var counted) ? counted - CurrentQuantity : null;
    public string CurrentBalanceDisplay => SelectedProduct is null || SelectedLocation is null
        ? "Select a product and location"
        : $"{CurrentQuantity:N0} {SelectedProduct.Unit} in {SelectedLocation.Label}";
    public string VarianceDisplay => Variance is not int variance
        ? "Enter a whole remaining quantity"
        : variance == 0
            ? "No adjustment needed"
            : variance > 0
                ? $"Increase by {variance:N0} {SelectedProduct?.Unit}"
                : $"Decrease by {Math.Abs(variance):N0} {SelectedProduct?.Unit}";

    public StockCountViewModel(IReadOnlyList<ProductResponse> products, ProductResponse? selectedProduct = null)
    {
        Products = products
            .Where(product => product.CanRecordStockCount)
            .OrderBy(product => product.Name)
            .ToArray();
        SelectedLocation = Locations[0];
        SelectedProduct = selectedProduct?.CanRecordStockCount == true
            ? Products.FirstOrDefault(product => product.Id == selectedProduct.Id)
            : Products.FirstOrDefault();
        CountedQuantity = CurrentQuantity;
    }

    partial void OnSelectedProductChanged(ProductResponse? value)
    {
        CountedQuantity = CurrentQuantity;
        NotifyPreview();
    }

    partial void OnSelectedLocationChanged(StockCountLocationOption? value)
    {
        CountedQuantity = CurrentQuantity;
        NotifyPreview();
    }

    partial void OnCountedQuantityChanged(decimal value) => NotifyPreview();

    public bool TryBuildRequest(out RecordStockCountRequest? request, out string error)
    {
        request = null;
        error = "";
        ValidationMessage = "";
        if (SelectedProduct is null) return Fail("Select a Supply or Consumable.", out error);
        if (SelectedLocation is null) return Fail("Select a stock location.", out error);
        if (!WholeCount(out var counted)) return Fail("Remaining quantity must be a whole number from zero to 2,147,483,647.", out error);
        if (counted == CurrentQuantity) return Fail("The remaining quantity already matches the current balance.", out error);
        if (Notes.Trim().Length > 500) return Fail("Notes must not exceed 500 characters.", out error);

        request = new RecordStockCountRequest(
            SelectedProduct.Id,
            SelectedLocation.Location,
            counted,
            SelectedProduct.Version,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());
        return true;
    }

    private bool WholeCount(out int value)
    {
        if (CountedQuantity != decimal.Truncate(CountedQuantity) || CountedQuantity is < 0 or > int.MaxValue)
        {
            value = 0;
            return false;
        }
        value = decimal.ToInt32(CountedQuantity);
        return true;
    }

    private void NotifyPreview()
    {
        OnPropertyChanged(nameof(CurrentQuantity));
        OnPropertyChanged(nameof(CurrentBalanceDisplay));
        OnPropertyChanged(nameof(Variance));
        OnPropertyChanged(nameof(VarianceDisplay));
    }

    private bool Fail(string message, out string error)
    {
        error = message;
        ValidationMessage = message;
        return false;
    }
}
