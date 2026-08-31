using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Services;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class ProductCatalogViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private IReadOnlyList<ProductResponse> _products = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private IReadOnlyList<ProductResponse> _filteredProducts = [];
    [ObservableProperty] private string _statusMessage = "Loading database products...";
    [ObservableProperty] private bool _isBusy;

    public ProductCatalogViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
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
            var page = await _api.GetProductsAsync(includeInactive: true, pageSize: 200);
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

    [RelayCommand]
    private async Task EditAsync(ProductResponse? product)
    {
        if (product is null || IsBusy) return;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            ShowError("Product not updated", "The edit dialog could not be opened.");
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading active suppliers...";
        try
        {
            var suppliers = await _api.GetSuppliersAsync();
            IsBusy = false;
            var dialog = new ProductEditDialog(new ProductEditViewModel(product, suppliers));
            var request = await dialog.ShowDialog<UpdateProductRequest?>(owner);
            if (request is null) return;

            IsBusy = true;
            StatusMessage = $"Updating {product.Name}...";
            var updated = await _api.UpdateProductAsync(product.Id, request);
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"{updated.Name} was updated.";
            _notifications.ShowSuccess("Product updated", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Product not updated", FailureMessage(exception));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync(ProductResponse? product)
    {
        if (product is null || !product.IsActive || IsBusy) return;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner }) return;
        var dialog = new ConfirmDialog();
        dialog.SetConfirmation(
            "Deactivate product",
            $"Deactivate {product.Name}? It will be removed from active sales and receiving choices, but its stock and history will be preserved.",
            "Deactivate");
        await dialog.ShowDialog(owner);
        if (!dialog.Confirmed) return;

        await ChangeActivityAsync(product, activate: false);
    }

    [RelayCommand]
    private Task ReactivateAsync(ProductResponse? product) =>
        product is null || product.IsActive ? Task.CompletedTask : ChangeActivityAsync(product, activate: true);

    private async Task ChangeActivityAsync(ProductResponse product, bool activate)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            StatusMessage = $"{(activate ? "Reactivating" : "Deactivating")} {product.Name}...";
            if (activate)
                await _api.ReactivateProductAsync(product.Id);
            else
                await _api.DeactivateProductAsync(product.Id);
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"{product.Name} was {(activate ? "reactivated" : "deactivated")}.";
            _notifications.ShowSuccess(activate ? "Product reactivated" : "Product deactivated", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError(activate ? "Product not reactivated" : "Product not deactivated", FailureMessage(exception));
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

    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }

    private static bool IsApiFailure(Exception exception) =>
        exception is ApiClientException or HttpRequestException or TaskCanceledException;

    private static string FailureMessage(Exception exception) => exception switch
    {
        HttpRequestException => "Cannot reach the store API.",
        TaskCanceledException => "The store API did not respond in time.",
        _ => exception.Message
    };
}
