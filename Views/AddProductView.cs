using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Views;

public class AddProductView : UserControl
{
    public AddProductView()
    {
        var content = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                Header(),
                Card(ProductDetails(), new Thickness(20)),
                Card(PackageSection(), new Thickness(20)),
                Card(SupplierSection(), new Thickness(20))
            }
        };
        Content = new ScrollViewer { Content = content, Margin = new Thickness(30) };
    }

    private static Control Header()
    {
        var title = new TextBlock { Text = "Add product" };
        title.Classes.Add("h1");
        return new StackPanel
        {
            Spacing = 5,
            Children =
            {
                title,
                Muted("Register the base piece and optional scannable packages. Inventory starts at zero."),
                Status()
            }
        };
    }

    private static Control ProductDetails()
    {
        var supplier = new ComboBox { PlaceholderText = "Select supplier" };
        supplier.Classes.Add("form-select");
        Bind(supplier, ItemsControl.ItemsSourceProperty, "Suppliers");
        Bind(supplier, ComboBox.SelectedItemProperty, "SelectedSupplier");
        supplier.ItemTemplate = new FuncDataTemplate<SupplierResponse>((_, _) => BoundText("Name"), true);

        var itemType = new ComboBox();
        itemType.Classes.Add("form-select");
        Bind(itemType, ItemsControl.ItemsSourceProperty, "ItemTypes");
        Bind(itemType, ComboBox.SelectedItemProperty, "ItemType");

        var primary = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.3*,0.8*,1*,1.2*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 12,
            Children =
            {
                Field("SUPPLIER", supplier),
                At(Field("ITEM TYPE", itemType), column: 1),
                At(Field("SKU", InputBox("Sku", "Required")), column: 2),
                At(Field("PIECE BARCODE", InputBox("PieceBarcode", "Leading zeros are preserved")), column: 3),
                At(Field("PRODUCT NAME", InputBox("Name", "Required")), row: 1),
                At(Field("CATEGORY", InputBox("Category", "Required")), column: 1, row: 1),
                At(Field("BASE UNIT LABEL", InputBox("Unit", "piece")), column: 2, row: 1),
                At(Field("COST PER PIECE", Number("CostPrice", "0.00")), column: 3, row: 1),
                At(Field("REGULAR PRICE", Number("RegularPrice", "0.00")), row: 2),
                At(Field("EMPLOYEE PRICE (0 = REGULAR)", Number("EmployeePrice", "0.00")), column: 1, row: 2),
                At(Field("REORDER LEVEL", Number("ReorderLevel", "0")), column: 2, row: 2),
                At(Field("TARGET STOCK", Number("TargetStockLevel", "0", 1)), column: 3, row: 2)
            }
        };

        var save = Button("Create product", "CreateProductCommand", true);
        save.HorizontalAlignment = HorizontalAlignment.Right;
        return new StackPanel { Spacing = 16, Children = { Heading("Required product details"), primary, save } };
    }

    private static Control PackageSection()
    {
        var add = Button("Add package", "AddPackageCommand");
        Grid.SetColumn(add, 1);
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel { Children = { Heading("Package units"), Muted("Suggested package prices start from the current base prices and remain editable.") } },
                add
            }
        };

        var packages = new ItemsControl();
        Bind(packages, ItemsControl.ItemsSourceProperty, "Packages");
        packages.ItemTemplate = new FuncDataTemplate<ProductPackageDraft>((_, _) => PackageRow(), true);
        return new StackPanel { Spacing = 14, Children = { heading, packages } };
    }

    private static Control PackageRow()
    {
        var remove = Button("Remove", "DataContext.RemovePackageCommand", ancestor: typeof(AddProductView));
        remove.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,1*,0.65*,0.8*,0.8*,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                Field("BARCODE", InputBox("Barcode", "Package barcode")),
                At(Field("LABEL", InputBox("Label", "e.g. Case")), column: 1),
                At(Field("PIECES", Number("PiecesPerUnit", "0", 2)), column: 2),
                At(Field("REGULAR", Number("RegularPrice", "0.00")), column: 3),
                At(Field("EMPLOYEE", Number("EmployeePrice", "0.00")), column: 4),
                At(remove, column: 5)
            }
        };
        remove.VerticalAlignment = VerticalAlignment.Bottom;
        return Resource(new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 14),
            Child = row
        }, Border.BorderBrushProperty, "Border");
    }

    private static Control SupplierSection()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.3*,1*,1*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                Field("SUPPLIER NAME", InputBox("SupplierName", "Required")),
                At(Field("CONTACT", InputBox("SupplierContact", "Optional")), column: 1),
                At(Field("PHONE", InputBox("SupplierPhone", "Optional")), column: 2),
                At(Button("Create and select supplier", "CreateSupplierCommand", true), column: 3)
            }
        };
        grid.Children[^1].VerticalAlignment = VerticalAlignment.Bottom;
        return new StackPanel { Spacing = 12, Children = { Heading("Create a supplier without leaving this page"), grid } };
    }

    private static Border Status()
    {
        var host = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(7), Child = BoundText("StatusMessage") };
        return Resource(host, Border.BackgroundProperty, "Secondary");
    }

    private static StackPanel Field(string label, Control control) => new() { Spacing = 5, Children = { Label(label), control } };
    private static TextBlock Label(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("form-label"); return value; }
    private static TextBlock Heading(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("h3"); return value; }
    private static TextBlock Muted(string text) => Resource(new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap }, TextBlock.ForegroundProperty, "MutedForeground");
    private static TextBlock BoundText(string path) { var value = new TextBlock(); Bind(value, TextBlock.TextProperty, path); return value; }
    private static TextBox InputBox(string path, string placeholder) { var value = new TextBox { PlaceholderText = placeholder }; value.Classes.Add("form-input"); Bind(value, TextBox.TextProperty, path); return value; }
    private static NumericUpDown Number(string path, string format, decimal minimum = 0) { var value = new NumericUpDown { Minimum = minimum, FormatString = format, Increment = 1 }; Bind(value, NumericUpDown.ValueProperty, path); return value; }
    private static Button Button(string text, string command, bool primary = false, Type? ancestor = null)
    {
        var value = new Button { Content = text };
        value.Classes.Add(primary ? "primary" : "secondary");
        value.Bind(Avalonia.Controls.Button.CommandProperty, ancestor is null ? new Binding(command) : new Binding(command) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = ancestor } });
        return value;
    }
    private static Border Card(Control child, Thickness padding) { var value = new Border { Padding = padding, Child = child }; value.Classes.Add("theme-card"); return Resource(value, Border.BackgroundProperty, "Card"); }
    private static T At<T>(T control, int column = 0, int row = 0) where T : Control { Grid.SetColumn(control, column); Grid.SetRow(control, row); return control; }
    private static T Bind<T>(T target, AvaloniaProperty property, string path) where T : AvaloniaObject { target.Bind(property, new Binding(path)); return target; }
    private static T Resource<T>(T target, AvaloniaProperty property, string key) where T : AvaloniaObject { target.Bind(property, new DynamicResourceExtension(key)); return target; }
}
