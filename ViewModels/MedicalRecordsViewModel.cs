using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class MedicalRecordsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = "";

    private readonly List<MedicalRecord> _allRecords = SampleData.MedicalRecords;

    public TablePager<MedicalRecord> Pager { get; }
    public IReadOnlyList<MedicalRecord> Records => Pager.Items;

    public MedicalRecordsViewModel()
    {
        Pager = new TablePager<MedicalRecord>(_allRecords, "record request", "medical records");
        Pager.ConfigureStateActions("New Record Request", NewRecordRequest, () => SearchText = "", ApplyFilter);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allRecords
            : _allRecords.Where(r =>
                r.MRN.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.PatientName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        Pager.SetItems(filtered, !string.IsNullOrWhiteSpace(SearchText));
    }

    [RelayCommand]
    private void NewRecordRequest()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            var dialog = new ConfirmDialog();
            dialog.SetInformation("New Record Request", "The record request form would open here.");
            _ = dialog.ShowDialog(window);
        }
    }
}
