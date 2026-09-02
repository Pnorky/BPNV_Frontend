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

public class SuppliersView : UserControl
{
    public SuppliersView()
    {
        var title = new TextBlock { Text = "Suppliers" };
        title.Classes.Add("h1");

        var search = new IconInput("Search", "Search supplier, contact, or phone...");
        search.Input.Bind(TextBox.TextProperty, new Binding("SearchText"));
        var refresh = Button("Refresh", "LoadCommand", false);
        Grid.SetColumn(refresh, 1);

        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("FilteredSuppliers"));
        list.ItemTemplate = new FuncDataTemplate<SupplierResponse>((_, _) => SupplierRow(), true);
        Grid.SetRow(list, 1);

        var listCard = new Border
        {
            Padding = new Thickness(0),
            ClipToBounds = true,
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children = { SupplierHeader(), list }
            }
        };
        listCard.Classes.Add("theme-card");
        listCard.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));

        Content = new Grid
        {
            Margin = new Thickness(30),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
            RowSpacing = 14,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, Muted("Database-backed suppliers, including suppliers created by inventory import") } },
                At(Status(), 1),
                At(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10, Children = { search, refresh } }, 2),
                At(AddSupplierCard(), 3),
                At(listCard, 4)
            }
        };
    }

    private static Border AddSupplierCard()
    {
        var submit = Button("Add supplier", "CreateSupplierCommand", true);
        submit.VerticalAlignment = VerticalAlignment.Bottom;
        var form = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.4*,1*,1*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                BoundFormInput("SUPPLIER NAME", "SupplierName", "Required"),
                At(BoundFormInput("CONTACT PERSON", "ContactPerson", "Optional"), column: 1),
                At(BoundFormInput("PHONE", "Phone", "Optional"), column: 2),
                At(submit, column: 3)
            }
        };
        var card = new Border { Padding = new Thickness(20), Child = form };
        card.Classes.Add("theme-card");
        card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        return card;
    }

    private static Border SupplierHeader()
    {
        var grid = new Grid { ColumnDefinitions = SupplierColumns(), ColumnSpacing = 14 };
        var labels = new[] { "SUPPLIER", "CONTACT PERSON", "PHONE", "STATUS", "ACTIONS" };
        for (var index = 0; index < labels.Length; index++)
        {
            var label = Muted(labels[index]);
            label.FontSize = 10;
            label.FontWeight = FontWeight.SemiBold;
            label.LetterSpacing = 0.6;
            grid.Children.Add(At(label, column: index));
        }

        var header = new Border { Padding = new Thickness(16, 10), Child = grid };
        header.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Muted"));
        return header;
    }

    private static Control SupplierRow()
    {
        var edit = Button("Edit", null, false);
        edit.Click += async (_, _) =>
        {
            if (edit.DataContext is not SupplierResponse supplier || GetViewModel(edit) is not { } viewModel ||
                TopLevel.GetTopLevel(edit) is not Window owner) return;
            var dialog = new SupplierEditDialog(supplier);
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed)
                await viewModel.UpdateSupplierAsync(supplier, dialog.NameValue, dialog.ContactValue, dialog.PhoneValue);
        };
        var deactivate = new ActionButton("Deactivate", ActionButtonVariant.Danger, ActionButtonSize.Sm);
        deactivate.Click += async (sender, _) =>
        {
            if (sender is not Button button || button.DataContext is not SupplierResponse supplier ||
                TopLevel.GetTopLevel(button) is not Window owner || GetViewModel(button) is not { } viewModel) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Deactivate supplier", $"Deactivate {supplier.Name}? Existing products and history will be preserved.", "Deactivate");
            await ConfirmDeactivationAsync(supplier, viewModel, async () =>
            {
                await dialog.ShowDialog(owner);
                return dialog.Confirmed;
            });
        };
        var restore = Button("Restore", null, false);
        restore.IsVisible = false;
        restore.Click += async (_, _) =>
        {
            if (restore.DataContext is SupplierResponse supplier && GetViewModel(restore) is { } viewModel)
                await viewModel.ReactivateSupplierAsync(supplier);
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { edit, deactivate, restore } };
        void UpdateActionVisibility()
        {
            if (actions.DataContext is SupplierResponse supplier)
            {
                edit.IsVisible = supplier.IsActive;
                deactivate.IsVisible = supplier.IsActive;
                restore.IsVisible = !supplier.IsActive;
            }
        }
        actions.DataContextChanged += (_, _) => UpdateActionVisibility();
        actions.AttachedToVisualTree += (_, _) => UpdateActionVisibility();
        var row = new Grid
        {
            ColumnDefinitions = SupplierColumns(),
            ColumnSpacing = 14,
            Children =
            {
                Bound("Name", FontWeight.SemiBold),
                At(Bound("ContactPerson"), column: 1),
                At(Bound("Phone"), column: 2),
                At(StatusBadge(), column: 3),
                At(actions, column: 4)
            }
        };
        var border = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(16, 12), Child = row };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        return border;
    }

    private static ColumnDefinitions SupplierColumns() => new("1.4*,1*,1*,Auto,Auto");
    internal static async Task ConfirmDeactivationAsync(
        SupplierResponse supplier,
        SuppliersViewModel viewModel,
        Func<Task<bool>> confirm)
    {
        if (await confirm()) await viewModel.DeactivateSupplierAsync(supplier);
    }
    private static SuppliersViewModel? GetViewModel(Control control) =>
        control.GetVisualAncestors().OfType<SuppliersView>().FirstOrDefault()?.DataContext as SuppliersViewModel;
    private static Border StatusBadge()
    {
        var badge = new AvaloniaApp.Views.UI.StatusBadge();
        badge.Bind(AvaloniaApp.Views.UI.StatusBadge.StatusProperty, new Binding("Status"));
        return badge;
    }
    private static FormInput BoundFormInput(string label, string path, string placeholder)
    {
        var value = new FormInput(label, placeholder);
        value.Input.Bind(TextBox.TextProperty, new Binding(path));
        return value;
    }
    private static Button Button(string text, string? command, bool primary)
    {
        var value = new ActionButton(text, primary ? ActionButtonVariant.Primary : ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        if (command is not null) value.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(command));
        return value;
    }
    private static Border Status() { var value = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(7), Child = Bound("StatusMessage") }; value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return value; }
    private static TextBlock Bound(string path, FontWeight? weight = null) { var value = new TextBlock { FontWeight = weight ?? FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center }; value.Bind(TextBlock.TextProperty, new Binding(path)); return value; }
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static T At<T>(T value, int row = 0, int column = 0) where T : Control { Grid.SetRow(value, row); Grid.SetColumn(value, column); return value; }
}
