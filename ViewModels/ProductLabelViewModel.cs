using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaApp.Services;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class ProductLabelViewModel(
    ProductResponse product,
    StoreApiClient api,
    INotificationService notifications) : ObservableObject
{
    [ObservableProperty] private ProductUnitResponse? _selectedUnit = product.Units.FirstOrDefault(unit => unit.IsActive && unit.IsBasePiece)
        ?? product.Units.FirstOrDefault(unit => unit.IsActive);
    [ObservableProperty] private string _statusMessage = "Select an active unit to generate or print its Code 128 label.";
    [ObservableProperty] private bool _isBusy;

    public ProductResponse Product { get; } = product;
    public IReadOnlyList<ProductUnitResponse> Units { get; } = product.Units.Where(unit => unit.IsActive).ToArray();
    public bool HasBarcode => !string.IsNullOrWhiteSpace(SelectedUnit?.Barcode);

    partial void OnSelectedUnitChanged(ProductUnitResponse? value)
    {
        StatusMessage = value is null
            ? "Select an active unit."
            : string.IsNullOrWhiteSpace(value.Barcode)
                ? "This unit has no barcode. Generate one before exporting a label."
                : $"Current barcode: {value.Barcode}";
        OnPropertyChanged(nameof(HasBarcode));
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (SelectedUnit is null || IsBusy) return;
        if (HasBarcode)
        {
            StatusMessage = "This unit already has a barcode. Use Replace barcode if a new value is required.";
            return;
        }
        await MutateBarcodeAsync(replace: false);
    }

    [RelayCommand]
    private async Task ReplaceAsync()
    {
        if (SelectedUnit is null || IsBusy || !HasBarcode || MainWindow() is not { } owner) return;
        var confirmation = new ConfirmDialog();
        confirmation.SetConfirmation(
            "Replace barcode?",
            $"Replace the barcode for {Product.Name} ({SelectedUnit.Label})? The current barcode {SelectedUnit.Barcode} will no longer scan.",
            "Replace barcode");
        await confirmation.ShowDialog(owner);
        if (!confirmation.Confirmed) return;
        await MutateBarcodeAsync(replace: true);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (SelectedUnit is null || IsBusy || MainWindow() is not { } owner) return;
        if (!HasBarcode)
        {
            ShowError("Label not exported", "Generate a barcode for this unit first.");
            return;
        }

        IsBusy = true;
        try
        {
            var label = await api.GetProductLabelDataAsync(Product.Id, SelectedUnit.Id);
            var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export product label",
                SuggestedFileName = SafeFileName($"{label.Sku}-{label.UnitLabel}-label.pdf"),
                DefaultExtension = "pdf",
                FileTypeChoices = [new FilePickerFileType("PDF document") { Patterns = ["*.pdf"] }]
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            BarcodeLabelExportService.ExportPdf(label, stream);
            StatusMessage = $"Label exported for {label.ProductName}, {label.UnitLabel}.";
            notifications.ShowSuccess("Label exported", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception) || exception is IOException)
        {
            ShowError("Label not exported", FailureMessage(exception));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportImageAsync()
    {
        if (SelectedUnit is null || IsBusy || MainWindow() is not { } owner) return;
        if (!HasBarcode)
        {
            ShowError("Image not exported", "Generate a barcode for this unit first.");
            return;
        }

        IsBusy = true;
        try
        {
            var label = await api.GetProductLabelDataAsync(Product.Id, SelectedUnit.Id);
            var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export barcode image",
                SuggestedFileName = SafeFileName($"{label.Sku}-{label.UnitLabel}-barcode.png"),
                DefaultExtension = "png",
                FileTypeChoices = [new FilePickerFileType("PNG image") { Patterns = ["*.png"] }]
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            BarcodeLabelExportService.ExportPng(label, stream);
            StatusMessage = $"Barcode image exported for {label.ProductName}, {label.UnitLabel}.";
            notifications.ShowSuccess("Barcode image exported", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception) || exception is IOException)
        {
            ShowError("Image not exported", FailureMessage(exception));
        }
        finally { IsBusy = false; }
    }

    private async Task MutateBarcodeAsync(bool replace)
    {
        if (SelectedUnit is null) return;
        IsBusy = true;
        try
        {
            var label = replace
                ? await api.ReplaceProductBarcodeAsync(Product.Id, SelectedUnit.Id)
                : await api.GenerateProductBarcodeAsync(Product.Id, SelectedUnit.Id);
            SelectedUnit = SelectedUnit with { Barcode = label.Barcode };
            StatusMessage = $"Barcode {label.Barcode} {(replace ? "replaced" : "generated")}.";
            notifications.ShowSuccess(replace ? "Barcode replaced" : "Barcode generated", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError(replace ? "Barcode not replaced" : "Barcode not generated", FailureMessage(exception));
        }
        finally { IsBusy = false; }
    }

    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        notifications.ShowError(title, message);
    }

    private static string SafeFileName(string value)
    {
        foreach (var character in Path.GetInvalidFileNameChars()) value = value.Replace(character, '-');
        return value;
    }

    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception switch
    {
        HttpRequestException => "Cannot reach the store API.",
        TaskCanceledException => "The store API did not respond in time.",
        _ => exception.Message
    };
    private static Avalonia.Controls.Window? MainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } ? window : null;
}
