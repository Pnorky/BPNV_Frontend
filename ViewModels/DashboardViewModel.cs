using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using AvaloniaApp.Views;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly StoreState _store;
    private readonly IReadOnlyList<NavItem> _allNavItems = SampleData.NavItems;
    private bool _suppressNavigation;
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

    public DashboardViewModel(StoreState store)
    {
        _store = store;
        foreach (var item in _allNavItems) NavItems.Add(item);
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
        CurrentPage = tag switch
        {
            "Dashboard" => new DashboardPageViewModel(_store),
            "Sales" => new SalesViewModel(_store),
            "InventoryProducts" => new InventoryViewModel(_store, 0),
            "InventorySuppliers" => new InventoryViewModel(_store, 1),
            "InventoryMovements" => new InventoryViewModel(_store, 2),
            "Reports" => new ReportsViewModel(_store),
            _ => new DashboardPageViewModel(_store)
        };
    }

    public void OpenInventorySection(string tag)
    {
        _collapsedInventoryTag = tag;
        NavigateTo(tag);
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
            foreach (var item in _allNavItems.Where(item => !item.IsChild)) NavItems.Add(item);
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
        foreach (var item in _allNavItems) NavItems.Add(item);
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
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } desktop)
            return;

        var dialog = new ConfirmDialog();
        dialog.SetConfirmation("Log out?", "Are you sure you want to end your current session?", "Log out");
        await dialog.ShowDialog(window);
        if (!dialog.Confirmed) return;

        var login = new MainWindow { DataContext = new MainViewModel(_store) };
        desktop.MainWindow = login;
        login.Show();
        window.Close();
    }

}
