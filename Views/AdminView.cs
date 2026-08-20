using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class AdminView : UserControl
{
    public AdminView()
    {
        var cards = ViewCode.Bind(new ItemsControl
        {
            Margin = new Thickness(0, 0, 0, 20),
            ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid { Columns = 4, ColumnSpacing = 20 }),
            ItemTemplate = new FuncDataTemplate<AdminCard>(
                _ => true,
                (_, _) =>
                {
                    var open = ViewCode.Bind(new Button
                    {
                        Content = "Open",
                        HorizontalAlignment = HorizontalAlignment.Left
                    }, Button.CommandProperty, "DataContext.OpenCardCommand", new RelativeSource
                    {
                        Mode = RelativeSourceMode.FindAncestor,
                        AncestorType = typeof(ItemsControl)
                    });
                    open.Classes.Add("primary");
                    ViewCode.Bind(open, Button.CommandParameterProperty, ".");

                    var panel = new StackPanel { Spacing = 10 };
                    panel.Children.Add(ViewCode.Resource(ViewCode.Bind(new TextBlock
                    {
                        FontSize = 16,
                        FontWeight = FontWeight.Bold
                    }, TextBlock.TextProperty, "Title"), TextBlock.ForegroundProperty, "Foreground"));
                    panel.Children.Add(ViewCode.Resource(ViewCode.Bind(new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.7
                    }, TextBlock.TextProperty, "Description"), TextBlock.ForegroundProperty, "MutedForeground"));
                    panel.Children.Add(open);
                    return ViewCode.Resource(new Border
                    {
                        Padding = new Thickness(24),
                        Margin = new Thickness(0, 0, 4, 6),
                        Child = panel,
                        Classes = { "theme-card" }
                    }, Border.BackgroundProperty, "Card");
                })
        }, ItemsControl.ItemsSourceProperty, "Cards");
        Grid.SetRow(cards, 1);

        var table = ViewCode.Table("Pager.SourceItems", "staff account", "staff accounts");
        ViewCode.Bind(table, PagedTable.IsLoadingProperty, "Pager.IsLoading");
        ViewCode.Bind(table, PagedTable.ErrorMessageProperty, "Pager.ErrorMessage");
        ViewCode.Bind(table, PagedTable.IsFirstTimeSetupProperty, "Pager.IsFirstTimeSetup");
        ViewCode.Bind(table, PagedTable.EmptyActionTextProperty, "Pager.StateActionText");
        ViewCode.Bind(table, PagedTable.EmptyActionCommandProperty, "Pager.StateActionCommand");
        ViewCode.AddColumns(table,
            ViewCode.Column("Staff ID", "StaffId"),
            ViewCode.Column("Name", "Name", 1.4),
            ViewCode.Column("Department", "Department", 1.3),
            ViewCode.Column("Role", "Role", 1.1),
            ViewCode.Column("Status", "Status", template: ViewCode.StatusTemplate<StaffRecord>("Status")));
        var tableBorder = ViewCode.Resource(new Border
        {
            CornerRadius = new CornerRadius(8),
            Child = table
        }, Border.BackgroundProperty, "Card");
        Grid.SetRow(tableBorder, 3);

        var staffHeading = ViewCode.Resource(new TextBlock
        {
            Text = "Staff Accounts",
            Margin = new Thickness(0, 0, 0, 10),
            Classes = { "h2" }
        }, TextBlock.ForegroundProperty, "Foreground");
        Grid.SetRow(staffHeading, 2);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            Margin = new Thickness(30)
        };
        root.Children.Add(ViewCode.Heading("Administration"));
        root.Children.Add(cards);
        root.Children.Add(staffHeading);
        root.Children.Add(tableBorder);
        Content = root;
    }
}

internal static class ViewCode
{
    internal static T Bind<T>(T target, AvaloniaProperty property, string path, RelativeSource? relativeSource = null,
        BindingMode mode = BindingMode.Default) where T : AvaloniaObject
    {
        target.Bind(property, new Binding(path) { RelativeSource = relativeSource, Mode = mode });
        return target;
    }

    internal static T Bind<T>(T target, AvaloniaProperty property, string path, IValueConverter converter)
        where T : AvaloniaObject
    {
        target.Bind(property, new Binding(path) { Converter = converter });
        return target;
    }

    internal static T Resource<T>(T target, AvaloniaProperty property, string key) where T : Control
    {
        target.Bind(property, target.GetResourceObservable(key));
        return target;
    }

    internal static TextBlock Heading(string text) => Resource(new TextBlock
    {
        Text = text,
        Margin = new Thickness(0, 0, 0, 20),
        Classes = { "h1" }
    }, TextBlock.ForegroundProperty, "Foreground");

    internal static PagedTable Table(string sourcePath, string itemName, string itemNamePlural)
    {
        var table = Bind(new PagedTable
        {
            IsSelectable = true,
            ItemName = itemName,
            ItemNamePlural = itemNamePlural
        }, PagedTable.ItemsSourceProperty, sourcePath);
        return table;
    }

    internal static PagedTableColumn Column(string header, string property, double width = 1,
        IDataTemplate? template = null) => new()
    {
        Header = header,
        PropertyName = property,
        Width = new GridLength(width, GridUnitType.Star),
        CellTemplate = template
    };

    internal static void AddColumns(PagedTable table, params PagedTableColumn[] columns)
    {
        foreach (var column in columns) table.Columns.Add(column);
    }

    internal static IDataTemplate StatusTemplate<T>(string path) where T : class =>
        new FuncDataTemplate<T>(_ => true, (_, _) => StatusBadge(path));

    private static Control StatusBadge(string path)
    {
        return Bind(new AvaloniaApp.Views.UI.StatusBadge(), AvaloniaApp.Views.UI.StatusBadge.StatusProperty, path);
    }
}
