using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.Dialogs;
using AvaloniaApp.Views.UI;
using Lucide.Avalonia;

namespace AvaloniaApp.Views;

public sealed class UsersView : UserControl
{
    public UsersView()
    {
        ConfigurePasswordStyles();
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel
            {
                Margin = new Thickness(30),
                Spacing = 16,
                Children =
                {
                    Header(),
                    Metrics(),
                    StatusMessage(),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("1.8*,0.85*"),
                        ColumnSpacing = 16,
                        Children = { UserTable(), At(EditorCard(), column: 1) }
                    }
                }
            }
        };
    }

    private void ConfigurePasswordStyles()
    {
        Styles.Add(new Style(x => x.OfType<LucideIcon>().Class("password-visible"))
        {
            Setters = { new Setter(Visual.IsVisibleProperty, false) }
        });
        Styles.Add(new Style(x => x.OfType<ToggleButton>().Class(":checked").Descendant().OfType<LucideIcon>().Class("password-hidden"))
        {
            Setters = { new Setter(Visual.IsVisibleProperty, false) }
        });
        Styles.Add(new Style(x => x.OfType<ToggleButton>().Class(":checked").Descendant().OfType<LucideIcon>().Class("password-visible"))
        {
            Setters = { new Setter(Visual.IsVisibleProperty, true) }
        });
        Styles.Add(new Style(x => x.OfType<ToggleButton>().Class("password-toggle").Class(":checked"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent)
            }
        });
        Styles.Add(new Style(x => x.OfType<ToggleButton>().Class("password-toggle").Class(":pointerover"))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent) }
        });
        Styles.Add(new Style(x => x.OfType<ToggleButton>().Class("password-toggle").Class(":pressed"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent)
            }
        });
        Styles.Add(new Style(x => x.OfType<ToggleButton>().Class("password-toggle").Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent) }
        });
    }

    private static Control Header()
    {
        var title = new TextBlock { Text = "Users", Classes = { "h1" } };
        var subtitle = Muted("Control staff accounts and assign access to store operations.");
        var add = new ActionButton("New user", ActionButtonVariant.Primary, ActionButtonSize.Sm);
        add.Bind(Button.CommandProperty, new Binding("NewUserCommand"));
        Grid.SetColumn(add, 1);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { new StackPanel { Spacing = 4, Children = { title, subtitle } }, add }
        };
    }

    private static Control Metrics()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 12 };
        grid.Children.Add(Metric(0, LucideIconKind.Users, "TotalUserCount", "Total accounts", "All configured users"));
        grid.Children.Add(Metric(1, LucideIconKind.UserCheck, "ActiveUserCount", "Active users", "Can currently sign in"));
        grid.Children.Add(Metric(2, LucideIconKind.ShieldCheck, "AdminCount", "Administrators", "Full system access"));
        grid.Children.Add(Metric(3, LucideIconKind.UserX, "InactiveUserCount", "Inactive users", "Access is disabled"));
        return grid;
    }

    private static Border Metric(int column, LucideIconKind iconKind, string valuePath, string label, string detail)
    {
        var icon = new LucideIcon { Kind = iconKind, Width = 20, Height = 20 };
        Resource(icon, LucideIcon.ForegroundProperty, "Primary");
        var iconHost = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = icon
        };
        Resource(iconHost, Border.BackgroundProperty, "Secondary");
        var value = Bound("", valuePath, FontWeight.Bold, 24);
        var card = Card(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 12,
            Children =
            {
                iconHost,
                At(new StackPanel { Spacing = 1, Children = { value, new TextBlock { Text = label, FontWeight = FontWeight.SemiBold }, Muted(detail, 11) } }, column: 1)
            }
        }, new Thickness(16));
        Grid.SetColumn(card, column);
        return card;
    }

    private static Control UserTable()
    {
        var search = new IconInput("Search", "Search name, username, or role...");
        search.Input.Bind(TextBox.TextProperty, new Binding("SearchText"));
        var status = new ComboBox { MinWidth = 130, Classes = { "form-select" } };
        status.Bind(ItemsControl.ItemsSourceProperty, new Binding("StatusFilters"));
        status.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SelectedStatusFilter"));
        var refresh = new ActionButton("Refresh", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        refresh.Bind(Button.CommandProperty, new Binding("LoadCommand"));

        var controls = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 8,
            Children = { search, At(status, column: 1), At(refresh, column: 2) }
        };

        var table = new PagedTable
        {
            ItemName = "user account",
            ItemNamePlural = "user accounts",
            PageSize = 10,
            MinHeight = 430,
            MinTableWidth = 780,
            IsSelectable = false
        };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding("FilteredUsers"));
        table.Bind(PagedTable.IsLoadingProperty, new Binding("IsBusy"));
        table.Bind(PagedTable.ErrorMessageProperty, new Binding("ErrorMessage"));
        table.Bind(PagedTable.IsFilteredProperty, new Binding("IsFiltered"));
        table.Bind(PagedTable.RetryCommandProperty, new Binding("LoadCommand"));
        table.Bind(PagedTable.ClearFiltersCommandProperty, new Binding("ClearFiltersCommand"));

        var identity = PagedTableColumn.Create<UserResponse, string>("USER", user => user.DisplayName, new GridLength(1.5, GridUnitType.Star));
        identity.CellTemplate = new FuncDataTemplate<UserResponse>((_, _) => IdentityCell(), true);
        var role = PagedTableColumn.Create<UserResponse, string>("ROLE", user => user.RoleDisplay, new GridLength(0.8, GridUnitType.Star));
        role.CellTemplate = new FuncDataTemplate<UserResponse>((_, _) => RoleBadge(), true);
        var statusColumn = PagedTableColumn.Create<UserResponse, string>("STATUS", user => user.Status, new GridLength(0.65, GridUnitType.Star));
        statusColumn.CellTemplate = new FuncDataTemplate<UserResponse>((_, _) => StatusCell(), true);
        var actions = PagedTableColumn.Create<UserResponse, string>("ACTIONS", _ => "", new GridLength(1.05, GridUnitType.Star), false);
        actions.CellTemplate = new FuncDataTemplate<UserResponse>((_, _) => RowActions(), true);
        table.Columns.Add(identity);
        table.Columns.Add(PagedTableColumn.Create<UserResponse, string>("USERNAME", user => user.Username, new GridLength(1, GridUnitType.Star)));
        table.Columns.Add(role);
        table.Columns.Add(statusColumn);
        table.Columns.Add(actions);

        var heading = new TextBlock { Text = "User directory", Classes = { "h2" } };
        var tableCard = Card(new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 14,
            Children =
            {
                new StackPanel { Spacing = 3, Children = { heading, Muted("All active and inactive accounts from the database.") } },
                At(controls, row: 1),
                At(table, row: 2)
            }
        }, new Thickness(18));
        tableCard.VerticalAlignment = VerticalAlignment.Top;
        return tableCard;
    }

    private static Control EditorCard()
    {
        var title = Bound("", "Editor.Title", FontWeight.SemiBold, 20);
        var description = Bound("", "Editor.Description", resource: "MutedForeground");
        description.TextWrapping = TextWrapping.Wrap;

        var role = new ComboBox { ItemsSource = Enum.GetValues<ApiUserRole>(), Classes = { "form-select" } };
        role.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("Editor.Role"));
        var active = new CheckBox { Content = "Account is active" };
        active.Bind(ToggleButton.IsCheckedProperty, new Binding("Editor.IsActive"));
        active.Bind(Visual.IsVisibleProperty, new Binding("Editor.IsEditing"));

        var save = new ActionButton("", ActionButtonVariant.Primary, ActionButtonSize.Md)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        save.Bind(ContentControl.ContentProperty, new Binding("Editor.ActionText"));
        save.Bind(Button.CommandProperty, new Binding("SaveCommand"));
        var cancel = new ActionButton("Cancel edit", ActionButtonVariant.Secondary, ActionButtonSize.Md)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        cancel.Bind(Button.CommandProperty, new Binding("CancelEditCommand"));
        cancel.Bind(Visual.IsVisibleProperty, new Binding("Editor.IsEditing"));

        var passwordHelp = new TextBlock
        {
            Text = "Minimum 8 characters. Leave blank while editing to keep the current password.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        Resource(passwordHelp, TextBlock.ForegroundProperty, "MutedForeground");

        var content = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new StackPanel { Spacing = 3, Children = { title, description } },
                Field("DISPLAY NAME", "Editor.DisplayName", "e.g. Maria Santos"),
                Field("USERNAME", "Editor.Username", "Used to sign in"),
                new StackPanel { Spacing = 6, Children = { Label("ROLE"), role } },
                new StackPanel { Spacing = 6, Children = { Label("PASSWORD"), PasswordField(), passwordHelp } },
                active,
                new StackPanel { Spacing = 8, Children = { save, cancel } }
            }
        };
        var card = Card(content, new Thickness(20));
        card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    private static Control IdentityCell()
    {
        var password = new TextBlock { Text = "Password change required", FontSize = 10 };
        Resource(password, TextBlock.ForegroundProperty, "MutedForeground");
        password.Bind(Visual.IsVisibleProperty, new Binding("MustChangePassword"));
        return new StackPanel { Spacing = 1, Children = { Bound("", "DisplayName", FontWeight.SemiBold), password } };
    }

    private static Control RoleBadge()
    {
        var text = Bound("", "RoleDisplay", FontWeight.SemiBold, 11);
        var badge = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(9, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = text
        };
        Resource(badge, Border.BackgroundProperty, "Secondary");
        return badge;
    }

    private static Control StatusCell()
    {
        var badge = new StatusBadge();
        badge.Bind(StatusBadge.StatusProperty, new Binding("Status"));
        return badge;
    }

    private static Control RowActions()
    {
        var edit = new ActionButton("Edit", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        edit.Click += (_, _) =>
        {
            if (edit.DataContext is UserResponse user && GetViewModel(edit) is { } viewModel)
                viewModel.EditUserCommand.Execute(user);
        };
        var deactivate = new ActionButton("Deactivate", ActionButtonVariant.Danger, ActionButtonSize.Sm);
        deactivate.Click += async (_, _) =>
        {
            if (deactivate.DataContext is not UserResponse user || GetViewModel(deactivate) is not { } viewModel ||
                TopLevel.GetTopLevel(deactivate) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Deactivate user", $"Deactivate {user.DisplayName}? Their active sessions will be revoked.", "Deactivate");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await viewModel.DeactivateAsync(user);
        };
        var restore = new ActionButton("Restore", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        restore.Click += async (_, _) =>
        {
            if (restore.DataContext is UserResponse user && GetViewModel(restore) is { } viewModel)
                await viewModel.ReactivateAsync(user);
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { edit, deactivate, restore } };
        void UpdateVisibility()
        {
            if (actions.DataContext is not UserResponse user) return;
            edit.IsVisible = user.IsActive;
            deactivate.IsVisible = user.IsActive;
            restore.IsVisible = !user.IsActive;
        }
        actions.DataContextChanged += (_, _) => UpdateVisibility();
        actions.AttachedToVisualTree += (_, _) => UpdateVisibility();
        return actions;
    }

    private static Control Field(string label, string path, string placeholder)
    {
        var input = new TextBox { PlaceholderText = placeholder, Classes = { "form-input" } };
        input.Bind(TextBox.TextProperty, new Binding(path));
        return new StackPanel { Spacing = 6, Children = { Label(label), input } };
    }

    private static Control PasswordField()
    {
        var input = new TextBox { PlaceholderText = "Temporary or replacement password", PasswordChar = '*', Classes = { "form-input" } };
        input.Bind(TextBox.TextProperty, new Binding("Editor.Password"));
        var hiddenIcon = new LucideIcon { Kind = LucideIconKind.EyeOff, Width = 18, Height = 18, Foreground = Brushes.Black };
        hiddenIcon.Classes.Add("password-hidden");
        var visibleIcon = new LucideIcon { Kind = LucideIconKind.Eye, Width = 18, Height = 18, Foreground = Brushes.Black };
        visibleIcon.Classes.Add("password-visible");
        var toggle = new ToggleButton
        {
            Width = 38,
            Height = 34,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Foreground = Brushes.White,
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = new Grid { Width = 18, Height = 18, Children = { hiddenIcon, visibleIcon } }
        };
        toggle.Classes.Add("password-toggle");
        toggle.Background = Brushes.Transparent;
        toggle.BorderBrush = Brushes.Transparent;
        Resource(toggle, TemplatedControl.ForegroundProperty, "Foreground");
        toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(TextBox.RevealPassword)) { Source = input, Mode = BindingMode.TwoWay });
        ToolTip.SetTip(toggle, "Show or hide password");
        input.InnerRightContent = toggle;
        return input;
    }

    private static TextBlock Label(string text) => new() { Text = text, Classes = { "form-label" } };

    private static Border StatusMessage()
    {
        var value = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(7), Child = Bound("", "StatusMessage") };
        Resource(value, Border.BackgroundProperty, "Secondary");
        return value;
    }

    private static UsersViewModel? GetViewModel(Control control) =>
        control.GetVisualAncestors().OfType<UsersView>().FirstOrDefault()?.DataContext as UsersViewModel;

    private static Border Card(Control child, Thickness padding)
    {
        var card = new Border { Padding = padding, Child = child, Classes = { "theme-card" } };
        Resource(card, Border.BackgroundProperty, "Card");
        return card;
    }

    private static TextBlock Bound(string text, string path, FontWeight? weight = null, double? size = null, string? resource = null)
    {
        var value = new TextBlock { Text = text, FontWeight = weight ?? FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
        if (size is not null) value.FontSize = size.Value;
        if (resource is not null) Resource(value, TextBlock.ForegroundProperty, resource);
        value.Bind(TextBlock.TextProperty, new Binding(path));
        return value;
    }

    private static TextBlock Muted(string text, double? size = null)
    {
        var value = new TextBlock { Text = text };
        if (size is not null) value.FontSize = size.Value;
        Resource(value, TextBlock.ForegroundProperty, "MutedForeground");
        return value;
    }

    private static T At<T>(T control, int row = 0, int column = 0) where T : Control
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        return control;
    }

    private static void Resource(AvaloniaObject target, AvaloniaProperty property, object key) =>
        target.Bind(property, new DynamicResourceExtension(key));
}
