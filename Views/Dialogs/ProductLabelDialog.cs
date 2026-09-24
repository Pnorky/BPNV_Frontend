using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class ProductLabelDialog : Window
{
    public ProductLabelDialog(ProductLabelViewModel viewModel)
    {
        Title = $"Labels - {viewModel.Product.Name}";
        Width = 760;
        Height = 390;
        MinWidth = 560;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        DataContext = viewModel;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var unit = new SearchableSelect { PlaceholderText = "Select active unit" };
        unit.Bind(SearchableSelect.ItemsSourceProperty, new Binding(nameof(ProductLabelViewModel.Units)));
        unit.Bind(SearchableSelect.SelectedItemProperty, new Binding(nameof(ProductLabelViewModel.SelectedUnit)));
        unit.ItemTemplate = new FuncDataTemplate<ProductUnitResponse>((item, _) =>
            new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = item.Label },
                    Muted(string.IsNullOrWhiteSpace(item.Barcode) ? "No barcode yet" : item.Barcode)
                }
            }, true);
        var generate = Action("Generate missing barcode", nameof(ProductLabelViewModel.GenerateCommand));
        var replace = Action("Replace barcode", nameof(ProductLabelViewModel.ReplaceCommand));
        var image = Action("Export image", nameof(ProductLabelViewModel.ExportImageCommand));
        var export = Action("Export printable PDF", nameof(ProductLabelViewModel.ExportCommand));
        var close = new ActionButton("Close", ActionButtonVariant.Secondary);
        close.Click += (_, _) => Close();

        var status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(ProductLabelViewModel.StatusMessage)));
        var statusHost = new Border { Padding = new Thickness(12, 10), Child = status };
        statusHost.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary"));

        var title = new TextBlock { Text = viewModel.Product.Name };
        title.Classes.Add("h2");
        Content = new Border
        {
            Padding = new Thickness(26),
            Child = new StackPanel
            {
                Spacing = 18,
                Children =
                {
                    new StackPanel { Spacing = 4, Children = { title, Muted("Generate and export Code 128 labels for active piece or package units.") } },
                    Field("ACTIVE UNIT", unit),
                    statusHost,
                    new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 10, LineSpacing = 10, Children = { generate, replace, image, export } },
                    new Grid
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Children = { close }
                    }
                }
            }
        };
        close.HorizontalAlignment = HorizontalAlignment.Right;
    }

    private static ActionButton Action(string text, string command, bool primary = false)
    {
        var button = new ActionButton(text, primary ? ActionButtonVariant.Primary : ActionButtonVariant.Secondary);
        button.Bind(Button.CommandProperty, new Binding(command));
        return button;
    }
    private static StackPanel Field(string label, Control control)
    {
        var caption = new TextBlock { Text = label };
        caption.Classes.Add("form-label");
        return new StackPanel { Spacing = 5, Children = { caption, control } };
    }
    private static TextBlock Muted(string text)
    {
        var value = new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        return value;
    }
}
