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

public partial class DashboardViewModel : ObservableObject, IDisposable, IInputValidationNotifier
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
    private bool _hasThemeOverride;
    private string? _collapsedInventoryTag;
    private readonly DispatcherTimer _clockTimer;
    private int _refreshTicks;
    private SalesViewModel? _salesPage;
    private CashierShiftViewModel? _cashierShiftPage;
    private CashierShiftManagementViewModel? _shiftManagementPage;
    private AdminCashierOperationsViewModel? _cashierOperationsPage;
    private AdminNotificationsViewModel? _adminNotificationsPage;
    private bool _disposed;

    [ObservableProperty]
    private bool _sidebarCollapsed;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _pageTitle = "Overview";

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    [ObservableProperty]
    private bool _isDarkTheme;

    public ObservableCollection<NavItem> NavItems { get; } = [];
    public CashierShiftState CashierShift { get; }
    public bool IsCashier => _session.HasRole("Cashier");
    public bool IsAdmin => _session.HasRole("Admin");
    public AdminNotificationState? AdminNotifications { get; }
    public int AdminUnreadCount => AdminNotifications?.UnreadCount ?? 0;
    public string AdminNotificationDisplay => AdminUnreadCount > 99 ? "Notifications 99+" : $"Notifications {AdminUnreadCount}";
    public string StoreClockDisplay => StoreDateTime.StoreNow.ToString("ddd, MMM d  h:mm:ss tt");
    public string ShiftStatusDisplay
    {
        get
        {
            if (CashierShift.OpenSession is not { } session) return CashierShift.StatusDisplay;
            var start = StoreDateTime.ToStoreTimeFromUtc(session.ScheduledStartAtUtc);
            var end = StoreDateTime.ToStoreTimeFromUtc(session.ScheduledEndAtUtc);
            var clockedIn = StoreDateTime.ToStoreTimeFromUtc(session.ClockedInAtUtc);
            var lateState = DateTime.UtcNow > session.ScheduledEndAtUtc.ToUniversalTime() ? " | Past scheduled end" : "";
            return $"{session.ShiftName} | {start:h:mm tt}-{end:h:mm tt} | In {clockedIn:h:mm tt} | {ShiftElapsedDisplay}{lateState}";
        }
    }
    public string ShiftElapsedDisplay
    {
        get
        {
            if (CashierShift.OpenSession is not { } session) return "";
            var elapsed = DateTime.UtcNow - session.ClockedInAtUtc.ToUniversalTime();
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }
    }
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
        CashierShift = new CashierShiftState(storeClient);
        CashierShift.PropertyChanged += OnCashierShiftChanged;
        if (_session.HasRole("Admin"))
        {
            AdminNotifications = new AdminNotificationState(storeClient, notifications);
            AdminNotifications.PropertyChanged += OnAdminNotificationsChanged;
        }
        _session.Changed += OnSessionChanged;
        if (Application.Current is { } application)
        {
            IsDarkTheme = application.ActualThemeVariant == ThemeVariant.Dark;
            application.ActualThemeVariantChanged += OnActualThemeVariantChanged;
        }
        _allowedNavItems = _allNavItems.Where(item => CanNavigateTo(item.Tag)).ToArray();
        foreach (var item in _allowedNavItems) NavItems.Add(item);
        SelectedNavItem = NavItems[0];
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += OnClockTimerTick;
        _clockTimer.Start();
        if (_session.HasRole("Cashier")) _ = CashierShift.RefreshAsync();
        if (AdminNotifications is not null) _ = AdminNotifications.RefreshAsync();
    }

    public void ShowInputValidationError(string message) =>
        _notifications.ShowError("Invalid number", message);

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
        PageTitle = tag switch
        {
            "Dashboard" => "Overview",
            "CashierShift" => "Cashier Shift",
            "CashierSales" => "My Sales",
            "Sales" => "Sale",
            "InventoryProducts" => "Products",
            "InventoryAddProduct" => "Add Product",
            "InventoryReceiveStock" => "Receive Stock",
            "InventoryBatchReceive" => "Batch Receive",
            "InventoryDeliveryHistory" => "Delivery History",
            "InventoryImport" => "Import Excel",
            "InventorySuppliers" => "Suppliers",
            "InventoryMovements" => "Stock Activity",
            "Reports" => "Reports",
            "CashierShiftManagement" => "Cashier Shifts",
            "CashierOperations" => "Cashier Operations",
            "AdminNotifications" => "Admin Notifications",
            "Employees" => "Employees",
            "Users" => "Users",
            _ => "Overview"
        };
        CurrentPage = tag switch
        {
            "Dashboard" => new DashboardPageViewModel(_storeClient, _notifications, IsCashier),
            "CashierShift" => _cashierShiftPage ??= new CashierShiftViewModel(CashierShift, _storeClient, _notifications, () => _salesPage?.Cart.Count > 0),
            "CashierSales" => new CashierSalesViewModel(_storeClient),
            "Sales" => _salesPage ??= new SalesViewModel(_storeClient, _notifications, CashierShift),
            "InventoryProducts" => new ProductCatalogViewModel(_storeClient, _notifications),
            "InventoryAddProduct" => new AddProductViewModel(_storeClient, _notifications),
            "InventoryReceiveStock" => new StockReceivingViewModel(_storeClient, _notifications),
            "InventoryBatchReceive" => new BatchReceivingViewModel(_storeClient, _notifications),
            "InventoryDeliveryHistory" => new DeliveryHistoryViewModel(_storeClient, _notifications),
            "InventoryImport" => new ExcelInventoryImportViewModel(_storeClient, _notifications),
            "InventorySuppliers" => new SuppliersViewModel(_storeClient, _notifications),
            "InventoryMovements" => new ApiStockMovementsViewModel(_storeClient, _notifications),
            "Reports" => new ReportsViewModel(_storeClient),
            "CashierShiftManagement" => _shiftManagementPage ??= new CashierShiftManagementViewModel(_storeClient, _notifications),
            "CashierOperations" => _cashierOperationsPage ??= new AdminCashierOperationsViewModel(_storeClient, _notifications),
            "AdminNotifications" => CreateAdminNotificationsPage(),
            "Employees" => new EmployeesViewModel(_storeClient, _notifications),
            "Users" => new UsersViewModel(_storeClient, _notifications),
            _ => new DashboardPageViewModel(_storeClient, _notifications, IsCashier)
        };
        if (tag is "Dashboard" or "AdminNotifications" && AdminNotifications is not null)
            _ = AdminNotifications.RefreshAsync();
    }

    public void OpenInventorySection(string tag)
    {
        if (!CanNavigateTo(tag)) return;
        if (!tag.StartsWith("Inventory", StringComparison.Ordinal))
        {
            var item = NavItems.FirstOrDefault(item => item.Tag == tag);
            if (item is not null) SelectNavItem(item);
            else NavigateTo(tag);
            return;
        }
        _collapsedInventoryTag = tag;
        if (!SidebarCollapsed)
        {
            var child = NavItems.FirstOrDefault(item => item.IsChild && item.Tag == tag);
            if (child is not null)
            {
                SelectNavItem(child);
                return;
            }
        }

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
        _hasThemeOverride = true;
        IsDarkTheme = !IsDarkTheme;
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (!_hasThemeOverride && Application.Current is { } application)
            IsDarkTheme = application.ActualThemeVariant == ThemeVariant.Dark;
    }

    [RelayCommand]
    private async Task Logout()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            return;

        var dialog = new ConfirmDialog();
        if (CashierShift.IsClockedIn)
        {
            dialog.SetInformation("Clock out before logging out", "Your cashier session remains open. Complete or clear any current sale, then clock out before logging out.");
            await dialog.ShowDialog(window);
            var shiftItem = NavItems.FirstOrDefault(item => item.Tag == "CashierShift");
            if (shiftItem is not null) SelectNavItem(shiftItem);
            return;
        }
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
        "CashierShift" or "Sales" or "CashierSales" => _session.HasRole("Cashier"),
        "InventoryProducts" or "InventoryAddProduct" or "InventoryReceiveStock" or
        "InventoryBatchReceive" or "InventoryDeliveryHistory" or "InventoryImport" or "InventorySuppliers" or "InventoryMovements" or "Reports" =>
            _session.HasRole("Admin") || _session.HasRole("Inventory"),
        "Employees" => _session.HasRole("Admin") || _session.HasRole("Inventory"),
        "CashierShiftManagement" or "CashierOperations" or "AdminNotifications" => _session.HasRole("Admin"),
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
        OnPropertyChanged(nameof(IsCashier));
        OnPropertyChanged(nameof(IsAdmin));
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
        CashierShift.Reset();
        Dispose();
        var login = new MainWindow { DataContext = new MainViewModel(_store, _authClient, _storeClient, _session) };
        desktop.MainWindow = login;
        login.Show();
        window.Close();
    }

    private void OnClockTimerTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(StoreClockDisplay));
        OnPropertyChanged(nameof(ShiftElapsedDisplay));
        OnPropertyChanged(nameof(ShiftStatusDisplay));
        if (++_refreshTicks >= 30)
        {
            _refreshTicks = 0;
            if (IsCashier) _ = CashierShift.RefreshAsync();
            if (AdminNotifications is not null) _ = AdminNotifications.RefreshAsync();
        }
    }

    private void OnCashierShiftChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShiftStatusDisplay));
        OnPropertyChanged(nameof(ShiftElapsedDisplay));
    }

    private AdminNotificationsViewModel CreateAdminNotificationsPage()
    {
        if (_adminNotificationsPage is not null) return _adminNotificationsPage;
        if (AdminNotifications is null) throw new InvalidOperationException("Admin notification state is unavailable.");
        _adminNotificationsPage = new AdminNotificationsViewModel(AdminNotifications);
        _adminNotificationsPage.ShiftSessionRequested += OnShiftSessionRequested;
        return _adminNotificationsPage;
    }

    private void OnShiftSessionRequested(Guid sessionId)
    {
        var operationsItem = NavItems.FirstOrDefault(item => item.Tag == "CashierOperations");
        if (operationsItem is not null) SelectNavItem(operationsItem);
        _cashierOperationsPage ??= new AdminCashierOperationsViewModel(_storeClient, _notifications);
        _ = _cashierOperationsPage.OpenSessionAsync(sessionId);
    }

    private void OnAdminNotificationsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AdminNotificationState.UnreadCount)) return;
        OnPropertyChanged(nameof(AdminUnreadCount));
        OnPropertyChanged(nameof(AdminNotificationDisplay));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _clockTimer.Stop();
        _clockTimer.Tick -= OnClockTimerTick;
        CashierShift.PropertyChanged -= OnCashierShiftChanged;
        _session.Changed -= OnSessionChanged;
        if (Application.Current is { } application)
            application.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        _salesPage?.Dispose();
        _cashierShiftPage?.Dispose();
        _cashierOperationsPage?.Dispose();
        if (_adminNotificationsPage is not null)
        {
            _adminNotificationsPage.ShiftSessionRequested -= OnShiftSessionRequested;
            _adminNotificationsPage.Dispose();
        }
        if (AdminNotifications is not null)
        {
            AdminNotifications.PropertyChanged -= OnAdminNotificationsChanged;
            AdminNotifications.Dispose();
        }
    }

}
