using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class StockReceivingView : UserControl
{
    private readonly TextBox _scanner;
    private StockReceivingViewModel? _viewModel;

    public StockReceivingView()
    {
        var title = new TextBlock { Text = "Receive stock" }; title.Classes.Add("h1");
        _scanner = Input("ScannerText", "Scan piece or package barcode, then press Enter");
        _scanner.KeyDown += OnScannerKeyDown;

        var selection = Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Heading("Selected unit"), Bound("SelectedName", 18, FontWeight.SemiBold),
                Muted("UnitDetails"), Bound("ConversionPreview", 13, FontWeight.SemiBold)
            }
        });

        var submit = Button("Receive into bodega", "SubmitReceiptCommand", true);
        submit.VerticalAlignment = VerticalAlignment.Bottom;
        var form = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("0.55*,1*,1.5*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                Field("COUNT OF SCANNED UNIT", Number("Count")),
                At(Field("REFERENCE", Input("Reference", "Delivery receipt")), 1),
                At(Field("NOTES", Input("Notes", "Optional notes")), 2),
                At(submit, 3)
            }
        };

        var root = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, StaticMuted("Exact barcode lookup supports base pieces and package units.") } },
                Card(Field("BARCODE SCANNER", _scanner)), selection, Card(form), Status()
            }
        };
        Content = new ScrollViewer { Margin = new Thickness(30), Content = root };
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => FocusScanner();
    }

    private async void OnScannerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not StockReceivingViewModel viewModel) return;
        e.Handled = true;
        await viewModel.LookupBarcodeAsync();
        FocusScanner();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null) _viewModel.ScannerFocusRequested -= OnScannerFocusRequested;
        _viewModel = DataContext as StockReceivingViewModel;
        if (_viewModel is not null) _viewModel.ScannerFocusRequested += OnScannerFocusRequested;
    }

    private void OnScannerFocusRequested(object? sender, EventArgs e) => FocusScanner();
    private void FocusScanner() => Dispatcher.UIThread.Post(() => _scanner.Focus());
    private static StackPanel Field(string label, Control control) { var caption = new TextBlock { Text = label }; caption.Classes.Add("form-label"); return new StackPanel { Spacing = 5, Children = { caption, control } }; }
    private static TextBox Input(string path, string placeholder) { var value = new TextBox { PlaceholderText = placeholder }; value.Classes.Add("form-input"); value.Bind(TextBox.TextProperty, new Binding(path)); return value; }
    private static NumberField Number(string path) { var value = new NumberField { Minimum = 1, Increment = 1, FormatString = "0" }; value.Bind(NumberField.ValueProperty, new Binding(path)); return value; }
    private static TextBlock Heading(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("h3"); return value; }
    private static TextBlock Bound(string path, double size, FontWeight weight) { var value = new TextBlock { FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap }; value.Bind(TextBlock.TextProperty, new Binding(path)); return value; }
    private static TextBlock Muted(string path) { var value = Bound(path, 12, FontWeight.Normal); value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static TextBlock StaticMuted(string text) { var value = new TextBlock { Text = text }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static Button Button(string text, string command, bool primary) { var value = new Button { Content = text }; value.Classes.Add(primary ? "primary" : "secondary"); value.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(command)); return value; }
    private static Border Card(Control child) { var value = new Border { Padding = new Thickness(20), Child = child }; value.Classes.Add("theme-card"); value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card")); return value; }
    private static Border Status() { var value = new Border { Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(7), Child = Bound("StatusMessage", 12, FontWeight.Normal) }; value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return value; }
    private static T At<T>(T value, int column) where T : Control { Grid.SetColumn(value, column); return value; }
}
