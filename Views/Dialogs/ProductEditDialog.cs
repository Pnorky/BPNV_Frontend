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

public sealed class ProductEditDialog : Window
{
    public ProductEditDialog(ProductEditViewModel viewModel)
    {
        Title = $"Edit {viewModel.Name} - BPNV Convenience Store";
        Width = 980;
        Height = 760;
        MinWidth = 820;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        DataContext = viewModel;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var title = new TextBlock { Text = "Edit product" };
        title.Classes.Add("h2");
        var subtitle = Muted("Update catalog details, reorder rules, prices, and scannable package units.");

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

        var validation = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        Bind(validation, TextBlock.TextProperty, nameof(ProductEditViewModel.ValidationMessage));
        validation.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Destructive"));

        var cancel = new ActionButton("Cancel", ActionButtonVariant.Secondary);
        cancel.Click += (_, _) => Close((UpdateProductRequest?)null);
        var save = new ActionButton("Save changes", ActionButtonVariant.Primary) { IsDefault = true };
        save.Click += (_, _) =>
        {
            if (DataContext is ProductEditViewModel model && model.TryBuildRequest(out var request, out _))
                Close(request);
        };
        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 10,
            Children = { validation, At(cancel, column: 1), At(save, column: 2) }
        };
        Grid.SetRow(actions, 2);

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 18,
            Margin = new Thickness(26),
            Children =
            {
                new StackPanel { Spacing = 5, Children = { title, subtitle } },
                At(new ScrollViewer
                {
                    Content = new Border { Padding = new Thickness(8, 8, 8, 32), Child = body },
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                }, row: 1),
                actions
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
        Bind(supplier, SearchableSelect.ItemsSourceProperty, nameof(ProductEditViewModel.Suppliers));
        Bind(supplier, SearchableSelect.SelectedItemProperty, nameof(ProductEditViewModel.SelectedSupplier));
        supplier.ItemTemplate = new FuncDataTemplate<SupplierResponse>((_, _) => BoundText(nameof(SupplierResponse.Name)), true);

        var itemType = new SearchableSelect { PlaceholderText = "Select item type" };
        Bind(itemType, SearchableSelect.ItemsSourceProperty, nameof(ProductEditViewModel.ItemTypes));
        Bind(itemType, SearchableSelect.SelectedItemProperty, nameof(ProductEditViewModel.ItemType));

        var category = new SearchableSelect { PlaceholderText = "Select or type a category", AllowCustomValue = true };
        Bind(category, SearchableSelect.ItemsSourceProperty, nameof(ProductEditViewModel.Categories));
        Bind(category, SearchableSelect.SelectedItemProperty, nameof(ProductEditViewModel.Category));

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
                At(Field("SKU", Input(nameof(ProductEditViewModel.Sku), "Required")), column: 2),
                At(Field("PRODUCT NAME", Input(nameof(ProductEditViewModel.Name), "Required")), row: 1),
                At(Field("CATEGORY", category), row: 1, column: 1),
                At(Field("BASE UNIT LABEL", Input(nameof(ProductEditViewModel.Unit), "piece")), row: 1, column: 2),
                At(Field("PIECE BARCODE", Input(nameof(ProductEditViewModel.PieceBarcode), "Leading zeros are preserved")), row: 2),
                At(Field("PURCHASE PRICE / PIECE", Amount(nameof(ProductEditViewModel.CostPrice))), row: 2, column: 1),
                At(Field("SELLING PRICE", Amount(nameof(ProductEditViewModel.RegularPrice))), row: 2, column: 2),
                At(Field("EMPLOYEE PRICE (0 = SELLING)", Amount(nameof(ProductEditViewModel.EmployeePrice))), row: 3)
            }
        };
    }

    private static Control ReorderFields() => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
        ColumnSpacing = 12,
        Children =
        {
            Field("CRITICAL LEVEL", Number(nameof(ProductEditViewModel.CriticalReorderLevel), 0)),
            At(Field("ORDER QTY AT CRITICAL", Number(nameof(ProductEditViewModel.CriticalOrderQuantity), 1)), column: 1),
            At(Field("WARNING LEVEL", Number(nameof(ProductEditViewModel.WarningReorderLevel), 0)), column: 2),
            At(Field("ORDER QTY AT WARNING", Number(nameof(ProductEditViewModel.WarningOrderQuantity), 1)), column: 3)
        }
    };

    private static Control PackageFields()
    {
        var add = new ActionButton("Add package", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        Bind(add, Button.CommandProperty, nameof(ProductEditViewModel.AddPackageCommand));
        Grid.SetColumn(add, 1);
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { Muted("Existing package IDs are preserved. Deactivate a unit without deleting its history."), add }
        };
        var packages = new ItemsControl();
        Bind(packages, ItemsControl.ItemsSourceProperty, nameof(ProductEditViewModel.Packages));
        packages.ItemTemplate = new FuncDataTemplate<ProductEditPackageDraft>((_, _) => PackageRow(), true);
        return new StackPanel { Spacing = 10, Children = { header, packages } };
    }

    private static Control PackageRow()
    {
        var active = new SelectionCheckbox("Active");
        active.Bind(ToggleButton.IsCheckedProperty,
            new Binding(nameof(ProductEditPackageDraft.IsActive)) { Mode = BindingMode.TwoWay });
        var remove = new ActionButton("Remove", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        remove.Bind(Button.CommandProperty, new Binding("DataContext.RemovePackageCommand")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ProductEditDialog) }
        });
        remove.Bind(Button.CommandParameterProperty, new Binding());
        remove.Bind(Visual.IsVisibleProperty, new Binding(nameof(ProductEditPackageDraft.CanRemove)));
        remove.VerticalAlignment = VerticalAlignment.Bottom;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,1*,0.65*,0.85*,0.85*,Auto,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                Field("BARCODE", Input(nameof(ProductEditPackageDraft.Barcode), "Package barcode")),
                At(Field("LABEL", Input(nameof(ProductEditPackageDraft.Label), "e.g. Case")), column: 1),
                At(Field("PIECES", Number(nameof(ProductEditPackageDraft.PiecesPerUnit), 2)), column: 2),
                At(Field("SELLING", Amount(nameof(ProductEditPackageDraft.RegularPrice))), column: 3),
                At(Field("EMPLOYEE", Amount(nameof(ProductEditPackageDraft.EmployeePrice))), column: 4),
                At(active, column: 5),
                At(remove, column: 6)
            }
        };
        active.VerticalAlignment = VerticalAlignment.Bottom;
        var border = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12), Child = row };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        return border;
    }

    private static Border Section(string heading, Control body)
    {
        var title = new TextBlock { Text = heading };
        title.Classes.Add("h3");
        var section = new Border
        {
            Padding = new Thickness(18),
            Child = new StackPanel { Spacing = 14, Children = { title, body } }
        };
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
