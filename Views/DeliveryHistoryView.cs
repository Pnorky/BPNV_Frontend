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
using AvaloniaApp.Views.Controls;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class DeliveryHistoryView : UserControl
{
    public DeliveryHistoryView()
    {
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel
            {
                Margin = new Thickness(30),
                Spacing = 16,
                Children =
                {
                    DeliveriesCard()
                }
            }
        };
    }

    private static Border DeliveriesCard()
    {
        var rows = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<DeliveryHistoryRowViewModel>((_, _) => DeliveryRow(), true)
        };
        rows.Bind(ItemsControl.ItemsSourceProperty, new Binding("Rows"));
        var state = new TableState();
        var body = new Grid
        {
            Children =
            {
                rows,
                state
            }
        };

        var inlineError = Resource(new Border
        {
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(7),
            Child = Bound("ListError", true)
        }, Border.BackgroundProperty, "Secondary");
        inlineError.Bind(Visual.IsVisibleProperty, new Binding("HasListError"));

        var pager = new TablePager();
        pager.Bind(Visual.IsVisibleProperty, new Binding("HasRows"));
        return Card(new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Margin = new Thickness(20, 18, 20, 12),
                    Spacing = 3,
                    Children =
                    {
                        Heading("Completed deliveries", "h3"),
                        Muted("Delivery timestamps are shown in Philippine store time. Select a row to inspect its immutable receipt lines.")
                    }
                },
                At(inlineError, row: 1),
                At(body, row: 2),
                At(pager, row: 3)
            }
        }, new Thickness(0));
    }

    private static Control DeliveryRow()
    {
        var status = new StatusBadge();
        status.Bind(StatusBadge.StatusProperty, new Binding("Delivery.Status"));
        var action = Bound("DetailsActionLabel");
        action.FontSize = 11;
        action.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Primary"));
        var actionArea = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { status, action }
        };
        var actionField = new StackPanel
        {
            Margin = new Thickness(0, 0, 12, 10),
            Spacing = 5,
            Children = { Label("STATUS / DETAILS"), actionArea }
        };
        var summary = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.15*,1.45*,1*,1.1*,0.7*,1.15*,0.95*"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                FillMetadata("RECEIPT / INVOICE NO.", "Delivery.ReceiptNumberDisplay"),
                FillMetadata("DELIVERED (PH TIME)", "Delivery.DeliveryAtDisplay"),
                FillMetadata("SUPPLIERS", "Delivery.SuppliersDisplay"),
                FillMetadata("PRODUCTS / PIECES", "Delivery.ProductsPiecesDisplay"),
                FillMetadata("TOTAL COST", "Delivery.TotalCostDisplay"),
                FillMetadata("RECEIVED BY", "Delivery.ReceivedByName"),
                actionField
            }
        };
        for (var index = 0; index < summary.Children.Count; index++) Grid.SetColumn(summary.Children[index], index);
        var rowViewport = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = summary
        };
        var toggle = new Button
        {
            Padding = new Thickness(16, 12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = rowViewport
        };
        toggle.Classes.Add("ghost");
        toggle.Bind(Button.CommandProperty, new Binding("DataContext.ToggleDetailsCommand")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(DeliveryHistoryView) }
        });
        toggle.Bind(Button.CommandParameterProperty, new Binding());

        var detail = Resource(new Border
        {
            Padding = new Thickness(20, 18),
            Child = DetailArea()
        }, Border.BackgroundProperty, "Secondary");
        detail.Bind(Visual.IsVisibleProperty, new Binding("IsExpanded"));
        var row = Resource(new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Children = { toggle, detail } }
        }, Border.BorderBrushProperty, "Border");
        row.SizeChanged += (_, args) => summary.Width = Math.Max(1400, args.NewSize.Width - 32);
        return row;
    }

    private static Control DetailArea()
    {
        var loading = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 10,
            Children =
            {
                new ProgressBar { Width = 30, Height = 4, IsIndeterminate = true },
                new TextBlock { Text = "Loading delivery details..." }
            }
        };
        loading.Bind(Visual.IsVisibleProperty, new Binding("IsDetailLoading"));

        var retry = new ActionButton("Retry", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        retry.Bind(Button.CommandProperty, new Binding("DataContext.RetryDetailCommand")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(DeliveryHistoryView) }
        });
        retry.Bind(Button.CommandParameterProperty, new Binding());
        var error = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 10,
            Children = { Bound("DetailError", true), retry }
        };
        error.Bind(Visual.IsVisibleProperty, new Binding("HasDetailError"));

        var lines = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<DeliveryHistoryLineResponse>((_, _) => DetailLine(), true)
        };
        lines.Bind(ItemsControl.ItemsSourceProperty, new Binding("Detail.Lines"));
        var content = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                DetailMetadata(),
                Resource(new Border { Height = 1 }, Border.BackgroundProperty, "Border"),
                new StackPanel { Spacing = 3, Children = { Heading("Received items", "h3"), Muted("Prices, quantities, supplier names, and balances are receipt-time snapshots.") } },
                lines
            }
        };
        content.Bind(Visual.IsVisibleProperty, new Binding("HasDetail"));
        return new Grid { Children = { loading, error, content } };
    }

    private static Control DetailMetadata() => new StackPanel
    {
        Spacing = 12,
        Children =
        {
            DistributedMetadata(
                ("RECEIPT / INVOICE NO.", "Detail.Delivery.ReceiptNumberDisplay"),
                ("DELIVERED (PH TIME)", "Detail.Delivery.DeliveryAtDisplay"),
                ("RECORDED (PH TIME)", "Detail.Delivery.CompletedAtDisplay"),
                ("SUPPLIERS", "Detail.Delivery.SuppliersDisplay"),
                ("RECEIVED BY", "Detail.Delivery.ReceivedByName")),
            DistributedMetadata(
                ("NOTES", "Detail.NotesDisplay"),
                ("INPUT RECORDS", "Detail.Delivery.AcceptedRecordCount"),
                ("PRODUCTS / PIECES", "Detail.Delivery.ProductsPiecesDisplay"),
                ("TOTAL COST", "Detail.Delivery.TotalCostDisplay"))
        }
    };

    private static Control DetailLine()
    {
        var fields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 20,
            RowSpacing = 16
        };
        var firstRow = new[]
        {
            Metadata("PRODUCT / SKU", "ProductDisplay"),
            Metadata("BARCODE / UNIT", "BarcodeUnitDisplay"),
            Metadata("SUPPLIER / SCANNER LIBRARY", "SupplierDisplay"),
            Metadata("RECEIVED", "ReceivedQuantityDisplay"),
            Metadata("UNIT COST", "CostPriceDisplay")
        };
        for (var index = 0; index < firstRow.Length; index++)
        {
            Grid.SetColumn(firstRow[index], index);
            fields.Children.Add(firstRow[index]);
        }
        var selling = At(Metadata("SELLING / EMPLOYEE", "RegularEmployeePriceDisplay"), row: 1);
        Grid.SetColumnSpan(selling, 2);
        var lineTotal = At(Metadata("LINE TOTAL / PRODUCT", "LineTotalProductDisplay"), column: 2, row: 1);
        Grid.SetColumnSpan(lineTotal, 2);
        fields.Children.Add(selling);
        fields.Children.Add(lineTotal);
        fields.Children.Add(At(Metadata("BODEGA BEFORE -> AFTER", "BodegaChangeDisplay"), column: 4, row: 1));
        return Resource(new Border
        {
            Padding = new Thickness(14, 12),
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            Child = fields
        }, Border.BorderBrushProperty, "Border");
    }

    private static Grid DistributedMetadata(params (string Label, string Path)[] fields)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        for (var index = 0; index < fields.Length; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var field = Metadata(fields[index].Label, fields[index].Path);
            Grid.SetColumn(field, index);
            grid.Children.Add(field);
        }
        return grid;
    }

    private static StackPanel Metadata(string label, string path)
    {
        var value = Bound(path, true);
        value.FontWeight = FontWeight.SemiBold;
        return new StackPanel { Spacing = 4, Children = { Label(label), value } };
    }

    private static StackPanel SizedMetadata(string label, string path, double width)
    {
        var value = Metadata(label, path);
        value.Width = width;
        value.Margin = new Thickness(0, 0, 12, 10);
        return value;
    }

    private static StackPanel FillMetadata(string label, string path)
    {
        var text = Bound(path);
        text.FontWeight = FontWeight.SemiBold;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        return new StackPanel
        {
            Margin = new Thickness(0, 0, 12, 10),
            Spacing = 4,
            Children = { Label(label), text }
        };
    }

    private static TextBlock Heading(string text, string style)
    {
        var value = new TextBlock { Text = text };
        value.Classes.Add(style);
        return value;
    }

    private static TextBlock Label(string text)
    {
        var value = new TextBlock { Text = text };
        value.Classes.Add("form-label");
        return value;
    }

    private static TextBlock Muted(string text) => Resource(new TextBlock
    {
        Text = text,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap
    }, TextBlock.ForegroundProperty, "MutedForeground");

    private static TextBlock Cell(string path, bool wrap = false)
    {
        var value = Bound(path, wrap);
        value.VerticalAlignment = VerticalAlignment.Center;
        return value;
    }

    private static TextBlock Bound(string path, bool wrap = false)
    {
        var value = new TextBlock { TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };
        value.Bind(TextBlock.TextProperty, new Binding(path));
        return value;
    }

    private static Border Card(Control child, Thickness padding, bool clip = false)
    {
        var card = new Border { Padding = padding, Child = child, ClipToBounds = clip };
        card.Classes.Add("theme-card");
        return Resource(card, Border.BackgroundProperty, "Card");
    }

    private static T At<T>(T control, int column = 0, int row = 0) where T : Control
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        return control;
    }

    private static T Resource<T>(T target, AvaloniaProperty property, string key) where T : AvaloniaObject
    {
        target.Bind(property, new DynamicResourceExtension(key));
        return target;
    }
}
