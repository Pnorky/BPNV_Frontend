using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class PharmacyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = "";

    private readonly List<MedicineRecord> _allMedicines = SampleData.Medicines;

    public TablePager<MedicineRecord> Pager { get; }
    public IReadOnlyList<MedicineRecord> Medicines => Pager.Items;

    public PharmacyViewModel()
    {
        Pager = new TablePager<MedicineRecord>(_allMedicines, "medicine", "medicines");
        Pager.ConfigureStateActions("Add Medicine", AddMedicine, () => SearchText = "", ApplyFilter);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allMedicines
            : _allMedicines.Where(m =>
                m.MedicineName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.MedicineCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        Pager.SetItems(filtered, !string.IsNullOrWhiteSpace(SearchText));
    }

    [RelayCommand]
    private void AddMedicine()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            var dialog = new ConfirmDialog();
            dialog.SetInformation("Add Medicine", "The medicine form would open here.");
            _ = dialog.ShowDialog(window);
        }
    }
}
