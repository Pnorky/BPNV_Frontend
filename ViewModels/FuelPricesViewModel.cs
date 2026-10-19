using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class FuelPricesViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;

    [ObservableProperty] private IReadOnlyList<FuelProductResponse> _fuels = [];
    [ObservableProperty] private IReadOnlyList<FuelPriceResponse> _priceHistory = [];
    [ObservableProperty] private FuelProductResponse? _selectedFuel;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _includeInactive;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _statusMessage = "Loading fuel prices...";
    [ObservableProperty] private string _fuelName = "";
    [ObservableProperty] private decimal _fuelSortOrder = 1;
    [ObservableProperty] private decimal _initialPrice;
    [ObservableProperty] private decimal _newPrice;

    public string FuelSaveText => SelectedFuel is null ? "Add fuel type" : "Save fuel type";
    public bool IsNewFuel => SelectedFuel is null;
    public bool HasSelectedFuel => SelectedFuel is not null;
    public bool HasNoPriceHistory => PriceHistory.Count == 0;
    public string PriceHistoryEmptyMessage => SelectedFuel is null
        ? "Select a fuel type to view its price history."
        : "No price history is available for this fuel type.";
    public bool IsFiltered => !string.IsNullOrWhiteSpace(SearchText) || IncludeInactive;
    public string PriceDifferenceDisplay => SelectedFuel is null || NewPrice <= 0
        ? "Enter a new price."
        : $"{(NewPrice - SelectedFuel.CurrentPrice >= 0 ? "+" : "-")}₱{Math.Abs(NewPrice - SelectedFuel.CurrentPrice):N2}/L";

    public FuelPricesViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        _ = LoadAsync();
    }

    partial void OnSelectedFuelChanged(FuelProductResponse? value)
    {
        NotifyEditor();
        OnPropertyChanged(nameof(PriceHistoryEmptyMessage));
        if (value is null) return;
        FuelName = value.Name;
        FuelSortOrder = value.SortOrder;
        InitialPrice = value.CurrentPrice;
        NewPrice = value.CurrentPrice;
        _ = LoadHistoryAsync();
    }

    partial void OnNewPriceChanged(decimal value) => OnPropertyChanged(nameof(PriceDifferenceDisplay));
    partial void OnPriceHistoryChanged(IReadOnlyList<FuelPriceResponse> value) =>
        OnPropertyChanged(nameof(HasNoPriceHistory));
    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(IsFiltered));
    partial void OnIncludeInactiveChanged(bool value) { OnPropertyChanged(nameof(IsFiltered)); _ = LoadAsync(); }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await LoadFuelsCoreAsync();
            StatusMessage = $"Loaded {Fuels.Count} fuel type{(Fuels.Count == 1 ? "" : "s")}.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ErrorMessage = FailureMessage(exception);
            ShowError("Fuel prices could not be loaded", ErrorMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadFuelsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await LoadFuelsCoreAsync();
            StatusMessage = $"Loaded {Fuels.Count} fuel type{(Fuels.Count == 1 ? "" : "s")}.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ErrorMessage = FailureMessage(exception);
            ShowError("Fuel prices could not be loaded", ErrorMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = "";
        if (IncludeInactive) IncludeInactive = false;
        else _ = LoadFuelsAsync();
    }

    [RelayCommand]
    private void NewFuel()
    {
        SelectedFuel = null;
        FuelName = "";
        FuelSortOrder = Fuels.Count == 0 ? 1 : Fuels.Max(fuel => fuel.SortOrder) + 1;
        InitialPrice = 0;
        NewPrice = 0;
        PriceHistory = [];
        NotifyEditor();
    }

    [RelayCommand]
    private async Task SaveFuelAsync()
    {
        if (IsBusy || !ValidateFuel()) return;
        IsBusy = true;
        try
        {
            if (SelectedFuel is null)
                await _api.CreateFuelAsync(new(FuelName.Trim(), (int)FuelSortOrder, InitialPrice));
            else
                await _api.UpdateFuelAsync(SelectedFuel.Id, new(FuelName.Trim(), (int)FuelSortOrder));
            var action = SelectedFuel is null ? "added" : "updated";
            IsBusy = false;
            NewFuel();
            await LoadFuelsAsync();
            StatusMessage = $"Fuel type {action}.";
            _notifications.ShowSuccess("Fuel saved", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Fuel not saved", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ChangePriceAsync()
    {
        if (SelectedFuel is null || IsBusy) return;
        if (!ValidPrice(NewPrice)) { ShowError("Price not changed", "Enter a price greater than zero with no more than four decimal places."); return; }
        if (NewPrice == SelectedFuel.CurrentPrice) { ShowError("Price not changed", "Enter a price different from the current price."); return; }
        IsBusy = true;
        try
        {
            var updated = await _api.ChangeFuelPriceAsync(SelectedFuel.Id, new(NewPrice));
            SelectedFuel = updated;
            IsBusy = false;
            await LoadFuelsAsync();
            await LoadHistoryAsync();
            StatusMessage = $"{updated.Name} is now {updated.CurrentPriceDisplay}.";
            _notifications.ShowSuccess("Fuel price changed", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Price not changed", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task DeactivateFuelAsync(FuelProductResponse? fuel) =>
        fuel is null || IsBusy ? Task.CompletedTask : SetFuelActiveAsync(fuel, false);

    [RelayCommand]
    private Task ReactivateFuelAsync(FuelProductResponse? fuel) =>
        fuel is null || IsBusy ? Task.CompletedTask : SetFuelActiveAsync(fuel, true);

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (SelectedFuel is null) { PriceHistory = []; return; }
        try { PriceHistory = (await _api.GetFuelPriceHistoryAsync(SelectedFuel.Id)).Items; }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Price history could not be loaded", FailureMessage(exception)); }
    }

    private async Task LoadFuelsCoreAsync()
    {
        var selectedId = SelectedFuel?.Id;
        Fuels = (await _api.GetFuelsAsync(SearchText, IncludeInactive)).Items;
        SelectedFuel = selectedId.HasValue ? Fuels.FirstOrDefault(fuel => fuel.Id == selectedId) : null;
    }

    private async Task SetFuelActiveAsync(FuelProductResponse fuel, bool active)
    {
        IsBusy = true;
        try
        {
            if (active) await _api.ReactivateFuelAsync(fuel.Id); else await _api.DeactivateFuelAsync(fuel.Id);
            if (SelectedFuel?.Id == fuel.Id) NewFuel();
            IsBusy = false;
            await LoadFuelsAsync();
            StatusMessage = $"{fuel.Name} {(active ? "reactivated" : "deactivated")}.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError($"Fuel not {(active ? "reactivated" : "deactivated")}", FailureMessage(exception));
        }
        finally { IsBusy = false; }
    }

    private bool ValidateFuel()
    {
        if (string.IsNullOrWhiteSpace(FuelName)) { ShowError("Fuel not saved", "Enter a fuel name such as XCS, Premium, or Diesel."); return false; }
        if (!ValidSortOrder(FuelSortOrder)) { ShowError("Fuel not saved", "Sort order must be a whole number from 0 to 10000."); return false; }
        if (SelectedFuel is null && !ValidPrice(InitialPrice)) { ShowError("Fuel not saved", "Enter an initial price greater than zero with no more than four decimal places."); return false; }
        return true;
    }

    private void NotifyEditor()
    {
        OnPropertyChanged(nameof(FuelSaveText));
        OnPropertyChanged(nameof(IsNewFuel));
        OnPropertyChanged(nameof(HasSelectedFuel));
        OnPropertyChanged(nameof(PriceDifferenceDisplay));
    }

    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }

    private static bool ValidSortOrder(decimal value) => value is >= 0 and <= 10000 && decimal.Truncate(value) == value;
    private static bool ValidPrice(decimal value) => value > 0 && value <= 99999999999999.9999m && decimal.Round(value, 4) == value;
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException
        ? "Cannot reach the store API."
        : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;
}
