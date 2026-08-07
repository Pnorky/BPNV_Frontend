using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using AvaloniaApp.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly StoreState _store;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private ISolidColorBrush? _statusColor;

    [ObservableProperty]
    private bool _hasError;

    public MainViewModel(StoreState store)
    {
        _store = store;
    }

    [RelayCommand]
    private void Login()
    {
        HasError = false;
        StatusMessage = "";

        if (Username == "admin" && Password == "password123")
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var loginWindow = desktop.MainWindow;
                var dashboard = new DashboardWindow
                {
                    DataContext = new DashboardViewModel(_store)
                };
                desktop.MainWindow = dashboard;
                dashboard.Show();
                loginWindow?.Close();
            }
        }
        else
        {
            StatusMessage = "Invalid username or password.";
            StatusColor = new SolidColorBrush(Colors.Red);
            HasError = true;
        }
    }
}
