using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaApp.Views;
using AvaloniaApp.Views.Dialogs;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly StoreState _store;
    private readonly AuthApiClient _authClient;
    private readonly StoreApiClient _storeClient;
    private readonly AuthSession _session;
    private readonly IReadOnlyList<NavItem> _allNavItems = SampleData.NavItems;
    private IReadOnlyList<NavItem> _allowedNavItems = [];
    private bool _suppressNavigation;
    private bool _returningToLogin;
    private string? _collapsedInventoryTag;

    [ObservableProperty]
    private bool _sidebarCollapsed;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    [ObservableProperty]
    private bool _isDarkTheme;

    public ObservableCollection<NavItem> NavItems { get; } = [];
    public string UserDisplayName => _session.User?.DisplayName ?? _session.User?.Username ?? "Store User";
    public string RoleDisplay => _session.User is { Roles.Count: > 0 } user
        ? string.Join(" / ", user.Roles)
        : "No assigned role";

    public DashboardViewModel(StoreState store, AuthApiClient authClient, StoreApiClient storeClient, AuthSession session)
    {
        _store = store;
        _authClient = authClient;
        _storeClient = storeClient;
        _session = session;
        _session.Changed += OnSessionChanged;
        _allowedNavItems = _allNavItems.Where(item => CanNavigateTo(item.Tag)).ToArray();
        foreach (var item in _allowedNavItems) NavItems.Add(item);
        SelectedNavItem = NavItems[0];
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is not null && !_suppressNavigation)
        {
            if (!value.IsChild) _collapsedInventoryTag = null;
            NavigateTo(value.Tag);
        }
    }

    private void NavigateTo(string tag)
    {
        if (!CanNavigateTo(tag)) tag = "Dashboard";
        CurrentPage = tag switch
        {
            "Dashboard" => new DashboardPageViewModel(_store),
            "Sales" => new SalesViewModel(_storeClient),
            "InventoryProducts" => new ProductCatalogViewModel(_storeClient),
            "InventoryAddProduct" => new AddProductViewModel(_storeClient),
            "InventoryReceiveStock" => new StockReceivingViewModel(_storeClient),
            "InventorySuppliers" => new InventoryViewModel(_store, 1),
            "InventoryMovements" => new InventoryViewModel(_store, 2),
            "Reports" => new ReportsViewModel(_store),
            _ => new DashboardPageViewModel(_store)
        };
    }

    public void OpenInventorySection(string tag)
    {
        if (!CanNavigateTo(tag)) return;
        _collapsedInventoryTag = tag;
        NavigateTo(tag);
    }

    public void SelectNavItem(NavItem item)
    {
        if (!CanNavigateTo(item.Tag)) return;
        if (item.IsChild) _collapsedInventoryTag = item.Tag;
        if (ReferenceEquals(SelectedNavItem, item))
            NavigateTo(item.Tag);
        else
            SelectedNavItem = item;
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        if (!SidebarCollapsed)
        {
            var collapseSelectedTag = SelectedNavItem?.Tag;
            if (SelectedNavItem?.IsChild == true) _collapsedInventoryTag = SelectedNavItem.Tag;
            _suppressNavigation = true;
            NavItems.Clear();
            foreach (var item in _allowedNavItems.Where(item => !item.IsChild)) NavItems.Add(item);
            SelectedNavItem = _collapsedInventoryTag is null
                ? NavItems.FirstOrDefault(item => item.Tag == collapseSelectedTag) ?? NavItems[0]
                : NavItems.First(item => item.Tag == "InventoryProducts");
            _suppressNavigation = false;
            SidebarCollapsed = true;
            return;
        }

        var selectedTag = _collapsedInventoryTag ?? SelectedNavItem?.Tag;
        _suppressNavigation = true;
        NavItems.Clear();
        foreach (var item in _allowedNavItems) NavItems.Add(item);
        SelectedNavItem = NavItems.FirstOrDefault(item => item.Tag == selectedTag &&
            (_collapsedInventoryTag is null || item.IsChild)) ?? NavItems[0];
        _suppressNavigation = false;
        SidebarCollapsed = false;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    [RelayCommand]
    private async Task Logout()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            return;

        var dialog = new ConfirmDialog();
        dialog.SetConfirmation("Log out?", "Are you sure you want to end your current session?", "Log out");
        await dialog.ShowDialog(window);
        if (!dialog.Confirmed) return;

        try
        {
            await _authClient.LogoutAsync();
        }
        catch (HttpRequestException)
        {
            _session.Clear();
        }
        catch (TaskCanceledException)
        {
            _session.Clear();
        }
        catch (ApiClientException)
        {
            _session.Clear();
        }

        ReturnToLogin();
    }

    private bool CanNavigateTo(string tag) => tag switch
    {
        "Dashboard" => _session.IsAuthenticated,
        "Sales" => _session.HasRole("Admin") || _session.HasRole("Cashier"),
        "InventoryProducts" or "InventoryAddProduct" or "InventoryReceiveStock" or
        "InventorySuppliers" or "InventoryMovements" or "Reports" =>
            _session.HasRole("Admin") || _session.HasRole("Inventory"),
        _ => false
    };

    private void OnSessionChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ApplySessionChange);

    private void ApplySessionChange()
    {
        if (!_session.IsAuthenticated)
        {
            ReturnToLogin();
            return;
        }

        OnPropertyChanged(nameof(UserDisplayName));
        OnPropertyChanged(nameof(RoleDisplay));
        var selectedTag = SelectedNavItem?.Tag;
        _allowedNavItems = _allNavItems.Where(item => CanNavigateTo(item.Tag)).ToArray();
        _suppressNavigation = true;
        NavItems.Clear();
        foreach (var item in SidebarCollapsed
                     ? _allowedNavItems.Where(item => !item.IsChild)
                     : _allowedNavItems)
        {
            NavItems.Add(item);
        }

        SelectedNavItem = NavItems.FirstOrDefault(item => item.Tag == selectedTag) ?? NavItems[0];
        _suppressNavigation = false;
        NavigateTo(SelectedNavItem.Tag);
    }

    private void ReturnToLogin()
    {
        if (_returningToLogin ||
            Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } desktop)
            return;

        _returningToLogin = true;
        _session.Changed -= OnSessionChanged;
        var login = new MainWindow { DataContext = new MainViewModel(_store, _authClient, _storeClient, _session) };
        desktop.MainWindow = login;
        login.Show();
        window.Close();
    }

}
