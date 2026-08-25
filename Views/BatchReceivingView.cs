using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class BatchReceivingView : UserControl
{
    public BatchReceivingView()
    {
        var content = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(0, 0, 18, 0),
            Children =
            {
                Header(),
                Status(),
                CaptureCard(),
                IssueCard(),
                PreviewCard(),
                ResultCard()
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
        var title = new TextBlock { Text = "Batch Receive" };
        title.Classes.Add("h1");
        var review = Button("Review Batch", "ReviewBatchCommand", true);
        Bind(review, Avalonia.Controls.Button.IsEnabledProperty, "CanReview");
        var receive = Button("Receive into Bodega", "CommitBatchCommand", true);
        Bind(receive, Avalonia.Controls.Button.IsEnabledProperty, "CanCommit");
        var clear = Button("Clear", "ClearDraftCommand");
        Bind(clear, Avalonia.Controls.Button.IsEnabledProperty, "CanEdit");
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
                        Muted("Capture an Eyoyo keyboard export, validate every unit, then receive the unchanged batch into Bodega only.")
                    }
                },
                At(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { clear, review, receive }
                }, column: 1)
            }
        };
    }

    private static Border Status()
    {
        var text = BoundText("StatusMessage");
        text.TextWrapping = TextWrapping.Wrap;
        return Resource(new Border
        {
            Padding = new Thickness(14, 10),
            CornerRadius = new CornerRadius(7),
            Child = text
        }, Border.BackgroundProperty, "Secondary");
    }

    private static Border CaptureCard()
    {
        var capture = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 170,
            MaxHeight = 300,
            VerticalContentAlignment = VerticalAlignment.Top,
            PlaceholderText = "Supplier library<Tab>Barcode<Tab>Quantity<Enter>"
        };
        capture.Classes.Add("form-input");
        capture.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        capture.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        Bind(capture, InputElement.IsEnabledProperty, "CanEdit");
        capture.Bind(TextBox.TextProperty, new Binding("CaptureText")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });

        var reference = Input("Reference", "Delivery receipt or invoice (optional)", 100);
        var notes = Input("Notes", "Shared batch notes (optional)", 500);
        var details = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*"),
            ColumnSpacing = 12,
            Children =
            {
                Field("REFERENCE", reference),
                At(Field("NOTES", notes), column: 1)
            }
        };

        return Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        Heading("Scanner capture"),
                        Muted("Click inside this field before starting Eyoyo Keyboard Export. Tabs and Enter are captured as data; supplier names may contain spaces.")
                    }
                },
                capture,
                Muted("Expected columns: supplier library, exact barcode text, and positive whole-number quantity. Scientific notation is rejected."),
                details
            }
        }, new Thickness(20));
    }

    private static Border IssueCard()
    {
        var items = new ItemsControl();
        Bind(items, ItemsControl.ItemsSourceProperty, "Issues");
        items.ItemTemplate = new FuncDataTemplate<BatchReceivingDisplayIssue>((_, _) => IssueRow(), true);
        var content = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new StackPanel { Spacing = 3, Children = { Heading("Review findings"), Muted("Warnings are advisory and may be accepted; errors must be resolved before the batch can be committed.") } },
                new ScrollViewer { Content = items, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
            }
        };
        var card = Card(content, new Thickness(20));
        Bind(card, Visual.IsVisibleProperty, "HasIssues");
        return card;
    }

    private static Control IssueRow()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("0.7*,0.8*,0.8*,1*,3*"),
            ColumnSpacing = 12,
            Children =
            {
                Cell("Severity", true),
                At(Cell("Location", true), column: 1),
                At(Cell("Field", true), column: 2),
                At(Cell("Code", true), column: 3),
                At(Cell("Message", true), column: 4)
            }
        };
        return Resource(new Border
        {
            Padding = new Thickness(4, 8),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid
        }, Border.BorderBrushProperty, "Border");
    }

    private static Border PreviewCard()
    {
        var table = new PagedTable
        {
            Height = 480,
            PageSize = 10,
            MinTableWidth = 1180,
            ItemName = "preview line",
            ItemNamePlural = "preview lines"
        };
        Bind(table, PagedTable.ItemsSourceProperty, "PreviewRows");
        Bind(table, PagedTable.IsLoadingProperty, "IsBusy");
        Bind(table, PagedTable.ErrorMessageProperty, "PreviewError");
        table.Columns.Add(Column("Product", row => row.ProductNameDisplay, 1.5));
        table.Columns.Add(Column("Barcode", row => row.Barcode, 1.15));
        table.Columns.Add(Column("Library -> Supplier", row => row.SupplierResolutionDisplay, 1.5));
        table.Columns.Add(Column("Scanned qty", row => row.ScannedQuantityDisplay, 1.05, HorizontalAlignment.Right));
        table.Columns.Add(Column("Receive pieces", row => row.BasePieceQuantityDisplay, 0.9, HorizontalAlignment.Right));
        table.Columns.Add(Column("Bodega before -> after", row => row.BodegaChangeDisplay, 1.15, HorizontalAlignment.Right));
        table.Columns.Add(StatusColumn());

        return Card(new StackPanel
        {
            Children =
            {
                new StackPanel
                {
                    Margin = new Thickness(20, 18, 20, 12),
                    Spacing = 3,
                    Children =
                    {
                        Heading("Validated preview"),
                        Muted("Library is scanner input; Supplier is the barcode's registered supplier and is authoritative."),
                        Muted(path: "ValidationSummary")
                    }
                },
                table
            }
        }, new Thickness(0), clip: true);
    }

    private static PagedTableColumn StatusColumn() => new()
    {
        Header = "Status",
        Width = new GridLength(0.8, GridUnitType.Star),
        IsSortable = true,
        ValueSelector = item => ((BatchReceiptPreviewRowResponse)item).Status,
        SortValueSelector = item => ((BatchReceiptPreviewRowResponse)item).Status,
        CellTemplate = new FuncDataTemplate<BatchReceiptPreviewRowResponse>((_, _) =>
        {
            var badge = new StatusBadge();
            badge.Bind(StatusBadge.StatusProperty, new Binding("Status"));
            return badge;
        }, true)
    };

    private static Border ResultCard()
    {
        var summary = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,0.7*,0.7*,0.8*,1.3*,1*"),
            ColumnSpacing = 12,
            Children =
            {
                ResultValue("REFERENCE", "Result.ReferenceDisplay"),
                At(ResultValue("RECORDS", "Result.AcceptedRecordCount"), column: 1),
                At(ResultValue("PRODUCTS", "Result.AffectedProductCount"), column: 2),
                At(ResultValue("BASE PIECES", "Result.TotalBasePieces"), column: 3),
                At(ResultValue("SUPPLIERS", "Result.SuppliersDisplay"), column: 4),
                At(ResultValue("COMPLETED", "Result.CompletedAtDisplay"), column: 5)
            }
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new StackPanel { Spacing = 3, Children = { Heading("Receipt completed"), Muted("The scanner capture may now be cleared from the Eyoyo library.") } },
                summary
            }
        };
        var card = Card(content, new Thickness(20));
        Bind(card, Visual.IsVisibleProperty, "HasResult");
        return card;
    }

    private static StackPanel ResultValue(string label, string path)
    {
        var value = BoundText(path);
        value.FontSize = 16;
        value.FontWeight = FontWeight.SemiBold;
        value.TextWrapping = TextWrapping.Wrap;
        return new StackPanel { Spacing = 4, Children = { Label(label), value } };
    }

    private static PagedTableColumn Column<T>(
        string header,
        Func<BatchReceiptPreviewRowResponse, T> selector,
        double width,
        HorizontalAlignment alignment = HorizontalAlignment.Stretch)
    {
        var column = PagedTableColumn.Create(header, selector, new GridLength(width, GridUnitType.Star));
        column.HorizontalAlignment = alignment;
        return column;
    }

    private static TextBox Input(string path, string placeholder, int maxLength)
    {
        var input = new TextBox { PlaceholderText = placeholder, MaxLength = maxLength };
        input.Classes.Add("form-input");
        Bind(input, InputElement.IsEnabledProperty, "CanEdit");
        input.Bind(TextBox.TextProperty, new Binding(path)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        return input;
    }

    private static StackPanel Field(string label, Control control) => new() { Spacing = 5, Children = { Label(label), control } };
    private static TextBlock Label(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("form-label"); return value; }
    private static TextBlock Heading(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("h3"); return value; }
    private static TextBlock Cell(string path, bool wrap = false) { var value = BoundText(path); value.VerticalAlignment = VerticalAlignment.Center; value.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap; return value; }
    private static TextBlock Muted(string? text = null, string? path = null)
    {
        var value = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        if (path is not null) Bind(value, TextBlock.TextProperty, path);
        return Resource(value, TextBlock.ForegroundProperty, "MutedForeground");
    }
    private static TextBlock BoundText(string path)
    {
        var value = new TextBlock();
        value.Bind(TextBlock.TextProperty, new Binding(path));
        return value;
    }
    private static Button Button(string text, string command, bool primary = false)
    {
        var value = new ActionButton(text, primary ? ActionButtonVariant.Primary : ActionButtonVariant.Secondary);
        value.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(command));
        return value;
    }
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
