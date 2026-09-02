using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class StockCountDialog : Window
{
    public StockCountDialog(StockCountViewModel viewModel)
    {
        Title = "Record stock count - BPNV Convenience Store";
        Width = 720;
        Height = 590;
        MinWidth = 620;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        DataContext = viewModel;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var title = new TextBlock { Text = "Record physical stock count" };
        title.Classes.Add("h2");
        var product = new SearchableSelect
        {
            PlaceholderText = "Search Supply or Consumable",
            SearchTextSelector = item => item is ProductResponse value
                ? $"{value.Name} {value.Sku} {value.SupplierName} {value.Barcode}"
                : ""
        };
        Bind(product, SearchableSelect.ItemsSourceProperty, nameof(StockCountViewModel.Products));
        Bind(product, SearchableSelect.SelectedItemProperty, nameof(StockCountViewModel.SelectedProduct));
        product.ItemTemplate = new FuncDataTemplate<ProductResponse>((_, _) => new StackPanel
        {
            Children =
            {
                Bound(nameof(ProductResponse.Name), FontWeight.SemiBold),
                Bound(nameof(ProductResponse.Sku), resource: "MutedForeground")
            }
        }, true);

        var location = new SearchableSelect { PlaceholderText = "Select location" };
        Bind(location, SearchableSelect.ItemsSourceProperty, nameof(StockCountViewModel.Locations));
        Bind(location, SearchableSelect.SelectedItemProperty, nameof(StockCountViewModel.SelectedLocation));
        location.ItemTemplate = new FuncDataTemplate<StockCountLocationOption>((_, _) => Bound(nameof(StockCountLocationOption.Label)), true);

        var counted = new NumberField { Minimum = 0, Maximum = int.MaxValue, Increment = 1, FormatString = "0" };
        Bind(counted, NumberField.ValueProperty, nameof(StockCountViewModel.CountedQuantity));
        var notes = new TextBox
        {
            PlaceholderText = "Optional reason, variance explanation, or count note",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 90,
            MaxLength = 500
        };
        notes.Classes.Add("form-input");
        Bind(notes, TextBox.TextProperty, nameof(StockCountViewModel.Notes));

        var current = Metric("CURRENT BALANCE", nameof(StockCountViewModel.CurrentBalanceDisplay));
        var variance = Metric("CALCULATED VARIANCE", nameof(StockCountViewModel.VarianceDisplay));
        var metrics = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12,
            Children = { current, At(variance, column: 1) }
        };

        var fields = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("1.6*,*"),
                    ColumnSpacing = 12,
                    Children = { Field("PRODUCT", product), At(Field("LOCATION", location), column: 1) }
                },
                metrics,
                Field("PHYSICAL QUANTITY REMAINING", counted),
                Field("NOTE", notes)
            }
        };

        var validation = Bound(nameof(StockCountViewModel.ValidationMessage), resource: "Destructive");
        validation.TextWrapping = TextWrapping.Wrap;
        var cancel = new ActionButton("Cancel", ActionButtonVariant.Secondary);
        cancel.Click += (_, _) => Close((RecordStockCountRequest?)null);
        var review = new ActionButton("Review count", ActionButtonVariant.Primary) { IsDefault = true };
        review.Click += (_, _) =>
        {
            if (DataContext is StockCountViewModel model && model.TryBuildRequest(out var request, out _))
                Close(request);
        };
        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 10,
            Children = { validation, At(cancel, column: 1), At(review, column: 2) }
        };

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 18,
            Margin = new Thickness(26),
            Children =
            {
                new StackPanel
                {
                    Spacing = 5,
                    Children =
                    {
                        title,
                        Muted("Enter the quantity physically remaining. The difference is recorded as an auditable stock adjustment.")
                    }
                },
                At(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, row: 1),
                At(actions, row: 2)
            }
        };
        var border = new Border { Child = content };
        border.Classes.Add("theme-dialog");
        border.CornerRadius = new CornerRadius(0);
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        Content = border;
    }

    private static Border Metric(string label, string path)
    {
        var value = Bound(path, FontWeight.SemiBold);
        value.FontSize = 16;
        var border = new Border
        {
            Padding = new Thickness(14),
            Child = new StackPanel { Spacing = 5, Children = { Label(label), value } }
        };
        border.Classes.Add("theme-card");
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary"));
        return border;
    }

    private static StackPanel Field(string label, Control control) => new() { Spacing = 5, Children = { Label(label), control } };
    private static TextBlock Label(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("form-label"); return value; }
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static TextBlock Bound(string path, FontWeight? weight = null, string? resource = null) { var value = new TextBlock { FontWeight = weight ?? FontWeight.Normal }; value.Bind(TextBlock.TextProperty, new Binding(path)); if (resource is not null) value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(resource)); return value; }
    private static T Bind<T>(T target, AvaloniaProperty property, string path) where T : AvaloniaObject { target.Bind(property, new Binding(path)); return target; }
    private static T At<T>(T control, int row = 0, int column = 0) where T : Control { Grid.SetRow(control, row); Grid.SetColumn(control, column); return control; }
}
