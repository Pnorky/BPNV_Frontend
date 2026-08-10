using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly StoreState _store;
    private readonly AuthApiClient _authClient;
    private readonly StoreApiClient _storeClient;
    private readonly AuthSession _session;

    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private ISolidColorBrush? _statusColor;
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _requiresPasswordChange;

    public bool CanLogin => !RequiresPasswordChange;

    public MainViewModel(StoreState store, AuthApiClient authClient, StoreApiClient storeClient, AuthSession session)
    {
        _store = store;
        _authClient = authClient;
        _storeClient = storeClient;
        _session = session;
    }

    partial void OnRequiresPasswordChangeChanged(bool value) => OnPropertyChanged(nameof(CanLogin));

    [RelayCommand]
    private async Task LoginAsync()
    {
        ClearStatus();
        _session.Clear();
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        {
            ShowError("Enter your username and password.");
            return;
        }

        try
        {
            var user = await _authClient.LoginAsync(Username, Password);
            if (user.MustChangePassword)
            {
                RequiresPasswordChange = true;
                ShowStatus("Create a new password before continuing.", Colors.DarkOrange);
                return;
            }

            Password = "";
            OpenDashboard();
        }
        catch (ApiClientException exception)
        {
            ShowError(exception.Message);
        }
        catch (HttpRequestException)
        {
            ShowError($"Cannot reach the server at {_authClient.BaseAddress}.");
        }
        catch (TaskCanceledException)
        {
            ShowError("The server did not respond in time.");
        }
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        ClearStatus();
        if (NewPassword.Length < 12)
        {
            ShowError("The new password must contain at least 12 characters.");
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ShowError("The new passwords do not match.");
            return;
        }

        if (NewPassword == Password)
        {
            ShowError("The new password must differ from the current password.");
            return;
        }

        try
        {
            await _authClient.ChangePasswordAsync(Password, NewPassword);
            Password = "";
            NewPassword = "";
            ConfirmPassword = "";
            RequiresPasswordChange = false;
            OpenDashboard();
        }
        catch (ApiClientException exception)
        {
            ShowError(exception.Message);
        }
        catch (HttpRequestException)
        {
            ShowError($"Cannot reach the server at {_authClient.BaseAddress}.");
        }
        catch (TaskCanceledException)
        {
            ShowError("The server did not respond in time.");
        }
    }

    [RelayCommand]
    private async Task CancelPasswordChangeAsync()
    {
        try
        {
            await _authClient.LogoutAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ApiClientException)
        {
            _session.Clear();
        }

        Password = "";
        NewPassword = "";
        ConfirmPassword = "";
        RequiresPasswordChange = false;
        ClearStatus();
    }

    private void OpenDashboard()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var loginWindow = desktop.MainWindow;
        var dashboard = new DashboardWindow
        {
            DataContext = new DashboardViewModel(_store, _authClient, _storeClient, _session)
        };
        desktop.MainWindow = dashboard;
        dashboard.Show();
        loginWindow?.Close();
    }

    private void ClearStatus()
    {
        HasStatus = false;
        StatusMessage = "";
    }

    private void ShowError(string message) => ShowStatus(message, Colors.Red);

    private void ShowStatus(string message, Color color)
    {
        StatusMessage = message;
        StatusColor = new SolidColorBrush(color);
        HasStatus = true;
    }
}
