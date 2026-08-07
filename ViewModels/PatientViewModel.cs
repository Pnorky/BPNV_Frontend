using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class PatientViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private IReadOnlyList<PatientRecord> _filteredPatients;

    private readonly List<PatientRecord> _allPatients = SampleData.Patients;

    public PatientViewModel() => _filteredPatients = _allPatients;

    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(HasSearch));
    }

    private void ApplyFilter()
    {
        FilteredPatients = string.IsNullOrWhiteSpace(SearchText)
            ? _allPatients
            : _allPatients.Where(p =>
                p.PatientId.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.LastName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.FirstName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Status.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    [RelayCommand]
    private void NewPatient()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            var dialog = new ConfirmDialog();
            dialog.SetInformation("New Patient", "The patient registration form would open here.");
            _ = dialog.ShowDialog(window);
        }
    }

    [RelayCommand]
    private async Task ViewPatient(PatientRecord? record)
    {
        if (record is null) return;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            var dialog = new ConfirmDialog();
            dialog.SetInformation("Patient Details", $"{record.FirstName} {record.LastName}\nPatient ID: {record.PatientId}");
            await dialog.ShowDialog(window);
        }
    }
}
