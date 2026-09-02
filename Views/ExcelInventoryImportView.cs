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

namespace AvaloniaApp.Views;

public sealed class ExcelInventoryImportView : UserControl
{
    public ExcelInventoryImportView()
    {
        var content = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(0, 0, 18, 0),
            Children =
            {
                Header(),
                Status(),
                SummaryCards(),
                ExcludedSectionsCard(),
                Card(SectionMappings(), new Thickness(20)),
                Card(BulkDefaults(), new Thickness(20)),
                Card(IssueArea(), new Thickness(20)),
                Card(ProductPreview(), new Thickness(0), true)
            }
        };
        Content = new ScrollViewer
        {
            Content = content,
            Margin = new Thickness(30, 30, 12, 30),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static Control Header()
    {
        var title = new TextBlock { Text = "Import inventory from Excel" };
        title.Classes.Add("h1");
        var open = Button("Open .xlsx", "OpenWorkbookCommand", true);
        var blank = Button("Blank template", "ExportBlankTemplateCommand");
        var prefilled = Button("Prefilled template", "ExportPrefilledTemplateCommand");
        Bind(prefilled, Visual.IsVisibleProperty, "IsLoaded");
        var validate = Button("Validate", "ValidateCommand");
        Bind(validate, Visual.IsVisibleProperty, "IsLoaded");
        Bind(validate, Avalonia.Controls.Button.IsEnabledProperty, "CanValidate");
        var import = Button("Import", "ImportCommand", true);
        Bind(import, Visual.IsVisibleProperty, "IsLoaded");
        Bind(import, Avalonia.Controls.Button.IsEnabledProperty, "CanImport");
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            Children =
            {
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        title,
                        Muted("Review and complete imported rows before any inventory is written to the database.")
                    }
                },
                At(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { blank, prefilled, open, validate, import }
                }, column: 1)
            }
        };
    }

    private static Border Status()
    {
        var status = BoundText("StatusMessage");
        status.TextWrapping = TextWrapping.Wrap;
        return Resource(new Border
        {
            Padding = new Thickness(14, 10),
            CornerRadius = new CornerRadius(7),
            Child = status
        }, Border.BackgroundProperty, "Secondary");
    }

    private static Control SummaryCards()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("1.4*,0.8*,0.6*,0.6*,0.6*,0.7*"), ColumnSpacing = 12 };
        grid.Children.Add(SummaryCard("FILE", "FileName", "FormatDisplay"));
        grid.Children.Add(At(SummaryCard("FORMAT", "FormatDisplay", "SourceHash", true), column: 1));
        grid.Children.Add(At(SummaryCard("PRODUCTS", "ProductCount"), column: 2));
        grid.Children.Add(At(SummaryCard("PACKAGES", "PackageCount"), column: 3));
        grid.Children.Add(At(SummaryCard("ISSUES", "IssueCount"), column: 4));
        grid.Children.Add(At(SummaryCard("EXCLUDED", "ExcludedProductCount", "ExcludedSectionCount", false, "{0} sections"), column: 5));
        return grid;
    }

    private static Border SummaryCard(string label, string valuePath, string? detailPath = null, bool trimDetail = false, string? detailFormat = null)
    {
        var value = BoundText(valuePath);
        value.FontSize = 18;
        value.FontWeight = FontWeight.SemiBold;
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        var stack = new StackPanel { Spacing = 4, Children = { Label(label), value } };
        if (detailPath is not null)
        {
            var detail = BoundText(detailPath, detailFormat);
            detail.FontSize = 10;
            detail.TextTrimming = trimDetail ? TextTrimming.CharacterEllipsis : TextTrimming.None;
            Resource(detail, TextBlock.ForegroundProperty, "MutedForeground");
            stack.Children.Add(detail);
        }
        return Card(stack, new Thickness(16));
    }

    private static Border ExcludedSectionsCard()
    {
        var sections = new ItemsControl();
        Bind(sections, ItemsControl.ItemsSourceProperty, "ExcludedSections");
        sections.ItemTemplate = new FuncDataTemplate<ExcelInventoryExcludedSectionDraft>((_, _) =>
            RowBorder(new StackPanel { Spacing = 2, Children = { SemiBold("SummaryDisplay"), Muted(path: "SourceSheet") } }));
        var content = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Heading("Excluded workbook sections"),
                Muted("These configured sections and all product rows under them will not be imported."),
                sections
            }
        };
        var card = Card(content, new Thickness(20));
        Bind(card, Visual.IsVisibleProperty, "HasExcludedSections");
        return card;
    }

    private static Control SectionMappings()
    {
        var applyAll = Button("Apply all sections", "ApplyAllSectionsCommand", true);
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        Heading("Legacy section mappings"),
                        Muted("Map each heading to a supplier, category, and item type, then apply it to products in that section.")
                    }
                },
                At(applyAll, column: 1)
            }
        };
        var sections = new ItemsControl();
        Bind(sections, ItemsControl.ItemsSourceProperty, "Sections");
        sections.ItemTemplate = new FuncDataTemplate<InventoryImportSectionMapping>((_, _) => SectionRow(), true);
        var content = new StackPanel { Spacing = 12, Children = { header, sections } };
        Bind(content, Visual.IsVisibleProperty, "IsLoaded");
        return content;
    }

    private static Control SectionRow()
    {
        var type = Select("ItemType", "DataContext.ItemTypes", typeof(ExcelInventoryImportView));
        var apply = Button("Apply", "DataContext.ApplySectionCommand", ancestor: typeof(ExcelInventoryImportView));
        apply.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());
        apply.VerticalAlignment = VerticalAlignment.Bottom;
        return RowBorder(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,1.2*,1.1*,0.9*,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                Field("SECTION", new StackPanel { Children = { SemiBold("Heading"), Muted(path: "SourceDisplay") } }),
                At(Field("SUPPLIER", Input("SupplierName", "Required")), column: 1),
                At(Field("CATEGORY", Input("Category", "Required")), column: 2),
                At(Field("ITEM TYPE", type), column: 3),
                At(apply, column: 4)
            }
        });
    }

    private static Control BulkDefaults()
    {
        var type = Select("DefaultItemType", "ItemTypes");
        var first = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,1*,0.7*,0.9*,0.8*,0.8*,0.8*"),
            ColumnSpacing = 10,
            Children =
            {
                Field("DEFAULT SUPPLIER", Input("DefaultSupplierName", "Used only when blank")),
                At(Field("CATEGORY", Input("DefaultCategory", "General")), column: 1),
                At(Field("UNIT", Input("DefaultUnit", "piece")), column: 2),
                At(Field("ITEM TYPE", type), column: 3),
                At(Field("COST", Number("DefaultCostPrice", "0.00")), column: 4),
                At(Field("SELLING", Number("DefaultRegularPrice", "0.00")), column: 5),
                At(Field("EMPLOYEE", Number("DefaultEmployeePrice", "0.00")), column: 6)
            }
        };
        var apply = Button("Fill missing fields", "ApplyDefaultsCommand", true);
        apply.VerticalAlignment = VerticalAlignment.Bottom;
        var second = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                Field("CRITICAL LEVEL", Number("DefaultCriticalLevel", "0")),
                At(Field("CRITICAL ORDER QTY", Number("DefaultCriticalOrderQuantity", "0", 1)), column: 1),
                At(Field("WARNING LEVEL", Number("DefaultWarningLevel", "0", 1)), column: 2),
                At(Field("WARNING ORDER QTY", Number("DefaultWarningOrderQuantity", "0", 1)), column: 3),
                At(apply, column: 4)
            }
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Heading("Bulk defaults and completion"),
                Muted("Only blank values are filled. Missing SKUs are generated from the source row and product name; barcodes remain optional for import."),
                first,
                second
            }
        };
        Bind(content, Visual.IsVisibleProperty, "IsLoaded");
        return content;
    }

    private static Control IssueArea()
    {
        var recheck = Button("Recheck edits", "RecheckLocalCommand");
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel { Spacing = 3, Children = { Heading("Validation and issues"), Muted(path: "ValidationSummary") } },
                At(recheck, column: 1)
            }
        };
        var issues = new ItemsControl();
        Bind(issues, ItemsControl.ItemsSourceProperty, "Issues");
        issues.ItemTemplate = new FuncDataTemplate<InventoryImportDisplayIssue>((_, _) => IssueRow(), true);
        var scroll = new ScrollViewer { MaxHeight = 220, Content = issues };
        var content = new StackPanel { Spacing = 10, Children = { header, scroll } };
        Bind(content, Visual.IsVisibleProperty, "IsLoaded");
        return content;
    }

    private static Control IssueRow() => RowBorder(new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("0.5*,0.8*,0.9*,3*"),
        ColumnSpacing = 12,
        Children =
        {
            SemiBold("Severity"),
            At(Cell("Location"), column: 1),
            At(Cell("Field"), column: 2),
            At(Cell("Message", wrap: true), column: 3)
        }
    }, new Thickness(4, 8));

    private static Control ProductPreview()
    {
        var products = new ItemsControl();
        Bind(products, ItemsControl.ItemsSourceProperty, "Products");
        products.ItemTemplate = new FuncDataTemplate<ExcelInventoryProductDraft>((_, _) => ProductRow(), true);
        var body = new ScrollViewer
        {
            Content = products,
            MaxHeight = 420,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var table = new ScrollViewer
        {
            Content = new StackPanel { Children = { ProductHeader(), body } },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var content = new StackPanel
        {
            Children =
            {
                new StackPanel
                {
                    Margin = new Thickness(20, 18, 20, 12),
                    Spacing = 3,
                    Children =
                    {
                        Heading("Product preview"),
                        Muted("Review and edit product identity, barcode, prices, stock, and reorder settings before validation.")
                    }
                },
                table
            }
        };
        Bind(content, Visual.IsVisibleProperty, "IsLoaded");
        return content;
    }

    private static Control ProductHeader() => Resource(new Border
    {
        Padding = new Thickness(12, 9),
        Child = HeaderGrid("ROW", "SUPPLIER", "SKU", "BARCODE", "PRODUCT", "CATEGORY", "UNIT", "TYPE", "COST", "SELLING", "EMPLOYEE", "DISPLAY", "BODEGA", "CRITICAL", "CRIT. QTY", "WARNING", "WARN. QTY")
    }, Border.BackgroundProperty, "Secondary");

    private static Control ProductRow()
    {
        var row = BoundText("SourceRow");
        row.VerticalAlignment = VerticalAlignment.Center;
        var type = Select("ItemType", "DataContext.ItemTypes", typeof(ExcelInventoryImportView));
        type.MinWidth = 130;
        return RowBorder(new Grid
        {
            Width = 2200,
            ColumnDefinitions = PreviewColumns(),
            ColumnSpacing = 8,
            Children =
            {
                row,
                At(CompactInput("SupplierName"), column: 1),
                At(CompactInput("Sku"), column: 2),
                At(CompactInput("PieceBarcode"), column: 3),
                At(CompactInput("Name"), column: 4),
                At(CompactInput("Category"), column: 5),
                At(CompactInput("Unit"), column: 6),
                At(type, column: 7),
                At(CompactNumber("CostPrice", "0.00"), column: 8),
                At(CompactNumber("RegularPrice", "0.00"), column: 9),
                At(CompactNumber("EmployeePrice", "0.00"), column: 10),
                At(CompactNumber("OpeningDisplayStock", "0"), column: 11),
                At(CompactNumber("OpeningBodegaStock", "0"), column: 12),
                At(CompactNumber("CriticalReorderLevel", "0"), column: 13),
                At(CompactNumber("CriticalOrderQuantity", "0", 1), column: 14),
                At(CompactNumber("WarningReorderLevel", "0"), column: 15),
                At(CompactNumber("WarningOrderQuantity", "0", 1), column: 16)
            }
        }, new Thickness(12, 8));
    }

    private static Grid HeaderGrid(params string[] labels)
    {
        var grid = new Grid { Width = 2200, ColumnDefinitions = PreviewColumns(), ColumnSpacing = 8 };
        for (var index = 0; index < labels.Length; index++) grid.Children.Add(At(Label(labels[index]), column: index));
        return grid;
    }

    private static ColumnDefinitions PreviewColumns() => new("52,170,140,160,220,150,90,140,100,100,100,85,85,95,95,95,95");
    private static TextBox CompactInput(string path)
    {
        var input = Input(path, "");
        input.MinHeight = 34;
        input.Padding = new Thickness(8, 5);
        return input;
    }
    private static NumberField CompactNumber(string path, string format, decimal minimum = 0)
    {
        var input = Number(path, format, minimum);
        input.MinHeight = 34;
        return input;
    }
    private static SearchableSelect Select(string path, string itemsPath, Type? ancestor = null)
    {
        var select = new SearchableSelect { PlaceholderText = "Select item type" };
        select.Bind(SearchableSelect.ItemsSourceProperty, ancestor is null
            ? new Binding(itemsPath)
            : new Binding(itemsPath) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = ancestor } });
        Bind(select, SearchableSelect.SelectedItemProperty, path);
        return select;
    }
    private static TextBox Input(string path, string placeholder)
    {
        var input = new TextBox { PlaceholderText = placeholder };
        input.Classes.Add("form-input");
        Bind(input, TextBox.TextProperty, path);
        return input;
    }
    private static NumberField Number(string path, string format, decimal minimum = 0)
    {
        var value = new NumberField { Minimum = minimum, FormatString = format, Increment = 1 };
        Bind(value, NumberField.ValueProperty, path);
        return value;
    }
    private static StackPanel Field(string label, Control control) => new() { Spacing = 5, Children = { Label(label), control } };
    private static TextBlock Label(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("form-label"); return value; }
    private static TextBlock Heading(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("h3"); return value; }
    private static TextBlock SemiBold(string path) { var value = BoundText(path); value.FontWeight = FontWeight.SemiBold; return value; }
    private static TextBlock Cell(string path, bool wrap = false) { var value = BoundText(path); value.VerticalAlignment = VerticalAlignment.Center; value.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap; return value; }
    private static TextBlock Muted(string? text = null, string? path = null)
    {
        var value = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        if (path is not null) Bind(value, TextBlock.TextProperty, path);
        return Resource(value, TextBlock.ForegroundProperty, "MutedForeground");
    }
    private static TextBlock BoundText(string path, string? format = null)
    {
        var value = new TextBlock();
        value.Bind(TextBlock.TextProperty, new Binding(path) { StringFormat = format });
        return value;
    }
    private static Button Button(string text, string command, bool primary = false, Type? ancestor = null)
    {
        var value = new ActionButton(text, primary ? ActionButtonVariant.Primary : ActionButtonVariant.Secondary);
        value.Bind(Avalonia.Controls.Button.CommandProperty, ancestor is null
            ? new Binding(command)
            : new Binding(command) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = ancestor } });
        return value;
    }
    private static Border RowBorder(Control child, Thickness? padding = null) => Resource(new Border
    {
        Padding = padding ?? new Thickness(0, 12),
        BorderThickness = new Thickness(0, 1, 0, 0),
        Child = child
    }, Border.BorderBrushProperty, "Border");
    private static Border Card(Control child, Thickness padding, bool clip = false)
    {
        var card = new Border { Padding = padding, Child = child, ClipToBounds = clip };
        card.Classes.Add("theme-card");
        return Resource(card, Border.BackgroundProperty, "Card");
    }
    private static T At<T>(T control, int column = 0, int row = 0) where T : Control { Grid.SetColumn(control, column); Grid.SetRow(control, row); return control; }
    private static T Bind<T>(T target, AvaloniaProperty property, string path) where T : AvaloniaObject { target.Bind(property, new Binding(path)); return target; }
    private static T Resource<T>(T target, AvaloniaProperty property, string key) where T : AvaloniaObject { target.Bind(property, new DynamicResourceExtension(key)); return target; }
}
