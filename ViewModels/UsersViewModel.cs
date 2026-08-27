using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public enum ApiUserRole
{
    Admin,
    Cashier,
    Inventory
}

public partial class UserEditor : ObservableObject
{
    [ObservableProperty] private Guid _id;
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private ApiUserRole _role = ApiUserRole.Cashier;
    [ObservableProperty] private bool _isActive = true;

    public bool IsEditing => Id != Guid.Empty;
    public string Title => IsEditing ? "Edit user" : "Create user";
    public string Description => IsEditing
        ? "Update account details, access, or assign a new password."
        : "Set up credentials and store access for a staff member.";
    public string ActionText => IsEditing ? "Save changes" : "Create user";

    partial void OnIdChanged(Guid value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(ActionText));
    }

    public void Load(UserResponse user)
    {
        Id = user.Id;
        Username = user.Username;
        DisplayName = user.DisplayName;
        IsActive = user.IsActive;
        Role = Enum.TryParse<ApiUserRole>(user.Roles.FirstOrDefault(), true, out var role)
            ? role
            : ApiUserRole.Cashier;
        Password = "";
    }

    public void Clear()
    {
        Id = Guid.Empty;
        Username = "";
        DisplayName = "";
        Password = "";
        Role = ApiUserRole.Cashier;
        IsActive = true;
    }
}

public partial class UsersViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private IReadOnlyList<UserResponse> _users = [];

    public UsersViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        _ = LoadAsync();
    }

    public UserEditor Editor { get; } = new();
    public IReadOnlyList<ApiUserRole> Roles { get; } = Enum.GetValues<ApiUserRole>();
    public IReadOnlyList<string> StatusFilters { get; } = ["All users", "Active", "Inactive"];

    [ObservableProperty] private IReadOnlyList<UserResponse> _filteredUsers = [];
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedStatusFilter = "All users";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _statusMessage = "Loading user accounts...";

    public int TotalUserCount => _users.Count;
    public int ActiveUserCount => _users.Count(user => user.IsActive);
    public int InactiveUserCount => _users.Count(user => !user.IsActive);
    public int AdminCount => _users.Count(user => user.IsActive && user.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase));
    public bool IsFiltered => SearchText.Trim().Length > 0 || SelectedStatusFilter != "All users";

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedStatusFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Loading user accounts...";
        try
        {
            _users = await _api.GetUsersAsync();
            ApplyFilter();
            NotifyCounts();
            StatusMessage = $"Loaded {_users.Count} user account{(_users.Count == 1 ? "" : "s")} from the database.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            _users = [];
            ApplyFilter();
            NotifyCounts();
            ErrorMessage = FailureMessage(exception);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewUser() => Editor.Clear();

    [RelayCommand]
    private void EditUser(UserResponse? user)
    {
        if (user is not null) Editor.Load(user);
    }

    [RelayCommand]
    private void CancelEdit() => Editor.Clear();

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = "";
        SelectedStatusFilter = "All users";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Editor.Username) || string.IsNullOrWhiteSpace(Editor.DisplayName))
        {
            ShowError("User not saved", "Username and display name are required.");
            return;
        }
        if ((!Editor.IsEditing || Editor.Password.Length > 0) && Editor.Password.Length < 8)
        {
            ShowError("User not saved", "Password must contain at least 8 characters.");
            return;
        }

        IsBusy = true;
        try
        {
            var roles = new[] { Editor.Role.ToString() };
            var action = Editor.IsEditing ? "updated" : "created";
            var displayName = Editor.DisplayName.Trim();
            if (Editor.IsEditing)
            {
                await _api.UpdateUserAsync(Editor.Id, new UpdateUserRequest(
                    Editor.Username.Trim(), displayName, Editor.IsActive,
                    string.IsNullOrWhiteSpace(Editor.Password) ? null : Editor.Password, roles));
            }
            else
            {
                await _api.CreateUserAsync(new CreateUserRequest(
                    Editor.Username.Trim(), displayName, Editor.Password, roles));
            }

            Editor.Clear();
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"{displayName} was {action}.";
            _notifications.ShowSuccess($"User {action}", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("User not saved", FailureMessage(exception));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DeactivateAsync(UserResponse? user)
    {
        if (user is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.DeactivateUserAsync(user.Id);
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"{user.DisplayName} was deactivated.";
            _notifications.ShowSuccess("User deactivated", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("User not deactivated", FailureMessage(exception));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ReactivateAsync(UserResponse? user)
    {
        if (user is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.UpdateUserAsync(user.Id, new UpdateUserRequest(
                user.Username, user.DisplayName, true, null, user.Roles));
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"{user.DisplayName} was reactivated.";
            _notifications.ShowSuccess("User reactivated", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("User not reactivated", FailureMessage(exception));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        FilteredUsers = _users
            .Where(user => SelectedStatusFilter switch
            {
                "Active" => user.IsActive,
                "Inactive" => !user.IsActive,
                _ => true
            })
            .Where(user => search.Length == 0 ||
                user.Username.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                user.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                user.Roles.Any(role => role.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(user => user.IsActive)
            .ThenBy(user => user.DisplayName)
            .ToArray();
        OnPropertyChanged(nameof(IsFiltered));
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(TotalUserCount));
        OnPropertyChanged(nameof(ActiveUserCount));
        OnPropertyChanged(nameof(InactiveUserCount));
        OnPropertyChanged(nameof(AdminCount));
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
