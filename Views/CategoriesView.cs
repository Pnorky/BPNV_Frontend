using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;
using AvaloniaApp.Views.Dialogs;

namespace AvaloniaApp.Views;

public sealed class CategoriesView : UserControl
{
    public CategoriesView()
    {
        var kind = new ComboBox { Classes = { "form-select" }, Width = 230 };
        kind.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(CategoriesViewModel.CategoryKinds)));
        kind.Bind(SelectingItemsControl.SelectedItemProperty, new Binding(nameof(CategoriesViewModel.SelectedKind)) { Mode = BindingMode.TwoWay });
        kind.ItemTemplate = new FuncDataTemplate<ApiManagedCategoryKind>((value, _) => new TextBlock
        {
            Text = value == ApiManagedCategoryKind.SalesReport ? "Sales Report" : "Product"
        }, true);
        var table = new PagedTable { ItemName = "category", ItemNamePlural = "categories", PageSize = 10, MinTableWidth = 700, IsSelectable = true };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding(nameof(CategoriesViewModel.Categories)));
        table.Bind(PagedTable.SelectedItemProperty, new Binding(nameof(CategoriesViewModel.SelectedCategory)) { Mode = BindingMode.TwoWay });
        table.Bind(PagedTable.IsLoadingProperty, new Binding(nameof(CategoriesViewModel.IsBusy)));
        table.Bind(PagedTable.ErrorMessageProperty, new Binding(nameof(CategoriesViewModel.ErrorMessage)));
        table.Bind(PagedTable.RetryCommandProperty, new Binding(nameof(CategoriesViewModel.LoadCommand)));
        table.Columns.Add(PagedTableColumn.Create<ManagedCategoryResponse, string>("CATEGORY NAME", item => item.Name, new GridLength(1.5, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<ManagedCategoryResponse, int>("DISPLAY ORDER", item => item.SortOrder, new GridLength(.7, GridUnitType.Star)));
        var status = PagedTableColumn.Create<ManagedCategoryResponse, string>("STATUS", item => item.Status, new GridLength(.7, GridUnitType.Star));
        status.CellTemplate = new FuncDataTemplate<ManagedCategoryResponse>((_, _) => Status(), true); table.Columns.Add(status);
        var actions = PagedTableColumn.Create<ManagedCategoryResponse, string>("ACTIONS", _ => "", new GridLength(1.3, GridUnitType.Star), false);
        actions.CellTemplate = new FuncDataTemplate<ManagedCategoryResponse>((_, _) => Actions(), true); table.Columns.Add(actions);

        var name = new TextBox { Classes = { "form-input" }, MinHeight = 42, PlaceholderText = "Category name" };
        name.Bind(TextBox.TextProperty, new Binding(nameof(CategoriesViewModel.Name)) { Mode = BindingMode.TwoWay });
        name.Bind(IsEnabledProperty, new Binding(nameof(CategoriesViewModel.CanEditName)));
        var order = new NumberField { Minimum = 0, Maximum = 10000 };
        order.Bind(NumberField.ValueProperty, new Binding(nameof(CategoriesViewModel.SortOrder)) { Mode = BindingMode.TwoWay });
        var save = Button("", nameof(CategoriesViewModel.SaveCommand), true); save.Bind(ContentControl.ContentProperty, new Binding(nameof(CategoriesViewModel.SaveText)));
        var clear = Button("Clear", nameof(CategoriesViewModel.NewCategoryCommand), false);
        var editor = Card(new StackPanel { Spacing = 12, Children =
        {
            Bound(nameof(CategoriesViewModel.EditorTitle), 20), Muted("Names are used by product forms. Display order controls dropdown and report order."),
            Label("CATEGORY NAME"), name, Label("DISPLAY ORDER"), order,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { save, clear } }
        } });

        Content = new Grid { Margin = new Thickness(30), RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 14, Children =
        {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { Label("CATEGORY LIST"), kind } },
            At(StatusMessage(), 1),
            At(new Grid { ColumnDefinitions = new ColumnDefinitions("2*,0.9*"), ColumnSpacing = 16, Children = { Card(table), At(editor, column: 1) } }, 2)
        } };
    }

    private static Control Actions()
    {
        var deactivate = new ActionButton("Deactivate", ActionButtonVariant.Danger, ActionButtonSize.Sm);
        deactivate.Click += async (_, _) =>
        {
            if (deactivate.DataContext is not ManagedCategoryResponse category || GetViewModel(deactivate) is not { } viewModel ||
                TopLevel.GetTopLevel(deactivate) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Deactivate category", $"Deactivate {category.Name}? It will no longer be available for new products.", "Deactivate");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await viewModel.DeactivateCommand.ExecuteAsync(category);
        };
        var reactivate = new ActionButton("Reactivate", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        reactivate.Click += async (_, _) =>
        {
            if (reactivate.DataContext is ManagedCategoryResponse category && GetViewModel(reactivate) is { } viewModel)
                await viewModel.ReactivateCommand.ExecuteAsync(category);
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { deactivate, reactivate } };
        void UpdateVisibility()
        {
            if (actions.DataContext is not ManagedCategoryResponse category) return;
            deactivate.IsVisible = category.IsActive && !category.IsSystem;
            reactivate.IsVisible = !category.IsActive;
        }
        actions.DataContextChanged += (_, _) => UpdateVisibility();
        actions.AttachedToVisualTree += (_, _) => UpdateVisibility();
        return actions;
    }
    private static StatusBadge Status() { var value = new StatusBadge(); value.Bind(StatusBadge.StatusProperty, new Binding(nameof(ManagedCategoryResponse.Status))); return value; }
    private static Button Button(string text, string command, bool primary, bool ancestor = false) { var value = new ActionButton(text, primary ? ActionButtonVariant.Primary : ActionButtonVariant.Secondary, ActionButtonSize.Sm); value.Bind(Avalonia.Controls.Button.CommandProperty, ancestor ? new Binding(command) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(CategoriesView) } } : new Binding(command)); if (ancestor) value.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding()); return value; }
    private static Border Card(Control child) { var value = new Border { Padding = new Thickness(18), Child = child }; value.Classes.Add("theme-card"); value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card")); return value; }
    private static Border StatusMessage() { var value = new Border { Padding = new Thickness(12, 8), Child = Bound(nameof(CategoriesViewModel.StatusMessage)) }; value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return value; }
    private static TextBlock Label(string text) => new() { Text = text, FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static TextBlock Bound(string path, double size = 14) { var value = new TextBlock { FontSize = size, FontWeight = size >= 20 ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal }; value.Bind(TextBlock.TextProperty, new Binding(path)); return value; }
    private static T At<T>(T value, int row = 0, int column = 0) where T : Control { Grid.SetRow(value, row); Grid.SetColumn(value, column); return value; }
    private static CategoriesViewModel? GetViewModel(Control control)
    {
        for (Control? current = control; current is not null; current = current.Parent as Control)
            if (current.DataContext is CategoriesViewModel viewModel) return viewModel;
        return null;
    }
}
