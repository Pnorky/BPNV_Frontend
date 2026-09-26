using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class CategoriesViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    [ObservableProperty] private ApiManagedCategoryKind _selectedKind = ApiManagedCategoryKind.Product;
    [ObservableProperty] private IReadOnlyList<ManagedCategoryResponse> _categories = [];
    [ObservableProperty] private ManagedCategoryResponse? _selectedCategory;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _sortOrder;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Loading categories...";
    [ObservableProperty] private string? _errorMessage;

    public IReadOnlyList<ApiManagedCategoryKind> CategoryKinds { get; } = Enum.GetValues<ApiManagedCategoryKind>();
    public string EditorTitle => SelectedCategory is null ? "Add category" : "Edit category";
    public string SaveText => SelectedCategory is null ? "Add category" : "Save changes";
    public bool CanEditName => SelectedCategory?.IsSystem != true;

    public CategoriesViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api; _notifications = notifications; _ = LoadAsync();
    }

    partial void OnSelectedKindChanged(ApiManagedCategoryKind value) { ClearEditor(); _ = LoadAsync(); }
    partial void OnSelectedCategoryChanged(ManagedCategoryResponse? value)
    {
        if (value is null) return;
        Name = value.Name; SortOrder = value.SortOrder;
        OnPropertyChanged(nameof(EditorTitle)); OnPropertyChanged(nameof(SaveText)); OnPropertyChanged(nameof(CanEditName));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return; IsBusy = true; ErrorMessage = null;
        try
        {
            Categories = await _api.GetManagedCategoriesAsync(SelectedKind, includeInactive: true);
            if (SelectedCategory is null)
                SortOrder = Categories.Count == 0 ? 1 : Categories.Max(category => category.SortOrder) + 1;
            StatusMessage = $"Loaded {Categories.Count} {KindLabel(SelectedKind)} categor{(Categories.Count == 1 ? "y" : "ies")}.";
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ErrorMessage = FailureMessage(exception); ShowError("Categories could not be loaded", ErrorMessage); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void NewCategory() => ClearEditor();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Name)) { ShowError("Category not saved", "Enter a category name."); return; }
        if (SortOrder is < 0 or > 10000 || decimal.Truncate(SortOrder) != SortOrder) { ShowError("Category not saved", "Sort order must be a whole number from 0 to 10000."); return; }
        IsBusy = true;
        try
        {
            if (SelectedCategory is null)
                await _api.CreateManagedCategoryAsync(new(SelectedKind, Name.Trim(), (int)SortOrder));
            else
                await _api.UpdateManagedCategoryAsync(SelectedCategory.Id, new(Name.Trim(), (int)SortOrder));
            var action = SelectedCategory is null ? "added" : "updated";
            ClearEditor(); IsBusy = false; await LoadAsync();
            StatusMessage = $"Category {action}."; _notifications.ShowSuccess("Category saved", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Category not saved", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeactivateAsync(ManagedCategoryResponse? category)
    {
        if (category is null || category.IsSystem || IsBusy) return; IsBusy = true;
        try { await _api.DeactivateManagedCategoryAsync(category.Id); IsBusy = false; await LoadAsync(); StatusMessage = $"{category.Name} deactivated."; }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Category not deactivated", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ReactivateAsync(ManagedCategoryResponse? category)
    {
        if (category is null || IsBusy) return; IsBusy = true;
        try { await _api.ReactivateManagedCategoryAsync(category.Id); IsBusy = false; await LoadAsync(); StatusMessage = $"{category.Name} reactivated."; }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Category not reactivated", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    private void ClearEditor() { SelectedCategory = null; Name = ""; SortOrder = Categories.Count == 0 ? 1 : Categories.Max(category => category.SortOrder) + 1; OnPropertyChanged(nameof(EditorTitle)); OnPropertyChanged(nameof(SaveText)); OnPropertyChanged(nameof(CanEditName)); }
    private void ShowError(string title, string message) { StatusMessage = message; _notifications.ShowError(title, message); }
    private static string KindLabel(ApiManagedCategoryKind kind) => kind == ApiManagedCategoryKind.Product ? "product" : "sales report";
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException ? "Cannot reach the store API." : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;
}
