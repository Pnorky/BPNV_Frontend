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
    private readonly INotificationService _notifications;
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
    public string UserInitials => string.Concat(UserDisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])));
    public string RoleDisplay => _session.User is { Roles.Count: > 0 } user
        ? string.Join(" / ", user.Roles)
        : "No assigned role";

    public DashboardViewModel(
        StoreState store,
        AuthApiClient authClient,
        StoreApiClient storeClient,
        AuthSession session,
        INotificationService notifications)
    {
        _store = store;
        _authClient = authClient;
        _storeClient = storeClient;
        _session = session;
        _notifications = notifications;
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
            "Dashboard" => new DashboardPageViewModel(_storeClient, _notifications),
            "Sales" => new SalesViewModel(_storeClient, _notifications),
            "InventoryProducts" => new ProductCatalogViewModel(_storeClient, _notifications),
            "InventoryAddProduct" => new AddProductViewModel(_storeClient, _notifications),
            "InventoryReceiveStock" => new StockReceivingViewModel(_storeClient, _notifications),
            "InventoryBatchReceive" => new BatchReceivingViewModel(_storeClient, _notifications),
            "InventoryImport" => new ExcelInventoryImportViewModel(_storeClient, _notifications),
            "InventorySuppliers" => new SuppliersViewModel(_storeClient, _notifications),
            "InventoryMovements" => new ApiStockMovementsViewModel(_storeClient, _notifications),
            "Reports" => new ReportsViewModel(_storeClient),
            "Users" => new UsersViewModel(_storeClient, _notifications),
            _ => new DashboardPageViewModel(_storeClient, _notifications)
        };
    }

    public void OpenInventorySection(string tag)
    {
        if (!CanNavigateTo(tag)) return;
        _collapsedInventoryTag = tag;
        var inventoryParent = NavItems.FirstOrDefault(item => !item.IsChild && item.Tag == "InventoryProducts");
        if (SidebarCollapsed && inventoryParent is not null && !ReferenceEquals(SelectedNavItem, inventoryParent))
        {
            _suppressNavigation = true;
            SelectedNavItem = inventoryParent;
            _suppressNavigation = false;
        }
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
        "InventoryBatchReceive" or "InventoryImport" or "InventorySuppliers" or "InventoryMovements" or "Reports" =>
            _session.HasRole("Admin") || _session.HasRole("Inventory"),
        "Users" => _session.HasRole("Admin"),
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
