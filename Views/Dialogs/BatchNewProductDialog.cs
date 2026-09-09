using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class BatchNewProductDialog : Window
{
    private readonly Guid _correlationId;

    public BatchNewProductDialog(BatchNewProductViewModel viewModel, Guid correlationId)
    {
        _correlationId = correlationId;
        Title = "Add scanned product - BPNV Convenience Store";
        Width = 980;
        Height = 760;
        MinWidth = 820;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        DataContext = viewModel;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var title = new TextBlock { Text = "Add scanned product" };
        title.Classes.Add("h2");
        var body = new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(0, 0, 14, 0),
            Children =
            {
                Section("Product details", ProductFields()),
                Section("Reorder rules", ReorderFields()),
                Section("Package units", PackageFields())
            }
        };

        var validation = BoundText(nameof(BatchNewProductViewModel.ValidationMessage));
        validation.FontSize = 12;
        validation.TextWrapping = TextWrapping.Wrap;
        validation.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Destructive"));
        var cancel = new ActionButton("Cancel", ActionButtonVariant.Secondary);
        cancel.Click += (_, _) => Close((BatchReceiptNewProductRequest?)null);
        var add = new ActionButton("Add to batch", ActionButtonVariant.Primary) { IsDefault = true };
        add.Click += (_, _) =>
        {
            if (DataContext is BatchNewProductViewModel model && model.TryBuildRequest(_correlationId, out var request, out _))
                Close(request);
        };
        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 10,
            Children = { validation, At(cancel, column: 1), At(add, column: 2) }
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
                        Muted("Complete the catalog details. The product and its stock will be created together when the batch is committed.")
                    }
                },
                At(new ScrollViewer
                {
                    Content = new Border { Padding = new Thickness(8, 8, 8, 32), Child = body },
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                }, row: 1),
                At(actions, row: 2)
            }
        };
        var border = new Border { Child = content };
        border.Classes.Add("theme-dialog");
        border.CornerRadius = new CornerRadius(0);
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        Content = border;
    }

    private static Control ProductFields()
    {
        var supplier = new SearchableSelect
        {
            PlaceholderText = "Select an active supplier",
            SearchTextSelector = item => item is SupplierResponse value ? value.Name : item.ToString() ?? ""
        };
        Bind(supplier, SearchableSelect.ItemsSourceProperty, nameof(BatchNewProductViewModel.Suppliers));
        Bind(supplier, SearchableSelect.SelectedItemProperty, nameof(BatchNewProductViewModel.SelectedSupplier));
        supplier.ItemTemplate = new FuncDataTemplate<SupplierResponse>((_, _) => BoundText(nameof(SupplierResponse.Name)), true);

        var itemType = new SearchableSelect { PlaceholderText = "Select item type" };
        Bind(itemType, SearchableSelect.ItemsSourceProperty, nameof(BatchNewProductViewModel.ItemTypes));
        Bind(itemType, SearchableSelect.SelectedItemProperty, nameof(BatchNewProductViewModel.ItemType));
        var category = new SearchableSelect { PlaceholderText = "Select or type a category", AllowCustomValue = true };
        Bind(category, SearchableSelect.ItemsSourceProperty, nameof(BatchNewProductViewModel.Categories));
        Bind(category, SearchableSelect.SelectedItemProperty, nameof(BatchNewProductViewModel.Category));
        var barcode = new TextBox { PlaceholderText = "Scanned barcode", IsReadOnly = true };
        barcode.Classes.Add("form-input");
        barcode.Bind(TextBox.TextProperty, new Binding(nameof(BatchNewProductViewModel.PieceBarcode)) { Mode = BindingMode.OneWay });

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 12,
            Children =
            {
                Field("SUPPLIER", supplier),
                At(Field("ITEM TYPE", itemType), column: 1),
                At(Field("SKU", Input(nameof(BatchNewProductViewModel.Sku), "Required")), column: 2),
                At(Field("PRODUCT NAME", Input(nameof(BatchNewProductViewModel.Name), "Required")), row: 1),
                At(Field("CATEGORY", category), row: 1, column: 1),
                At(Field("BASE UNIT LABEL", Input(nameof(BatchNewProductViewModel.Unit), "piece")), row: 1, column: 2),
                At(Field("SCANNED PIECE BARCODE", barcode), row: 2),
                At(Field("PURCHASE PRICE / PIECE", Amount(nameof(BatchNewProductViewModel.CostPrice))), row: 2, column: 1),
                At(Field("SELLING PRICE", Amount(nameof(BatchNewProductViewModel.RegularPrice))), row: 2, column: 2),
                At(Field("EMPLOYEE PRICE (0 = SELLING)", Amount(nameof(BatchNewProductViewModel.EmployeePrice))), row: 3)
            }
        };
    }

    private static Control ReorderFields() => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
        ColumnSpacing = 12,
        Children =
        {
            Field("CRITICAL LEVEL", Number(nameof(BatchNewProductViewModel.CriticalReorderLevel), 0)),
            At(Field("ORDER QTY AT CRITICAL", Number(nameof(BatchNewProductViewModel.CriticalOrderQuantity), 1)), column: 1),
            At(Field("WARNING LEVEL", Number(nameof(BatchNewProductViewModel.WarningReorderLevel), 0)), column: 2),
            At(Field("ORDER QTY AT WARNING", Number(nameof(BatchNewProductViewModel.WarningOrderQuantity), 1)), column: 3)
        }
    };

    private static Control PackageFields()
    {
        var add = new ActionButton("Add package", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        Bind(add, Button.CommandProperty, nameof(BatchNewProductViewModel.AddPackageCommand));
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { Muted("Optional package barcodes and selling prices."), At(add, column: 1) }
        };
        var packages = new ItemsControl();
        Bind(packages, ItemsControl.ItemsSourceProperty, nameof(BatchNewProductViewModel.Packages));
        packages.ItemTemplate = new FuncDataTemplate<ProductPackageDraft>((_, _) => PackageRow(), true);
        return new StackPanel { Spacing = 10, Children = { header, packages } };
    }

    private static Control PackageRow()
    {
        var remove = new ActionButton("Remove", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        remove.Bind(Button.CommandProperty, new Binding("DataContext.RemovePackageCommand")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(BatchNewProductDialog) }
        });
        remove.Bind(Button.CommandParameterProperty, new Binding());
        remove.VerticalAlignment = VerticalAlignment.Bottom;
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,1*,0.65*,0.85*,0.85*,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                Field("BARCODE", Input(nameof(ProductPackageDraft.Barcode), "Package barcode")),
                At(Field("LABEL", Input(nameof(ProductPackageDraft.Label), "e.g. Case")), column: 1),
                At(Field("PIECES", Number(nameof(ProductPackageDraft.PiecesPerUnit), 2)), column: 2),
                At(Field("SELLING", Amount(nameof(ProductPackageDraft.RegularPrice))), column: 3),
                At(Field("EMPLOYEE", Amount(nameof(ProductPackageDraft.EmployeePrice))), column: 4),
                At(remove, column: 5)
            }
        };
        var border = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12), Child = row };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        return border;
    }

    private static Border Section(string heading, Control body)
    {
        var title = new TextBlock { Text = heading };
        title.Classes.Add("h3");
        var section = new Border { Padding = new Thickness(18), Child = new StackPanel { Spacing = 14, Children = { title, body } } };
        section.Classes.Add("theme-card");
        section.CornerRadius = new CornerRadius(12);
        section.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        section.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        section.BorderThickness = new Thickness(1);
        return section;
    }

    private static StackPanel Field(string label, Control control) => new() { Spacing = 5, Children = { Label(label), control } };
    private static TextBlock Label(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("form-label"); return value; }
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static TextBlock BoundText(string path) { var value = new TextBlock(); Bind(value, TextBlock.TextProperty, path); return value; }
    private static TextBox Input(string path, string placeholder) { var value = new TextBox { PlaceholderText = placeholder }; value.Classes.Add("form-input"); Bind(value, TextBox.TextProperty, path); return value; }
    private static AmountInput Amount(string path) { var value = new AmountInput { MinHeight = 42 }; Bind(value, AmountInput.ValueProperty, path); return value; }
    private static NumberField Number(string path, decimal minimum) { var value = new NumberField { Minimum = minimum, FormatString = "0", Increment = 1 }; Bind(value, NumberField.ValueProperty, path); return value; }
    private static T Bind<T>(T target, AvaloniaProperty property, string path) where T : AvaloniaObject { target.Bind(property, new Binding(path) { Mode = BindingMode.TwoWay }); return target; }
    private static T At<T>(T control, int row = 0, int column = 0) where T : Control { Grid.SetRow(control, row); Grid.SetColumn(control, column); return control; }
}
