using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.Dialogs;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class EmployeesView : UserControl
{
    public EmployeesView()
    {
        var title = new TextBlock { Text = "Employees" }; title.Classes.Add("h1");
        var search = new IconInput("Search", "Search employee ID or name...");
        search.Input.Bind(TextBox.TextProperty, new Binding("SearchText"));
        var refresh = Button("Refresh", "LoadCommand"); Grid.SetColumn(refresh, 1);
        var list = new ItemsControl();
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("FilteredEmployees"));
        list.ItemTemplate = new FuncDataTemplate<EmployeeResponse>((_, _) => EmployeeRow(), true);
        Grid.SetRow(list, 1);
        var listCard = Card(new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { Header(), list } }, 0);

        Content = new Grid { Margin = new Thickness(30), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), RowSpacing = 14, Children =
        {
            new StackPanel { Spacing = 4, Children = { title, Muted("Reference employees for employee-priced sales and salary deductions") } },
            At(Status(), 1),
            At(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10, Children = { search, refresh } }, 2),
            At(AddCard(), 3),
            At(listCard, 4)
        } };
    }

    private static Border AddCard()
    {
        var submit = Button("Add employee", "CreateEmployeeCommand", true);
        submit.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(submit, 1);
        var input = new FormInput("EMPLOYEE NAME", "Required");
        input.Input.Bind(TextBox.TextProperty, new Binding("EmployeeName"));
        return Card(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Children = { input, submit } }, 20);
    }

    private static Border Header()
    {
        var grid = new Grid { ColumnDefinitions = Columns(), ColumnSpacing = 14 };
        string[] labels = ["EMPLOYEE ID", "NAME", "STATUS", "ACTIONS"];
        for (var index = 0; index < labels.Length; index++)
        {
            var label = Muted(labels[index]); label.FontSize = 10; label.FontWeight = FontWeight.SemiBold; label.LetterSpacing = 0.6;
            grid.Children.Add(At(label, column: index));
        }
        var header = new Border { Padding = new Thickness(16, 10), Child = grid };
        header.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Muted"));
        return header;
    }

    private static Control EmployeeRow()
    {
        var edit = new ActionButton("Edit", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        edit.Click += async (_, _) =>
        {
            if (edit.DataContext is not EmployeeResponse employee || GetViewModel(edit) is not { } vm || TopLevel.GetTopLevel(edit) is not Window owner) return;
            var dialog = new EmployeeEditDialog(employee);
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await vm.UpdateEmployeeAsync(employee, dialog.NameValue);
        };
        var deactivate = new ActionButton("Deactivate", ActionButtonVariant.Danger, ActionButtonSize.Sm);
        deactivate.Click += async (_, _) =>
        {
            if (deactivate.DataContext is not EmployeeResponse employee || GetViewModel(deactivate) is not { } vm || TopLevel.GetTopLevel(deactivate) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Deactivate employee", $"Deactivate {employee.Name}? Historical employee purchases will be preserved.", "Deactivate");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await vm.DeactivateEmployeeAsync(employee);
        };
        var restore = new ActionButton("Restore", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        restore.Click += async (_, _) => { if (restore.DataContext is EmployeeResponse employee && GetViewModel(restore) is { } vm) await vm.ReactivateEmployeeAsync(employee); };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { edit, deactivate, restore }
        };
        void UpdateActions()
        {
            if (actions.DataContext is not EmployeeResponse employee) return;
            edit.IsVisible = employee.IsActive;
            deactivate.IsVisible = employee.IsActive;
            restore.IsVisible = !employee.IsActive;
        }
        actions.DataContextChanged += (_, _) => UpdateActions();
        actions.AttachedToVisualTree += (_, _) => UpdateActions();
        var badge = StatusCell();
        var row = new Grid { ColumnDefinitions = Columns(), ColumnSpacing = 14, Children =
        {
            Bound("EmployeeNumber", true), At(Bound("Name"), column: 1), At(badge, column: 2), At(actions, column: 3)
        } };
        var border = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(16, 12), Child = row };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        return border;
    }

    private static ColumnDefinitions Columns() => new("0.8*,1.5*,120,190");
    private static EmployeesViewModel? GetViewModel(Control control) => control.GetVisualAncestors().OfType<EmployeesView>().FirstOrDefault()?.DataContext as EmployeesViewModel;
    private static StatusBadge StatusCell()
    {
        var badge = new StatusBadge { VerticalAlignment = VerticalAlignment.Center };
        badge.Bind(StatusBadge.StatusProperty, new Binding("Status"));
        return badge;
    }
    private static Border Card(Control child, double padding) { var value = new Border { Padding = new Thickness(padding), ClipToBounds = true, Child = child }; value.Classes.Add("theme-card"); value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card")); return value; }
    private static Border Status() { var value = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(7), Child = Bound("StatusMessage") }; value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return value; }
    private static Button Button(string text, string? command = null, bool primary = false) { var value = new ActionButton(text, primary ? ActionButtonVariant.Primary : ActionButtonVariant.Secondary, ActionButtonSize.Sm); if (command is not null) value.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(command)); return value; }
    private static TextBlock Bound(string path, bool bold = false) { var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal }; value.Bind(TextBlock.TextProperty, new Binding(path)); return value; }
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static T At<T>(T control, int row = 0, int column = 0) where T : Control { Grid.SetRow(control, row); Grid.SetColumn(control, column); return control; }
}
