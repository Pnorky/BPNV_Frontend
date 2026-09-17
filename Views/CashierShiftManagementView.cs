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

public sealed class CashierShiftManagementView : UserControl
{
    public CashierShiftManagementView()
    {
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Shift definitions", Content = Scroll(DefinitionsSection()) },
                new TabItem { Header = "Recurring schedules", Content = Scroll(SchedulesSection()) },
                new TabItem { Header = "Daily replacements", Content = Scroll(ReplacementsSection()) }
            }
        };
        tabs.Bind(TabControl.SelectedIndexProperty, new Binding("SelectedTabIndex") { Mode = BindingMode.TwoWay });

        Content = new Grid
        {
            Margin = new Thickness(30),
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 16,
            Children =
            {
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        Heading("Cashier shifts", "h1"),
                        Muted("Configure store-local shift windows, weekly cashier assignments, and date-specific replacements.")
                    }
                },
                At(tabs, row: 1)
            }
        };
    }

    private static Control DefinitionsSection()
    {
        var list = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<ShiftDefinitionResponse>((definition, _) => DefinitionRow(definition), true)
        };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("Definitions.Items"));

        var refresh = Button("Refresh", ActionButtonVariant.Secondary, "Definitions.LoadCommand");
        var create = Button("New shift", ActionButtonVariant.Primary, "Definitions.NewDefinitionCommand");
        var header = SectionHeader("Shift definitions", "Store-local operating windows. An end time at or before the start ends on the next day.", refresh, create);

        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                header,
                Status("Definitions.StatusMessage"),
                TwoColumn(
                    Card(new StackPanel
                    {
                        Spacing = 12,
                        Children = { Heading("Configured shifts", "h2"), list }
                    }),
                    DefinitionEditor())
            }
        };
    }

    private static Control DefinitionRow(ShiftDefinitionResponse definition)
    {
        var edit = SmallButton("Edit", ActionButtonVariant.Secondary);
        edit.Click += (_, _) => FindViewModel(edit)?.Definitions.EditDefinitionCommand.Execute(definition);
        var deactivate = SmallButton("Deactivate", ActionButtonVariant.Danger);
        deactivate.Click += async (_, _) =>
        {
            if (FindViewModel(deactivate) is not { } viewModel || TopLevel.GetTopLevel(deactivate) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Deactivate shift definition", $"Deactivate {definition.Name}? Existing history remains visible, but it cannot be used for new schedules.", "Deactivate");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await viewModel.Definitions.DeactivateAsync(definition);
        };
        var reactivate = SmallButton("Reactivate", ActionButtonVariant.Secondary);
        reactivate.Click += async (_, _) =>
        {
            if (FindViewModel(reactivate) is { } viewModel) await viewModel.Definitions.ReactivateAsync(definition);
        };
        deactivate.IsVisible = definition.IsActive;
        reactivate.IsVisible = !definition.IsActive;

        return RowCard(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 14,
            Children =
            {
                new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock { Text = definition.Name, FontWeight = FontWeight.SemiBold, FontSize = 15 },
                        Muted(definition.ScheduleDisplay),
                        Badge(definition.IsOvernight ? $"{definition.Status} · Overnight" : definition.Status)
                    }
                },
                At(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { edit, deactivate, reactivate } }, column: 1)
            }
        });
    }

    private static Control DefinitionEditor()
    {
        var start = TimePicker("Definitions.Editor.StartTime");
        var end = TimePicker("Definitions.Editor.EndTime");
        var timeGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 10,
            Children = { Field("START TIME", start), At(Field("END TIME", end), column: 1) }
        };
        var preview = BoundText("Definitions.Editor.RangePreview");
        preview.TextWrapping = TextWrapping.Wrap;
        var form = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                BoundHeading("Definitions.Editor.Title"),
                TextField("NAME", "Definitions.Editor.Name", "e.g. Shift 1"),
                timeGrid,
                new StackPanel { Spacing = 4, Children = { Label("SCHEDULE PREVIEW"), preview, Muted("Overnight shifts explicitly end on the following day.", 11) } },
                FormActions("Definitions.Editor.ActionText", "Definitions.SaveCommand", "Definitions.CancelEditCommand")
            }
        };
        var card = Card(form);
        card.Bind(InputElement.IsEnabledProperty, new Binding("Definitions.CanEdit"));
        return card;
    }

    private static Control SchedulesSection()
    {
        var list = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<CashierShiftScheduleResponse>((schedule, _) => ScheduleRow(schedule), true)
        };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("Schedules.Items"));
        var header = SectionHeader(
            "Recurring schedules",
            "Assign active Cashier-role users by weekday and effective date. The API remains authoritative for overlap conflicts.",
            Button("Refresh", ActionButtonVariant.Secondary, "Schedules.LoadCommand"),
            Button("New schedule", ActionButtonVariant.Primary, "Schedules.NewScheduleCommand"));
        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                header,
                Status("Schedules.StatusMessage"),
                TwoColumn(
                    Card(new StackPanel { Spacing = 12, Children = { Heading("Weekly assignments", "h2"), list } }),
                    ScheduleEditor())
            }
        };
    }

    private static Control ScheduleRow(CashierShiftScheduleResponse schedule)
    {
        var edit = SmallButton("Edit", ActionButtonVariant.Secondary);
        edit.Click += (_, _) => FindViewModel(edit)?.Schedules.EditScheduleCommand.Execute(schedule);
        var deactivate = SmallButton("Deactivate", ActionButtonVariant.Danger);
        deactivate.Click += async (_, _) =>
        {
            if (FindViewModel(deactivate) is not { } viewModel || TopLevel.GetTopLevel(deactivate) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Deactivate recurring schedule", $"Deactivate {schedule.CashierName}'s {schedule.ShiftName} schedule?", "Deactivate");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await viewModel.Schedules.DeactivateAsync(schedule);
        };
        var reactivate = SmallButton("Reactivate", ActionButtonVariant.Secondary);
        reactivate.Click += async (_, _) =>
        {
            if (FindViewModel(reactivate) is { } viewModel) await viewModel.Schedules.ReactivateAsync(schedule);
        };
        deactivate.IsVisible = schedule.IsActive;
        reactivate.IsVisible = !schedule.IsActive;
        var overnight = schedule.EndLocalTime <= schedule.StartLocalTime;
        var time = $"{schedule.StartLocalTime:h:mm tt} - {schedule.EndLocalTime:h:mm tt}{(overnight ? " next day" : "")}";

        return RowCard(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 14,
            Children =
            {
                new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock { Text = $"{schedule.ShiftName} · {schedule.CashierName}", FontWeight = FontWeight.SemiBold, FontSize = 15 },
                        Muted($"{schedule.DaysDisplay} · {time}"),
                        Muted(schedule.EffectiveDisplay, 11),
                        Badge(schedule.Status)
                    }
                },
                At(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { edit, deactivate, reactivate } }, column: 1)
            }
        });
    }

    private static Control ScheduleEditor()
    {
        var cashierSearch = new TextBox { PlaceholderText = "Filter by name or username", Classes = { "search" } };
        cashierSearch.Bind(TextBox.TextProperty, new Binding("Schedules.CashierSearchText") { Mode = BindingMode.TwoWay });
        var cashier = new ComboBox { Classes = { "form-select" } };
        cashier.Bind(ItemsControl.ItemsSourceProperty, new Binding("Schedules.FilteredCashiers"));
        cashier.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("Schedules.Editor.SelectedCashier") { Mode = BindingMode.TwoWay });
        var shift = new ComboBox { Classes = { "form-select" } };
        shift.Bind(ItemsControl.ItemsSourceProperty, new Binding("Schedules.ShiftDefinitions"));
        shift.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("Schedules.Editor.SelectedShift") { Mode = BindingMode.TwoWay });
        var from = DateOnlyPicker("Schedules.Editor.EffectiveFrom");
        var to = DateOnlyPicker("Schedules.Editor.EffectiveTo");

        var form = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                BoundHeading("Schedules.Editor.Title"),
                Field("SEARCH CASHIERS", cashierSearch),
                Field("ASSIGNED CASHIER", cashier),
                Field("SHIFT DEFINITION", shift),
                Field("WEEKDAYS", WeekdayPicker()),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 10,
                    Children = { Field("EFFECTIVE FROM", from), At(Field("EFFECTIVE TO (OPTIONAL)", to), column: 1) }
                },
                Muted("Only active users currently carrying the Cashier role are listed.", 11),
                FormActions("Schedules.Editor.ActionText", "Schedules.SaveCommand", "Schedules.CancelEditCommand")
            }
        };
        var card = Card(form);
        card.Bind(InputElement.IsEnabledProperty, new Binding("Schedules.CanEdit"));
        return card;
    }

    private static Control WeekdayPicker()
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (label, path) in new[]
        {
            ("Mon", "Schedules.Editor.Monday"), ("Tue", "Schedules.Editor.Tuesday"),
            ("Wed", "Schedules.Editor.Wednesday"), ("Thu", "Schedules.Editor.Thursday"),
            ("Fri", "Schedules.Editor.Friday"), ("Sat", "Schedules.Editor.Saturday"),
            ("Sun", "Schedules.Editor.Sunday")
        })
        {
            var day = new CheckBox { Content = label, Margin = new Thickness(0, 0, 10, 6) };
            day.Bind(ToggleButton.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay });
            panel.Children.Add(day);
        }
        return panel;
    }

    private static Control ReplacementsSection()
    {
        var previous = Button("Previous day", ActionButtonVariant.Secondary, "Replacements.PreviousDayCommand");
        var today = Button("Today", ActionButtonVariant.Ghost, "Replacements.TodayCommand");
        var next = Button("Next day", ActionButtonVariant.Secondary, "Replacements.NextDayCommand");
        var refresh = Button("Refresh", ActionButtonVariant.Primary, "Replacements.LoadCommand");
        var date = DateOnlyPicker("Replacements.SelectedBusinessDate");

        var dateBar = Card(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"),
            ColumnSpacing = 8,
            Children = { previous, At(today, column: 1), At(next, column: 2), At(date, column: 3), At(refresh, column: 4) }
        }, new Thickness(14));
        dateBar.Bind(InputElement.IsEnabledProperty, new Binding("Replacements.CanEdit"));

        var list = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<ResolvedDailyShiftRow>((row, _) => DailyShiftRow(row), true)
        };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("Replacements.DailyShifts"));

        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                SectionHeader("Date-specific replacements", "Review the server-resolved daily schedule and replace an assignment before its session starts."),
                dateBar,
                Status("Replacements.StatusMessage"),
                TwoColumn(
                    Card(new StackPanel { Spacing = 12, Children = { BoundHeading("Replacements.SelectedDateDisplay"), list } }),
                    ReplacementEditor())
            }
        };
    }

    private static Control DailyShiftRow(ResolvedDailyShiftRow row)
    {
        var edit = SmallButton(row.HasOverride ? "Edit" : "Replace", ActionButtonVariant.Secondary);
        edit.IsEnabled = row.CanReplace;
        edit.Click += (_, _) => FindViewModel(edit)?.Replacements.EditReplacementCommand.Execute(row);
        ToolTip.SetTip(edit, row.CanReplace ? "Create or edit this date-specific replacement" : row.IsOccupied ? "Administratively clock out the occupied session first" : "A normally completed session cannot be replaced");
        var delete = SmallButton("Delete", ActionButtonVariant.Danger);
        delete.IsVisible = row.HasOverride;
        delete.IsEnabled = row.CanReplace;
        delete.Click += async (_, _) =>
        {
            if (FindViewModel(delete) is not { } viewModel || TopLevel.GetTopLevel(delete) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Delete replacement", $"Delete the {row.Definition.Name} replacement for {row.BusinessDate:MMMM d, yyyy}? The recurring assignment will resolve again.", "Delete");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await viewModel.Replacements.DeleteAsync(row);
        };

        return RowCard(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 14,
            Children =
            {
                new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock { Text = row.Definition.Name, FontWeight = FontWeight.SemiBold, FontSize = 15 },
                        Muted(row.Definition.ScheduleDisplay),
                        new TextBlock { Text = $"Original: {row.OriginalCashierDisplay}" },
                        new TextBlock { Text = $"Resolved cashier: {row.AssignedCashierDisplay}" },
                        Muted($"Reason: {row.ReasonDisplay}", 11),
                        Badge(row.StateDisplay)
                    }
                },
                At(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { edit, delete } }, column: 1)
            }
        });
    }

    private static Control ReplacementEditor()
    {
        var shift = new ComboBox { Classes = { "form-select" } };
        shift.Bind(ItemsControl.ItemsSourceProperty, new Binding("Replacements.DailyShifts"));
        shift.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("Replacements.Editor.SelectedShift") { Mode = BindingMode.TwoWay });
        shift.ItemTemplate = new FuncDataTemplate<ResolvedDailyShiftRow>((row, _) => new TextBlock { Text = $"{row.Definition.Name} · {row.Definition.ScheduleDisplay}" }, true);
        var search = new TextBox { PlaceholderText = "Filter by name or username", Classes = { "search" } };
        search.Bind(TextBox.TextProperty, new Binding("Replacements.CashierSearchText") { Mode = BindingMode.TwoWay });
        var cashier = new ComboBox { Classes = { "form-select" } };
        cashier.Bind(ItemsControl.ItemsSourceProperty, new Binding("Replacements.FilteredCashiers"));
        cashier.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("Replacements.Editor.SelectedCashier") { Mode = BindingMode.TwoWay });
        var reason = new TextBox
        {
            PlaceholderText = "Required absence or replacement reason",
            AcceptsReturn = true,
            MaxLength = 1000,
            MinHeight = 76,
            TextWrapping = TextWrapping.Wrap,
            Classes = { "form-input" }
        };
        reason.Bind(TextBox.TextProperty, new Binding("Replacements.Editor.Reason") { Mode = BindingMode.TwoWay });

        var form = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                BoundHeading("Replacements.Editor.Title"),
                Muted("The selected business date above is sent with this override. The original recurring schedule remains in audit history.", 11),
                Field("SHIFT", shift),
                Field("SEARCH CASHIERS", search),
                Field("REPLACEMENT CASHIER", cashier),
                Field("REASON", reason),
                FormActions("Replacements.Editor.ActionText", "Replacements.SaveCommand", "Replacements.CancelEditCommand")
            }
        };
        var card = Card(form);
        card.Bind(InputElement.IsEnabledProperty, new Binding("Replacements.CanEdit"));
        return card;
    }

    private static Grid SectionHeader(string title, string description, params Control[] actions)
    {
        var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var action in actions) actionPanel.Children.Add(action);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            Children =
            {
                new StackPanel { Spacing = 3, Children = { Heading(title, "h2"), Muted(description) } },
                At(actionPanel, column: 1)
            }
        };
    }

    private static Grid TwoColumn(Control list, Control editor) => new()
    {
        ColumnDefinitions = new ColumnDefinitions("1.55*,0.85*"),
        ColumnSpacing = 16,
        Children = { list, At(editor, column: 1) }
    };

    private static StackPanel FormActions(string actionTextPath, string saveCommandPath, string cancelCommandPath)
    {
        var save = new ActionButton("", ActionButtonVariant.Primary) { HorizontalAlignment = HorizontalAlignment.Stretch };
        save.Bind(ContentControl.ContentProperty, new Binding(actionTextPath));
        save.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(saveCommandPath));
        var cancel = Button("Clear form", ActionButtonVariant.Secondary, cancelCommandPath);
        cancel.HorizontalAlignment = HorizontalAlignment.Stretch;
        return new StackPanel { Spacing = 8, Children = { save, cancel } };
    }

    private static Border Status(string path)
    {
        var text = BoundText(path);
        text.TextWrapping = TextWrapping.Wrap;
        var host = new Border { Padding = new Thickness(12, 9), CornerRadius = new CornerRadius(7), Child = text };
        Resource(host, Border.BackgroundProperty, "Secondary");
        return host;
    }

    private static Border Card(Control child, Thickness? padding = null)
    {
        var card = new Border { Padding = padding ?? new Thickness(18), Child = child, Classes = { "theme-card" }, VerticalAlignment = VerticalAlignment.Top };
        Resource(card, Border.BackgroundProperty, "Card");
        return card;
    }

    private static Border RowCard(Control child)
    {
        var row = new Border { Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 8), CornerRadius = new CornerRadius(8), Child = child };
        Resource(row, Border.BackgroundProperty, "Secondary");
        return row;
    }

    private static Border Badge(string text)
    {
        var badge = new Border
        {
            Padding = new Thickness(8, 3),
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold }
        };
        Resource(badge, Border.BackgroundProperty, "Muted");
        return badge;
    }

    private static StackPanel TextField(string label, string path, string placeholder)
    {
        var input = new TextBox { PlaceholderText = placeholder, MaxLength = 80, Classes = { "form-input" } };
        input.Bind(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return Field(label, input);
    }

    private static StackPanel Field(string label, Control control) => new() { Spacing = 6, Children = { Label(label), control } };
    private static TextBlock Label(string text) => new() { Text = text, Classes = { "form-label" } };

    private static ActionButton Button(string text, ActionButtonVariant variant, string commandPath)
    {
        var button = new ActionButton(text, variant);
        button.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(commandPath));
        return button;
    }

    private static ActionButton SmallButton(string text, ActionButtonVariant variant) => new(text, variant, ActionButtonSize.Sm);

    private static TextBlock Heading(string text, string style)
    {
        var heading = new TextBlock { Text = text };
        heading.Classes.Add(style);
        return heading;
    }

    private static TextBlock BoundHeading(string path)
    {
        var heading = Heading("", "h2");
        heading.Bind(TextBlock.TextProperty, new Binding(path));
        return heading;
    }

    private static TextBlock BoundText(string path)
    {
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, new Binding(path));
        return text;
    }

    private static ShadcnDateTimePicker DateOnlyPicker(string path)
    {
        var picker = new ShadcnDateTimePicker { ShowTime = false, ShowDateLabel = false };
        picker.Bind(ShadcnDateTimePicker.SelectedDateProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return picker;
    }

    private static ShadcnTimePicker TimePicker(string path)
    {
        var picker = new ShadcnTimePicker { PlaceholderText = "6:00 AM" };
        picker.Bind(ShadcnTimePicker.SelectedTimeProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return picker;
    }

    private static TextBlock Muted(string text, double? fontSize = null)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        if (fontSize.HasValue) block.FontSize = fontSize.Value;
        Resource(block, TextBlock.ForegroundProperty, "MutedForeground");
        return block;
    }

    private static ScrollViewer Scroll(Control content) => new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Content = new Border { Padding = new Thickness(0, 16, 0, 0), Child = content }
    };

    private static CashierShiftManagementViewModel? FindViewModel(Control control) =>
        control.GetVisualAncestors().OfType<CashierShiftManagementView>().FirstOrDefault()?.DataContext as CashierShiftManagementViewModel;

    private static T At<T>(T control, int row = 0, int column = 0) where T : Control
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        return control;
    }

    private static void Resource(AvaloniaObject target, AvaloniaProperty property, object key) =>
        target.Bind(property, new DynamicResourceExtension(key));
}
