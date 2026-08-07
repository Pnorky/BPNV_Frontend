using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public class AdminCard
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
}

public partial class AdminViewModel : ObservableObject
{
    public TablePager<StaffRecord> Pager { get; } = new(SampleData.Staff, "staff account", "staff accounts");
    public IReadOnlyList<StaffRecord> Staff => Pager.Items;

    public List<AdminCard> Cards { get; } = new()
    {
        new() { Title = "User Management", Description = "Manage staff accounts, roles, and permissions", Icon = "Users" },
        new() { Title = "Department Settings", Description = "Configure departments, wards, and service units", Icon = "Building" },
        new() { Title = "System Logs", Description = "View audit logs and system activity", Icon = "ScrollText" },
        new() { Title = "Reports", Description = "Generate hospital statistics and compliance reports", Icon = "BarChart3" },
    };

    [RelayCommand]
    private async Task OpenCard(AdminCard? card)
    {
        if (card is null) return;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            var dialog = new ConfirmDialog();
            dialog.SetInformation(card.Title, card.Description);
            await dialog.ShowDialog(window);
        }
    }
}
