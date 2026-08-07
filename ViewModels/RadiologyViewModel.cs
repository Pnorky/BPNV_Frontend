using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class RadiologyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = "";

    private readonly List<RadiologyRecord> _allOrders = SampleData.RadiologyOrders;

    public TablePager<RadiologyRecord> Pager { get; }
    public IReadOnlyList<RadiologyRecord> Orders => Pager.Items;

    public RadiologyViewModel()
    {
        Pager = new TablePager<RadiologyRecord>(_allOrders, "radiology order", "radiology orders");
        Pager.ConfigureStateActions("Create Order", NewOrder, () => SearchText = "", ApplyFilter);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allOrders
            : _allOrders.Where(r =>
                r.OrderNo.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.Patient.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.Procedure.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        Pager.SetItems(filtered, !string.IsNullOrWhiteSpace(SearchText));
    }

    [RelayCommand]
    private void NewOrder()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            var dialog = new ConfirmDialog();
            dialog.SetInformation("New Radiology Order", "The radiology order form would open here.");
            _ = dialog.ShowDialog(window);
        }
    }
}
