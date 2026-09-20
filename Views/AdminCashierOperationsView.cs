using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.Dialogs;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class AdminCashierOperationsView : UserControl
{
    private AdminCashierOperationsViewModel? _subscribedViewModel;

    public AdminCashierOperationsView()
    {
        DataContextChanged += OnDataContextChanged;
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "History and cash differences", Content = Scroll(HistorySection()) },
                new TabItem { Header = "Report Summary", Content = Scroll(ReportSection()) }
            }
        };

        Content = new Grid
        {
            Margin = new Thickness(30),
            RowDefinitions = new RowDefinitions("*"),
            Children =
            {
                tabs
            }
        };
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
            _subscribedViewModel.SessionDetailLoaded -= OnSessionDetailLoaded;
        _subscribedViewModel = DataContext as AdminCashierOperationsViewModel;
        if (_subscribedViewModel is not null)
            _subscribedViewModel.SessionDetailLoaded += OnSessionDetailLoaded;
    }

    private async void OnSessionDetailLoaded(CashierShiftSessionDetailResponse detail)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var dialog = new CashierShiftSummaryDialog(detail.Session, detail.CashAdjustments, _subscribedViewModel);
        await dialog.ShowDialog(owner);
    }

    private static Control HistorySection()
    {
        var content = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                Filters(),
                Status(),
                SessionHistory()
            }
        };
        return content;
    }

    private static Control Filters()
    {
        var dateRange = new DateRangePicker
        {
            PlaceholderText = "Select business dates",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        dateRange.Bind(DateRangePicker.StartDateProperty,
            new Binding("FromDate") { Mode = BindingMode.TwoWay });
        dateRange.Bind(DateRangePicker.EndDateProperty,
            new Binding("ToDate") { Mode = BindingMode.TwoWay });
        var status = new ComboBox { Classes = { "form-select" }, MinWidth = 180 };
        status.Bind(ItemsControl.ItemsSourceProperty, new Binding("StatusFilters"));
        status.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SelectedStatusFilter") { Mode = BindingMode.TwoWay });
        var cashier = new ComboBox { Classes = { "form-select" }, MinWidth = 190, PlaceholderText = "All cashiers" };
        cashier.Bind(ItemsControl.ItemsSourceProperty, new Binding("Cashiers"));
        cashier.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SelectedCashier") { Mode = BindingMode.TwoWay });
        cashier.ItemTemplate = new FuncDataTemplate<UserResponse>((user, _) => new TextBlock { Text = user?.DisplayName ?? "" }, true);
        var shift = new ComboBox { Classes = { "form-select" }, MinWidth = 170, PlaceholderText = "All shifts" };
        shift.Bind(ItemsControl.ItemsSourceProperty, new Binding("ShiftDefinitions"));
        shift.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SelectedShiftDefinition") { Mode = BindingMode.TwoWay });
        shift.ItemTemplate = new FuncDataTemplate<ShiftDefinitionResponse>((item, _) => new TextBlock { Text = item?.Name ?? "" }, true);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Bottom,
            Children =
            {
                CommandButton("Apply filters", ActionButtonVariant.Primary, "ApplyFiltersCommand"),
                CommandButton("Refresh", ActionButtonVariant.Secondary, "RefreshCommand")
            }
        };

        return Card(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.45*,*,*,*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                Field("BUSINESS DATE RANGE", dateRange),
                At(Field("SESSION STATUS", status), column: 1),
                At(Field("CASHIER", cashier), column: 2),
                At(Field("SHIFT", shift), column: 3),
                At(actions, column: 4)
            }
        }, new Thickness(16));
    }

    private static Control SessionHistory()
    {
        var sessions = new ListBox
        {
            ItemTemplate = new FuncDataTemplate<CashierShiftSessionResponse>((session, _) => SessionRow(session), true)
        };
        sessions.Bind(ItemsControl.ItemsSourceProperty, new Binding("Sessions"));
        sessions.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SelectedSession") { Mode = BindingMode.TwoWay });
        sessions.Tapped += async (_, _) =>
        {
            // Tapped fires even when the same already-selected row is opened again.
            await Task.Yield();
            if (sessions.SelectedItem is not CashierShiftSessionResponse session ||
                sessions.GetVisualAncestors().OfType<AdminCashierOperationsView>().FirstOrDefault() is not { } view ||
                view.DataContext is not AdminCashierOperationsViewModel viewModel ||
                TopLevel.GetTopLevel(sessions) is not Window) return;

            await viewModel.OpenSessionAsync(session.Id);
        };

        var pageSize = new ComboBox
        {
            Width = 76,
            MinHeight = 34,
            Padding = new Thickness(10, 4),
            Classes = { "form-select" }
        };
        pageSize.Bind(ItemsControl.ItemsSourceProperty, new Binding("PageSizeOptions"));
        pageSize.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("PageSize") { Mode = BindingMode.TwoWay });
        var rowsLabel = Muted("Rows");
        rowsLabel.VerticalAlignment = VerticalAlignment.Center;
        var pageSummary = Bound("PageSummary", FontWeight.SemiBold);
        pageSummary.VerticalAlignment = VerticalAlignment.Center;
        var navigation = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                CommandButton("Previous", ActionButtonVariant.Secondary, "PreviousPageCommand", ActionButtonSize.Sm),
                CommandButton("Next", ActionButtonVariant.Secondary, "NextPageCommand", ActionButtonSize.Sm)
            }
        };
        navigation.Bind(Visual.IsVisibleProperty, new Binding("HasSessions"));

        var pager = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { rowsLabel, pageSize, pageSummary }
                },
                navigation
            }
        };

        var card = Card(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 3,
                            Children = { Heading("Session history", "h2"), Muted("Choose a work period to inspect its details.") }
                        },
                        At(Bound("PageSummary", FontWeight.SemiBold), column: 1)
                    }
                },
                SessionTableHeader(),
                sessions,
                pager
            }
        });
        card.Padding = new Thickness(16);
        return card;
    }

    private static Control SessionTableHeader() => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("1.6*,1*,1.7*,1.7*,Auto,1*,1*"),
        ColumnSpacing = 12,
        Children =
        {
            Label("SHIFT / CASHIER"),
            At(Label("BUSINESS DATE"), column: 1),
            At(Label("SCHEDULED"), column: 2),
            At(Label("ACTUAL"), column: 3),
            At(Label("STATUS"), column: 4),
            At(Label("SALES"), column: 5),
            At(Label("CASH DIFFERENCE"), column: 6)
        }
    };

    private static Control SessionRow(CashierShiftSessionResponse session)
    {
        return RowCard(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.6*,1*,1.7*,1.7*,Auto,1*,1*"),
            ColumnSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Spacing = 3,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = session.ShiftName, FontWeight = FontWeight.SemiBold },
                        Muted(session.CashierName, 11)
                    }
                },
                At(Cell($"{session.BusinessDate:MMM d, yyyy}"), column: 1),
                At(Cell(session.ScheduledDisplay), column: 2),
                At(Cell(session.ActualDisplay), column: 3),
                At(Badge(session.StatusDisplay), column: 4),
                At(Cell(session.TotalSalesDisplay, true), column: 5),
                At(Cell(session.VarianceDisplay, true), column: 6)
            }
        });
    }

    private static TextBlock Cell(string text, bool emphasize = false) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = emphasize ? FontWeight.SemiBold : FontWeight.Normal,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Control SessionDetail()
    {
        var loading = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
            Children =
            {
                new ProgressBar { Width = 80, Height = 4, IsIndeterminate = true },
                Muted("Loading work period details...")
            }
        };
        loading.Bind(Visual.IsVisibleProperty, new Binding("IsDetailLoading"));

        var placeholder = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 5,
            Children =
            {
                Heading("Select a cashier shift", "h2"),
                Muted("Sales, cash adjustments, remittance controls, and audit history will appear here.")
            }
        };
        placeholder.Bind(Visual.IsVisibleProperty, new Binding("ShowDetailPlaceholder"));

        var detail = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                DetailHeader(),
                SessionSummary(),
                AdministrativeClockOutForm(),
                RemittanceForm(),
                CorrectionForm(),
                SalesSection(),
                AdjustmentsSection(),
                CorrectionsSection()
            }
        };
        detail.Bind(Visual.IsVisibleProperty, new Binding("HasDetail"));

        var error = Bound("DetailError", FontWeight.SemiBold, wrap: true);
        return Card(new Grid { Children = { loading, placeholder, error, detail } }, new Thickness(20));
    }

    private static Control DetailHeader()
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Bound("Detail.Session.CashierName", FontWeight.Bold, 20),
                Bound("Detail.Session.ShiftName", FontWeight.SemiBold),
                MutedBound("Detail.Session.StatusDisplay")
            }
        };
    }

    private static Control SessionSummary()
    {
        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Section("Timeline", "When this work period was scheduled and worked.", MetadataGrid(
                    ("BUSINESS DATE", "Detail.Session.BusinessDate", "{0:MMM d, yyyy}"),
                    ("SCHEDULED", "Detail.Session.ScheduledDisplay", null),
                    ("ACTUAL", "Detail.Session.ActualDisplay", null),
                    ("WORKED", "Detail.Session.WorkedDisplay", null),
                    ("CLOSE TYPE", "Detail.Session.CloseType", null))),
                Section("Cash position", "Expected and actual cash movement for this work period.", MetadataGrid(
                    ("OPENING FLOAT", "Detail.Session.OpeningCashFloat", "₱{0:N2}"),
                    ("EXPECTED TERMINAL CASH", "Detail.Session.ExpectedTerminalCash", "₱{0:N2}"),
                    ("EXPECTED REMITTANCE", "Detail.Session.ExpectedRemittance", "₱{0:N2}"),
                    ("ACTUAL REMITTANCE", "Detail.Session.ActualRemittance", "₱{0:N2}"),
                    ("CASH DIFFERENCE", "Detail.Session.VarianceDisplay", null),
                    ("FLOAT RETURNED", "Detail.Session.CashFloatReturned", null))),
                Section("Sales activity", "Sales and adjustments attributed to this cashier session.", MetadataGrid(
                    ("TOTAL SALES", "Detail.Session.TotalSales", "₱{0:N2}"),
                    ("CASH SALES", "Detail.Session.CashSales", "₱{0:N2}"),
                    ("GCASH", "Detail.Session.GCashSales", "₱{0:N2}"),
                    ("CASH REFUNDS", "Detail.Session.CashRefunds", "₱{0:N2}"),
                    ("CASH PAYOUTS", "Detail.Session.CashPayouts", "₱{0:N2}"),
                    ("TRANSACTIONS", "Detail.Session.TransactionCount", "{0:N0}"))),
                Section("Remittance note", "The note recorded for this work period.", MetadataGrid(
                    ("NOTE", "Detail.Session.RemittanceNote", null)))
            }
        };
    }

    private static Grid MetadataGrid(params (string Label, string Path, string? Format)[] fields)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 18,
            RowSpacing = 12
        };
        for (var index = 0; index < fields.Length; index++)
        {
            var field = Metadata(fields[index].Label, fields[index].Path, 0, fields[index].Format);
            field.Width = double.NaN;
            field.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(field, index % 4);
            Grid.SetRow(field, index / 4);
            grid.Children.Add(field);
        }
        return grid;
    }

    private static Control AdministrativeClockOutForm()
    {
        var reason = MultilineInput("Administrative reason required", "AdministrativeClockOutReason", 70);
        var button = ConfirmAdministrativeClockOutButton();
        var panel = Section(
            "Administrative clock out",
            "This finalizes the open session through the audited Admin endpoint. The cashier cannot continue sales on it.",
            new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    Field("REQUIRED REASON", reason),
                    Validation("AdministrativeClockOutValidationMessage"),
                    button
                }
            });
        panel.Bind(Visual.IsVisibleProperty, new Binding("CanShowAdministrativeClockOut"));
        return panel;
    }

    private static Control RemittanceForm()
    {
        var amount = new AmountInput { PlaceholderText = "0.00" };
        amount.Bind(AmountInput.ValueProperty, new Binding("ActualRemittance") { Mode = BindingMode.TwoWay });
        var returned = new CheckBox { Content = "Opening cash float was returned or replaced" };
        returned.Bind(ToggleButton.IsCheckedProperty, new Binding("CashFloatReturned") { Mode = BindingMode.TwoWay });
        var note = MultilineInput("Required for shortage, overage, or unreturned float", "RemittanceNote", 70);
        var preview = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                Bound("ProvisionalVarianceState", FontWeight.SemiBold),
                At(Bound("ProvisionalVarianceDisplay", FontWeight.Bold), column: 1)
            }
        };

        var panel = Section(
            "Record remittance",
            "Enter sales cash only. Starting cash is confirmed separately and is excluded from the difference.",
            new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    Field("ACTUAL SALES CASH REMITTANCE", amount),
                    returned,
                    Field("ADMIN NOTE", note),
                    RowCard(preview),
                    Validation("RemittanceValidationMessage"),
                    CommandButton("Record remittance", ActionButtonVariant.Primary, "RecordRemittanceCommand")
                }
            });
        panel.Bind(Visual.IsVisibleProperty, new Binding("CanShowRemittanceForm"));
        return panel;
    }

    private static Control CorrectionForm()
    {
        var amount = new AmountInput { PlaceholderText = "0.00" };
        amount.Bind(AmountInput.ValueProperty, new Binding("CorrectedActualRemittance") { Mode = BindingMode.TwoWay });
        var returned = new CheckBox { Content = "Corrected state: starting cash was returned or replaced" };
        returned.Bind(ToggleButton.IsCheckedProperty, new Binding("CorrectedCashFloatReturned") { Mode = BindingMode.TwoWay });
        var reason = MultilineInput("Required approved cash correction reason", "CorrectionReason", 70);
        var panel = Section(
            "Approved cash remittance correction",
            "Only remittance amount and float-return state are corrected. Original values remain in correction history.",
            new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    Field("CORRECTED ACTUAL REMITTANCE", amount),
                    returned,
                    Field("REQUIRED REASON", reason),
                    Validation("CorrectionValidationMessage"),
                    CommandButton("Record approved cash correction", ActionButtonVariant.Danger, "CorrectRemittanceCommand")
                }
            });
        panel.Bind(Visual.IsVisibleProperty, new Binding("CanShowCorrectionForm"));
        return panel;
    }

    private static Control SalesSection()
    {
        var list = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<CashierShiftSaleResponse>((sale, _) => RowCard(new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    StaticMetadata("SALE", sale.SaleNumber, 170),
                    StaticMetadata("PAYMENT", sale.PaymentMethod.ToString(), 120),
                    StaticMetadata("TOTAL", sale.TotalDisplay, 130),
                    StaticMetadata("SOLD AT", sale.SoldAtDisplay, 230)
                }
            }), true)
        };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("Detail.Sales"));
        return Section("Linked sales", "Transactions attributed by the server to this cashier session.", list);
    }

    private static Control AdjustmentsSection()
    {
        var note = MultilineInput("Optional note applied to the approval or rejection", "AdjustmentReviewNote", 64);
        var correctionReason = MultilineInput("Required reason when reversing an existing decision", "AdjustmentCorrectionReason", 64);
        var correctedApproval = new CheckBox { Content = "Correct decision to Approved (clear for Rejected)" };
        correctedApproval.Bind(ToggleButton.IsCheckedProperty, new Binding("CorrectAdjustmentToApproved") { Mode = BindingMode.TwoWay });
        var list = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<CashierCashAdjustmentResponse>((adjustment, _) => AdjustmentRow(adjustment), true)
        };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("Detail.CashAdjustments"));
        return Section("Cash adjustments", "Approve or reject pending refund and payout requests. Store expenses remain separate from remittance.", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Field("REVIEW NOTE (OPTIONAL)", note),
                Field("AUDITED CORRECTION REASON", correctionReason),
                correctedApproval,
                list
            }
        });
    }

    private static Control AdjustmentRow(CashierCashAdjustmentResponse adjustment)
    {
        var approve = AdjustmentDecisionButton("Approve", ActionButtonVariant.Primary, adjustment, true);
        var reject = AdjustmentDecisionButton("Reject", ActionButtonVariant.Danger, adjustment, false);
        approve.IsVisible = adjustment.Status == ApiCashAdjustmentStatus.Pending;
        reject.IsVisible = adjustment.Status == ApiCashAdjustmentStatus.Pending;
        var correct = AncestorCommandButton("Correct decision", ActionButtonVariant.Secondary, "CorrectAdjustmentCommand", adjustment);
        correct.IsVisible = adjustment.Status != ApiCashAdjustmentStatus.Pending;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { approve, reject, correct } };
        return RowCard(new StackPanel
        {
            Spacing = 7,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new TextBlock { Text = $"{adjustment.Type} - {adjustment.AmountDisplay}", FontWeight = FontWeight.SemiBold },
                        At(Badge(adjustment.Status.ToString()), column: 1)
                    }
                },
                Muted($"Requested by {adjustment.RequestedByName} at {adjustment.RequestedAtDisplay}", 11),
                new TextBlock { Text = $"Note: {adjustment.Note}", TextWrapping = TextWrapping.Wrap },
                Muted($"Reference: {adjustment.Reference ?? "-"} | Reviewed by: {adjustment.ReviewedByName ?? "-"}", 11),
                actions
            }
        });
    }

    private static Control CorrectionsSection()
    {
        var list = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<CashierShiftCorrectionResponse>((correction, _) => RowCard(new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new TextBlock { Text = correction.FieldName, FontWeight = FontWeight.SemiBold },
                            At(Badge("Edited"), column: 1)
                        }
                    },
                    new TextBlock { Text = $"{correction.OldValue ?? "-"} -> {correction.NewValue ?? "-"}", TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = $"Reason: {correction.Reason}", TextWrapping = TextWrapping.Wrap },
                    Muted($"{correction.CorrectedByName} | {correction.CorrectedAtDisplay}", 11)
                }
            }), true)
        };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("Detail.Corrections"));
        return Section("Correction history", "Audited original and corrected values returned by the server.", list);
    }

    private static Control ReportSection()
    {
        var rows = new PagedTable
        {
            ItemName = "session",
            ItemNamePlural = "sessions",
            PageSize = 10,
            MinHeight = 0,
            MinTableWidth = 1100,
            IsSelectable = false
        };
        rows.Bind(PagedTable.ItemsSourceProperty, new Binding("Report.Sessions"));
        rows.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>(
            "DATE / SHIFT", row => $"{row.BusinessDate:MMM d, yyyy} | {row.ShiftName}", new GridLength(1.5, GridUnitType.Star)));
        rows.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>(
            "CASHIER", row => row.CashierName, new GridLength(1.15, GridUnitType.Star)));
        rows.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>(
            "STATUS", row => row.Status == ApiCashierShiftSessionStatus.ClosedPendingRemittance ? "Pending Remittance" : row.Status.ToString(), new GridLength(1.05, GridUnitType.Star)));
        var actualColumn = PagedTableColumn.Create<CashierShiftReportRowResponse, string>(
            "ACTUAL", row => row.ClockedOutAtUtc.HasValue
                ? $"{StoreDateTime.FormatUtc(row.ClockedInAtUtc)} - {StoreDateTime.FormatUtc(row.ClockedOutAtUtc.Value)}"
                : $"{StoreDateTime.FormatUtc(row.ClockedInAtUtc)} - Active", new GridLength(2.1, GridUnitType.Star));
        actualColumn.CellTemplate = new FuncDataTemplate<CashierShiftReportRowResponse>((row, _) => new TextBlock
        {
            Text = row.ClockedOutAtUtc.HasValue
                ? $"{StoreDateTime.FormatUtc(row.ClockedInAtUtc)}\n{StoreDateTime.FormatUtc(row.ClockedOutAtUtc.Value)}"
                : $"{StoreDateTime.FormatUtc(row.ClockedInAtUtc)}\nActive",
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13
        }, true);
        rows.Columns.Add(actualColumn);
        rows.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>(
            "TXNS", row => $"{row.TransactionCount ?? 0:N0}", new GridLength(0.85, GridUnitType.Star)));
        rows.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>(
            "SALES", row => CashierShiftFormatting.Money(row.TotalSales), new GridLength(1, GridUnitType.Star)));
        rows.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>(
            "EXPECTED", row => CashierShiftFormatting.Money(row.ExpectedRemittance), new GridLength(1, GridUnitType.Star)));
        rows.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>(
            "ACTUAL REMIT.", row => CashierShiftFormatting.Money(row.ActualRemittance), new GridLength(1.25, GridUnitType.Star)));
        rows.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>(
            "CASH DIFFERENCE", row => CashierShiftFormatting.SignedMoney(row.Variance), new GridLength(1, GridUnitType.Star)));
        var report = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 3,
                            Children =
                            {
                                Heading("Cashier shift report", "h2"),
                                Muted("This snapshot uses the active date range and the server report endpoint. The history status filter does not alter report totals.")
                            }
                        },
                        At(CommandButton("Refresh snapshot", ActionButtonVariant.Secondary, "RefreshCommand"), column: 1)
                    }
                },
                ReportSummary(),
                Card(new StackPanel { Spacing = 10, Children = { Heading("Report sessions", "h3"), rows } })
            }
        };
        return report;
    }

    private static Control ReportSummary()
    {
        return Card(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*"),
            ColumnSpacing = 18,
            Children =
            {
                ReportMetadata("SESSIONS", "ReportSessionsDisplay"),
                At(ReportMetadata("TOTAL SALES", "ReportTotalSalesDisplay"), column: 1),
                At(ReportMetadata("CASH SALES", "ReportCashSalesDisplay"), column: 2),
                At(ReportMetadata("GCASH", "ReportGCashSalesDisplay"), column: 3),
                At(ReportMetadata("EXPECTED REMITTANCE", "ReportExpectedDisplay"), column: 4),
                At(ReportMetadata("ACTUAL REMITTANCE", "ReportActualDisplay"), column: 5),
                At(ReportMetadata("CASH DIFFERENCE", "ReportVarianceDisplay"), column: 6)
            }
        });
    }

    private static Control ReportRow(CashierShiftReportRowResponse row)
    {
        var actual = row.ClockedOutAtUtc.HasValue
            ? $"{StoreDateTime.FormatUtc(row.ClockedInAtUtc)} - {StoreDateTime.FormatUtc(row.ClockedOutAtUtc.Value)}"
            : $"{StoreDateTime.FormatUtc(row.ClockedInAtUtc)} - Active";
        return RowCard(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.4*,1.1*,1.1*,2*,.8*,1*,1*,1.2*,1*"),
            ColumnSpacing = 14,
            Children =
            {
                ReportCell("DATE / SHIFT", $"{row.BusinessDate:MMM d, yyyy} | {row.ShiftName}"),
                At(ReportCell("CASHIER", row.CashierName), column: 1),
                At(ReportCell("STATUS", row.Status == ApiCashierShiftSessionStatus.ClosedPendingRemittance ? "Pending Remittance" : row.Status.ToString()), column: 2),
                At(ReportCell("ACTUAL", actual), column: 3),
                At(ReportCell("TRANSACTIONS", $"{row.TransactionCount ?? 0:N0}"), column: 4),
                At(ReportCell("TOTAL SALES", CashierShiftFormatting.Money(row.TotalSales)), column: 5),
                At(ReportCell("EXPECTED", CashierShiftFormatting.Money(row.ExpectedRemittance)), column: 6),
                At(ReportCell("ACTUAL REMITTANCE", CashierShiftFormatting.Money(row.ActualRemittance)), column: 7),
                At(ReportCell("CASH DIFFERENCE", CashierShiftFormatting.SignedMoney(row.Variance)), column: 8)
            }
        });
    }

    private static StackPanel ReportMetadata(string label, string path)
    {
        var field = Metadata(label, path, 0);
        field.Width = double.NaN;
        field.Margin = new Thickness(0);
        return field;
    }

    private static StackPanel ReportCell(string label, string value) => new()
    {
        Spacing = 4,
        Children = { Label(label), new TextBlock { Text = value, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap } }
    };

    private static Border Status()
    {
        var text = Bound("StatusMessage", wrap: true);
        var status = new Border { Padding = new Thickness(12, 9), CornerRadius = new CornerRadius(7), Child = text };
        return Resource(status, Border.BackgroundProperty, "Secondary");
    }

    private static Border Section(string title, string description, Control content)
    {
        var border = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new StackPanel { Spacing = 3, Children = { Heading(title, "h3"), Muted(description, 11) } },
                    content
                }
            }
        };
        return Resource(border, Border.BorderBrushProperty, "Border");
    }

    private static StackPanel Metadata(string label, string path, double width, string? format = null)
    {
        var value = Bound(path, FontWeight.SemiBold, wrap: true, format: format);
        return new StackPanel
        {
            Width = width,
            Margin = new Thickness(0, 0, 12, 12),
            Spacing = 4,
            Children = { Label(label), value }
        };
    }

    private static StackPanel MoneyMetadata(string label, string path, double width) => Metadata(label, path, width, "₱{0:N2}");

    private static StackPanel StaticMetadata(string label, string value, double width) => new()
    {
        Width = width,
        Margin = new Thickness(0, 0, 12, 8),
        Spacing = 4,
        Children = { Label(label), new TextBlock { Text = value, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap } }
    };

    private static TextBox MultilineInput(string placeholder, string path, double minHeight)
    {
        var input = new TextBox
        {
            PlaceholderText = placeholder,
            AcceptsReturn = true,
            MaxLength = 1000,
            MinHeight = minHeight,
            TextWrapping = TextWrapping.Wrap,
            Classes = { "form-input" }
        };
        input.Bind(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return input;
    }

    private static StackPanel Field(string label, Control control) => new() { Spacing = 6, Children = { Label(label), control } };

    private static TextBlock Validation(string path)
    {
        var value = Bound(path, FontWeight.SemiBold, wrap: true);
        Resource(value, TextBlock.ForegroundProperty, "Destructive");
        return value;
    }

    private static ActionButton CommandButton(string text, ActionButtonVariant variant, string commandPath, ActionButtonSize size = ActionButtonSize.Md)
    {
        var button = new ActionButton(text, variant, size);
        button.Bind(Button.CommandProperty, new Binding(commandPath));
        return button;
    }

    private static ActionButton AncestorCommandButton(string text, ActionButtonVariant variant, string commandPath, object parameter)
    {
        var button = new ActionButton(text, variant, ActionButtonSize.Sm) { CommandParameter = parameter };
        button.Bind(Button.CommandProperty, new Binding($"DataContext.{commandPath}")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(AdminCashierOperationsView) }
        });
        return button;
    }

    private static ActionButton AdjustmentDecisionButton(string text, ActionButtonVariant variant, CashierCashAdjustmentResponse adjustment, bool approve)
    {
        var button = new ActionButton(text, variant, ActionButtonSize.Sm);
        button.Click += async (_, _) =>
        {
            var view = button.GetVisualAncestors().OfType<AdminCashierOperationsView>().FirstOrDefault();
            if (view?.DataContext is not AdminCashierOperationsViewModel viewModel || TopLevel.GetTopLevel(button) is not Window owner) return;
            var dialog = new ConfirmDialog();
            var action = approve ? "approve" : "reject";
            dialog.SetConfirmation($"{char.ToUpperInvariant(action[0]) + action[1..]} cash adjustment?", $"This will {action} the {adjustment.Type} request for {adjustment.AmountDisplay}. Approved refunds and payouts change expected remittance.", char.ToUpperInvariant(action[0]) + action[1..]);
            await dialog.ShowDialog(owner);
            if (!dialog.Confirmed) return;
            if (approve) viewModel.ApproveAdjustmentCommand.Execute(adjustment);
            else viewModel.RejectAdjustmentCommand.Execute(adjustment);
        };
        return button;
    }

    private static ActionButton ConfirmAdministrativeClockOutButton()
    {
        var button = new ActionButton("Administrative clock out", ActionButtonVariant.Danger);
        button.Bind(Button.IsEnabledProperty, new Binding("DataContext.CanAdministrativeClockOutAction")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(AdminCashierOperationsView) }
        });
        button.Click += async (_, _) =>
        {
            var view = button.GetVisualAncestors().OfType<AdminCashierOperationsView>().FirstOrDefault();
            if (view?.DataContext is not AdminCashierOperationsViewModel viewModel ||
                !viewModel.AdministrativeClockOutCommand.CanExecute(null) || TopLevel.GetTopLevel(button) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Administratively close this shift?", "This audited action is final, prevents further sales on the cashier session, and starts expected remittance calculation. Use it only when the cashier cannot clock out.", "Close shift");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) viewModel.AdministrativeClockOutCommand.Execute(null);
        };
        return button;
    }

    private static Border Card(Control child, Thickness? padding = null)
    {
        var card = new Border
        {
            Padding = padding ?? new Thickness(18),
            Child = child,
            Classes = { "theme-card" },
            VerticalAlignment = VerticalAlignment.Top
        };
        return Resource(card, Border.BackgroundProperty, "Card");
    }

    private static Border RowCard(Control child)
    {
        var row = new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(7),
            Child = child
        };
        return Resource(row, Border.BackgroundProperty, "Secondary");
    }

    private static Border Badge(string text)
    {
        var badge = new Border
        {
            Padding = new Thickness(8, 3),
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold }
        };
        return Resource(badge, Border.BackgroundProperty, "Muted");
    }

    private static TextBlock Heading(string text, string style)
    {
        var value = new TextBlock { Text = text };
        value.Classes.Add(style);
        return value;
    }

    private static TextBlock Label(string text) => new() { Text = text, Classes = { "form-label" } };

    private static TextBlock Bound(string path, FontWeight? weight = null, double? fontSize = null, bool wrap = false, string? format = null)
    {
        var value = new TextBlock
        {
            FontWeight = weight ?? FontWeight.Normal,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap
        };
        if (fontSize.HasValue) value.FontSize = fontSize.Value;
        value.Bind(TextBlock.TextProperty, new Binding(path) { StringFormat = format });
        return value;
    }

    private static TextBlock MutedBound(string path)
    {
        var value = Bound(path, wrap: true);
        return Resource(value, TextBlock.ForegroundProperty, "MutedForeground");
    }

    private static ShadcnDateTimePicker DateOnlyPicker(string path)
    {
        var picker = new ShadcnDateTimePicker { ShowTime = false, ShowDateLabel = false };
        picker.Bind(ShadcnDateTimePicker.SelectedDateProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return picker;
    }

    private static TextBlock Muted(string text, double? fontSize = null)
    {
        var value = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        if (fontSize.HasValue) value.FontSize = fontSize.Value;
        return Resource(value, TextBlock.ForegroundProperty, "MutedForeground");
    }

    private static ScrollViewer Scroll(Control content) => new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Content = new Border { Padding = new Thickness(0, 16, 0, 0), Child = content }
    };

    private static T At<T>(T control, int row = 0, int column = 0) where T : Control
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        return control;
    }

    private static T Resource<T>(T target, AvaloniaProperty property, object key) where T : AvaloniaObject
    {
        target.Bind(property, new DynamicResourceExtension(key));
        return target;
    }
}
