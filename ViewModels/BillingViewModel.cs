using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class BillingViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private IReadOnlyList<InvoiceRecord> _filteredInvoices;

    private readonly List<InvoiceRecord> _allInvoices = SampleData.Invoices;

    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);

    public BillingViewModel() => _filteredInvoices = _allInvoices;

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(HasSearch));
    }

    private void ApplyFilter()
    {
        FilteredInvoices = string.IsNullOrWhiteSpace(SearchText)
            ? _allInvoices
            : _allInvoices.Where(i =>
                i.InvoiceNo.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                i.Patient.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                i.Status.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    [RelayCommand]
    private async Task ViewInvoice(InvoiceRecord? record)
    {
        if (record is null) return;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            var dialog = new ConfirmDialog();
            dialog.SetInformation("Invoice Details", $"Invoice {record.InvoiceNo}\n{record.Patient} · {record.Amount}");
            await dialog.ShowDialog(window);
        }
    }

    [RelayCommand]
    private void NewInvoice()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            var dialog = new ConfirmDialog();
            dialog.SetInformation("New Invoice", "The invoice form would open here.");
            _ = dialog.ShowDialog(window);
        }
    }
}
