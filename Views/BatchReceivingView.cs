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
            // Keep the page viewport bounded so wide tables can scroll internally.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
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

        var reference = Input("Reference", "Delivery receipt or invoice number (optional)", 100);
        var notes = Input("Notes", "Shared batch notes (optional)", 500);
        var details = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*"),
            ColumnSpacing = 12,
            Children =
            {
                Field("RECEIPT / INVOICE NO.", reference),
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
                details,
                DeliveryTimeSection()
            }
        }, new Thickness(20));
    }

    private static Control DeliveryTimeSection()
    {
        var automatic = new ShadcnSwitch();
        automatic.Bind(ToggleButton.IsCheckedProperty, new Binding("UseCommitTime") { Mode = BindingMode.TwoWay });
        Bind(automatic, InputElement.IsEnabledProperty, "CanEdit");
        var preview = BoundText("DeliveryTimePreview");
        preview.FontWeight = FontWeight.SemiBold;
        var switchBlock = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                automatic,
                At(new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = "Use exact commit time", FontWeight = FontWeight.SemiBold },
                        Muted(path: "DeliveryTimeHelpText")
                    }
                }, column: 1)
            }
        };
        automatic.VerticalAlignment = VerticalAlignment.Center;
        switchBlock.VerticalAlignment = VerticalAlignment.Center;

        var manual = new ShadcnDateTimePicker
        {
            DateLabel = "DELIVERY DATE",
            TimeLabel = "DELIVERY TIME"
        };
        manual.Bind(ShadcnDateTimePicker.SelectedDateProperty,
            new Binding("DeliveryDate") { Mode = BindingMode.TwoWay });
        manual.Bind(ShadcnDateTimePicker.SelectedTimeProperty,
            new Binding("DeliveryTime") { Mode = BindingMode.TwoWay });
        Bind(manual, InputElement.IsEnabledProperty, "CanEdit");
        Bind(manual, Visual.IsVisibleProperty, "IsManualDeliveryTime");

        preview.HorizontalAlignment = HorizontalAlignment.Right;
        preview.VerticalAlignment = VerticalAlignment.Center;
        var sectionHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            Children =
            {
                Label("DELIVERY DATE AND TIME (PHILIPPINE TIME)"),
                At(preview, column: 1)
            }
        };
        var controls = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
            RowDefinitions = new RowDefinitions("Auto"),
            ColumnSpacing = 28,
            Children =
            {
                switchBlock,
                At(manual, column: 1)
            }
        };
        bool? isNarrow = null;
        controls.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 960;
            if (isNarrow == narrow) return;
            isNarrow = narrow;

            if (narrow)
            {
                controls.ColumnDefinitions = new ColumnDefinitions("*");
                controls.RowDefinitions = new RowDefinitions("Auto,Auto");
                controls.ColumnSpacing = 0;
                controls.RowSpacing = 12;
                Grid.SetColumn(switchBlock, 0);
                Grid.SetRow(switchBlock, 0);
                Grid.SetColumn(manual, 0);
                Grid.SetRow(manual, 1);
            }
            else
            {
                controls.ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*");
                controls.RowDefinitions = new RowDefinitions("Auto");
                controls.ColumnSpacing = 28;
                controls.RowSpacing = 0;
                Grid.SetColumn(switchBlock, 0);
                Grid.SetRow(switchBlock, 0);
                Grid.SetColumn(manual, 1);
                Grid.SetRow(manual, 0);
                Grid.SetColumnSpan(manual, 1);
            }
        };

        return Resource(new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(7),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    sectionHeader,
                    controls
                }
            }
        }, Border.BackgroundProperty, "Secondary");
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
        var rows = new ItemsControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<BatchReceivingRowViewModel>((_, _) => PreviewRow(), true)
        };
        Bind(rows, ItemsControl.ItemsSourceProperty, "PreviewPager.Items");

        var tableHeader = new Grid
        {
            ColumnDefinitions = PreviewColumns(),
            ColumnSpacing = 12,
            Margin = new Thickness(16, 10),
            Children =
            {
                Label("PRODUCT"),
                At(Label("BARCODE"), column: 1),
                At(Label("LIBRARY -> SUPPLIER"), column: 2),
                At(Label("SCANNED QTY"), column: 3),
                At(Label("RECEIVE PIECES"), column: 4),
                At(Label("STATUS"), column: 5)
            }
        };
        Bind(tableHeader, Visual.IsVisibleProperty, "PreviewPager.HasItems");

        var tableBody = new Grid
        {
            Children =
            {
                new StackPanel { Children = { tableHeader, rows } },
                new AvaloniaApp.Views.Controls.TableState
                {
                    [!DataContextProperty] = new Binding("PreviewPager")
                }
            }
        };

        var pager = new AvaloniaApp.Views.Controls.TablePager
        {
            [!DataContextProperty] = new Binding("PreviewPager")
        };

        var previewHeader = new StackPanel
        {
            Margin = new Thickness(20, 18, 20, 12),
            Spacing = 3,
            Children =
            {
                Heading("Validated preview"),
                Muted("Library is scanner input; Supplier is the barcode's registered supplier and is authoritative."),
                Muted(path: "ValidationSummary")
            }
        };

        var previewLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children =
            {
                previewHeader,
                At(new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = tableBody
                }, row: 1),
                At(pager, row: 2)
            }
        };

        return Card(previewLayout, new Thickness(0), clip: true);
    }

    private static Control PreviewRow()
    {
        var status = new StatusBadge();
        status.Bind(StatusBadge.StatusProperty, new Binding(nameof(BatchReceivingRowViewModel.Status)));
        var detailsLabel = BoundText(nameof(BatchReceivingRowViewModel.DetailsActionLabel));
        detailsLabel.FontSize = 11;
        detailsLabel.HorizontalAlignment = HorizontalAlignment.Right;
        detailsLabel.VerticalAlignment = VerticalAlignment.Center;
        detailsLabel.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Primary"));

        var summary = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnDefinitions = PreviewColumns(),
            ColumnSpacing = 12,
            Children =
            {
                Cell(nameof(BatchReceivingRowViewModel.ProductNameDisplay), true),
                At(Cell(nameof(BatchReceivingRowViewModel.Barcode)), column: 1),
                At(Cell(nameof(BatchReceivingRowViewModel.SupplierResolutionDisplay), true), column: 2),
                At(Cell(nameof(BatchReceivingRowViewModel.ScannedQuantityDisplay)), column: 3),
                At(Cell(nameof(BatchReceivingRowViewModel.BasePieceQuantityDisplay)), column: 4),
                At(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
                    ColumnSpacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Left,
                     Children = { status, At(detailsLabel, column: 1) }
                }, column: 5)
            }
        };

        var toggle = new Button
        {
            Padding = new Thickness(16, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = summary
        };
        toggle.Classes.Add("ghost");
        toggle.Bind(Avalonia.Controls.Button.CommandProperty, new Binding("DataContext.TogglePreviewRowCommand")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(BatchReceivingView) }
        });
        toggle.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());

        var detail = Resource(new Border
        {
            Padding = new Thickness(20, 16),
            Child = PreviewRowDetails()
        }, Border.BackgroundProperty, "Secondary");
        detail.Bind(Visual.IsVisibleProperty, new Binding(nameof(BatchReceivingRowViewModel.IsExpanded)));

        var rowBorder = Resource(new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Children = { toggle, detail } }
        }, Border.BorderBrushProperty, "Border");
        rowBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        return rowBorder;
    }

    private static Control PreviewRowDetails()
    {
        var total = BoundText(nameof(BatchReceivingRowViewModel.TotalCostDisplay));
        total.FontSize = 22;
        total.FontWeight = FontWeight.SemiBold;

        var productAction = new ActionButton("Add product", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        productAction.Bind(Avalonia.Controls.Button.ContentProperty, new Binding(nameof(BatchReceivingRowViewModel.ProductActionLabel)));
        productAction.Bind(Visual.IsVisibleProperty, new Binding(nameof(BatchReceivingRowViewModel.CanManageProduct)));
        productAction.Bind(Avalonia.Controls.Button.CommandProperty, new Binding("DataContext.AddUnknownProductCommand")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(BatchReceivingView) }
        });
        productAction.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 20,
            RowSpacing = 16,
            Children =
            {
                DetailValue("BODEGA BEFORE -> AFTER", nameof(BatchReceivingRowViewModel.BodegaChangeDisplay)),
                At(new StackPanel { Spacing = 5, Children = { Label("TOTAL COST"), total } }, column: 1),
                At(new StackPanel { Spacing = 7, Children = { Label("PRODUCT ACTION"), productAction } }, column: 2),
                At(PriceEditor("UNIT COST", nameof(BatchReceivingRowViewModel.PreviousCostDisplay), nameof(BatchReceivingRowViewModel.CostPrice)), row: 1),
                At(PriceEditor("SELLING PRICE", nameof(BatchReceivingRowViewModel.PreviousRegularDisplay), nameof(BatchReceivingRowViewModel.RegularPrice)), column: 1, row: 1),
                At(PriceEditor("EMPLOYEE PRICE", nameof(BatchReceivingRowViewModel.PreviousEmployeeDisplay), nameof(BatchReceivingRowViewModel.EmployeePrice)), column: 2, row: 1)
            }
        };
    }

    private static StackPanel DetailValue(string label, string path)
    {
        var value = BoundText(path);
        value.FontSize = 20;
        value.FontWeight = FontWeight.SemiBold;
        return new StackPanel { Spacing = 5, Children = { Label(label), value } };
    }

    private static StackPanel PriceEditor(string label, string previousPath, string valuePath)
    {
        var previous = BoundText(previousPath);
        previous.FontSize = 10;
        previous.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        var input = new AmountInput { Width = 240, MinHeight = 46, HorizontalAlignment = HorizontalAlignment.Left };
        input.Bind(AmountInput.ValueProperty, new Binding(valuePath) { Mode = BindingMode.TwoWay });
        input.Bind(InputElement.IsEnabledProperty, new Binding(nameof(BatchReceivingRowViewModel.CanEditPrices)));
        return new StackPanel { Spacing = 5, Children = { Label(label), previous, input } };
    }

    // Fixed boundaries keep the header and item templates aligned when rows measure to content.
    private static ColumnDefinitions PreviewColumns() => new("280,210,300,180,180,220");

    private static Border ResultCard()
    {
        var summary = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,0.65*,0.65*,0.8*,0.85*,0.7*,1.2*"),
            ColumnSpacing = 12,
            Children =
            {
                ResultValue("RECEIPT / INVOICE NO.", "Result.ReferenceDisplay"),
                At(ResultValue("RECORDS", "Result.AcceptedRecordCount"), column: 1),
                At(ResultValue("PRODUCTS", "Result.AffectedProductCount"), column: 2),
                At(ResultValue("BASE PIECES", "Result.TotalBasePieces"), column: 3),
                At(ResultValue("TOTAL COST", "Result.TotalCostDisplay"), column: 4),
                At(ResultValue("NEW PRODUCTS", "Result.CreatedProductCount"), column: 5),
                At(ResultValue("SUPPLIERS", "Result.SuppliersDisplay"), column: 6)
            }
        };
        var timestamps = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16,
            Children =
            {
                ResultValue("DELIVERED (PHILIPPINE TIME)", "Result.DeliveryAtDisplay"),
                At(ResultValue("RECORDED (PHILIPPINE TIME)", "Result.CompletedAtDisplay"), column: 1)
            }
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new StackPanel { Spacing = 3, Children = { Heading("Receipt completed"), Muted("The scanner capture may now be cleared from the Eyoyo library.") } },
                summary,
                timestamps
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
