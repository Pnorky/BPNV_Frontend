using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaApp.Converters;
using AvaloniaApp.ViewModels;
using Lucide.Avalonia;

namespace AvaloniaApp.Views;

public class DashboardWindow : Window
{
    private FlyoutBase? _inventoryFlyout;
    private readonly Border _sidebarBorder;
    private readonly StackPanel _brandPanel;
    private readonly TextBlock _brandSubtitle;
    private readonly Button _toggleButton;
    private readonly LucideIcon _toggleIcon;
    private readonly Border _userFooter;
    private readonly DockPanel _userFooterContent;
    private readonly Grid _userAvatar;
    private readonly StackPanel _userPanel;
    private readonly ListBox _navList;
    private readonly ContentControl _mainContent;

    public DashboardWindow()
    {
        Title = "BPNV Convenience Store - Sales & Inventory";
        WindowState = WindowState.Maximized;
        Width = 1280;
        Height = 820;
        MinWidth = 920;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.BindResource(BackgroundProperty, "Background");
        this.BindResource(ForegroundProperty, "Foreground");
        Resources["StringToIcon"] = new StringToIconConverter();
        ConfigureStyles();

        _brandSubtitle = new TextBlock { Text = "SALES + INVENTORY", FontSize = 10, Opacity = 0.8 };
        _brandSubtitle.BindResource(TextBlock.ForegroundProperty, "SidebarForeground");
        var brandTitle = new TextBlock { Text = "BPNV STORE", FontSize = 19, FontWeight = FontWeight.Bold };
        brandTitle.BindResource(TextBlock.ForegroundProperty, "Primary");
        _brandPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { brandTitle, _brandSubtitle }
        };

        _toggleIcon = new LucideIcon { Kind = LucideIconKind.PanelLeftClose, Width = 20, Height = 20 };
        _toggleButton = new Button
        {
            Margin = new Thickness(4, 0, 0, 0),
            Content = _toggleIcon
        };
        _toggleButton.Classes.Add("sidebar-toggle");
        _toggleButton.Bind(Button.CommandProperty, new Binding(nameof(DashboardViewModel.ToggleSidebarCommand)));
        _toggleButton.BindResource(ForegroundProperty, "SidebarForeground");
        Grid.SetColumn(_toggleButton, 1);
        var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _brandPanel, _toggleButton } };
        var header = new Border { Padding = new Thickness(12), Child = headerGrid };
        header.BindResource(BackgroundProperty, "SidebarHeader");
        DockPanel.SetDock(header, Dock.Top);

        var avatarBackground = new Ellipse();
        avatarBackground.BindResource(Shape.FillProperty, "Primary");
        _userAvatar = new Grid
        {
            Width = 32,
            Height = 32,
            Children =
            {
                avatarBackground,
                new LucideIcon { Kind = LucideIconKind.User, Foreground = Brushes.White }
            }
        };
        var staff = new TextBlock { Text = "Store Staff", FontSize = 13, FontWeight = FontWeight.SemiBold };
        staff.BindResource(TextBlock.ForegroundProperty, "SidebarForeground");
        var access = new TextBlock { Text = "Inventory access", FontSize = 11 };
        access.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        var themeButton = CreateFooterButton("Theme", nameof(DashboardViewModel.ToggleThemeCommand));
        var logoutButton = CreateFooterButton("Logout", nameof(DashboardViewModel.LogoutCommand));
        _userPanel = new StackPanel
        {
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                staff, access,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { themeButton, logoutButton } }
            }
        };
        _userFooterContent = new DockPanel { Children = { _userAvatar, _userPanel } };
        _userFooter = new Border { Padding = new Thickness(12), Child = _userFooterContent };
        _userFooter.BindResource(BackgroundProperty, "SidebarHeader");
        DockPanel.SetDock(_userFooter, Dock.Bottom);

        _navList = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            ItemTemplate = new FuncDataTemplate<NavItem>((item, _) => CreateNavItem(item!), true)
        };
        _navList.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(DashboardViewModel.NavItems)));
        _navList.Bind(SelectingItemsControl.SelectedItemProperty,
            new Binding(nameof(DashboardViewModel.SelectedNavItem)) { Mode = BindingMode.TwoWay });
        _navList.BindResource(ForegroundProperty, "SidebarForeground");

        var sidebarContent = new DockPanel
        {
            Children = { header, _userFooter, new ScrollViewer { Content = _navList } }
        };
        _sidebarBorder = new Border { Width = 230, Child = sidebarContent };
        _sidebarBorder.BindResource(BackgroundProperty, "Sidebar");
        DockPanel.SetDock(_sidebarBorder, Dock.Left);

        _mainContent = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        _mainContent.Bind(ContentControl.ContentProperty, new Binding(nameof(DashboardViewModel.CurrentPage)));
        var contentBorder = new Border { Child = new Grid { Children = { _mainContent } } };
        contentBorder.BindResource(BackgroundProperty, "Muted");
        Content = new DockPanel { Children = { _sidebarBorder, contentBorder } };

        _sidebarBorder.Transitions = new Transitions
        {
            new DoubleTransition { Property = Border.WidthProperty, Duration = TimeSpan.FromSeconds(0.2) }
        };

        DataContextChanged += OnDataContextChanged;
    }

    private Button CreateFooterButton(string text, string commandPath)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 11,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        button.Bind(Button.CommandProperty, new Binding(commandPath));
        button.BindResource(ForegroundProperty, "SidebarForeground");
        return button;
    }

    private Control CreateNavItem(NavItem item)
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.RightEdgeAlignedTop };
        flyout.Items.Add(CreateFlyoutItem("Products", "InventoryProducts", LucideIconKind.Package));
        flyout.Items.Add(CreateFlyoutItem("Suppliers", "InventorySuppliers", LucideIconKind.Truck));
        flyout.Items.Add(CreateFlyoutItem("Stock Movements", "InventoryMovements", LucideIconKind.ArrowLeftRight));

        var icon = new LucideIcon { Width = 20, Height = 20 };
        icon.Bind(LucideIcon.KindProperty, new Binding(nameof(NavItem.Icon)) { Converter = new StringToIconConverter() });
        var label = new TextBlock
        {
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        label.Classes.Add("nav-label");
        label.Bind(TextBlock.TextProperty, new Binding(nameof(NavItem.Text)));
        Grid.SetColumn(label, 1);
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("20,*"),
            Children = { icon, label }
        };
        content.Classes.Add("nav-content");
        content.Classes.Set("inventory-child", item.IsChild);
        content.PointerEntered += OnNavItemPointerEntered;
        FlyoutBase.SetAttachedFlyout(content, flyout);
        return content;
    }

    private MenuItem CreateFlyoutItem(string header, string tag, LucideIconKind icon)
    {
        var item = new MenuItem
        {
            Header = header,
            Tag = tag,
            Icon = new LucideIcon { Kind = icon, Width = 16, Height = 16 }
        };
        item.Click += OnInventoryFlyoutItemClick;
        return item;
    }

    private void ConfigureStyles()
    {
        AddStyle(x => x.OfType<ListBoxItem>(),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 10)),
            new Setter(Layoutable.MarginProperty, new Thickness(4, 2)),
            new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
            new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(3, 0, 0, 0)),
            new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent),
            new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(6)),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));
        AddStyle(x => x.OfType<ListBoxItem>().Class(":pointerover").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.BackgroundProperty, new DynamicResourceExtension("NavHover")));
        AddStyle(x => x.OfType<ListBoxItem>().Class(":selected"),
            new Setter(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("Primary")),
            new Setter(TemplatedControl.FontWeightProperty, FontWeight.SemiBold));
        AddStyle(x => x.OfType<ListBoxItem>().Class(":selected").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.BackgroundProperty, new DynamicResourceExtension("NavSelected")));
        AddStyle(x => x.OfType<Button>().Class("sidebar-toggle"),
            new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
            new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            new Setter(Layoutable.WidthProperty, 32d), new Setter(Layoutable.HeightProperty, 32d),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));
        AddStyle(x => x.OfType<Border>().Class("collapsed").Descendant().OfType<ListBoxItem>(),
            new Setter(Layoutable.HeightProperty, 48d), new Setter(TemplatedControl.PaddingProperty, new Thickness(0, 0, 3, 0)),
            new Setter(Layoutable.MarginProperty, new Thickness(4, 2)),
            new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        AddStyle(x => x.OfType<Border>().Class("collapsed").Descendant().OfType<TextBlock>().Class("nav-label"),
            new Setter(Visual.IsVisibleProperty, false));
        AddStyle(x => x.OfType<Grid>().Class("nav-content").Class("inventory-child"),
            new Setter(Layoutable.MarginProperty, new Thickness(22, 0, 0, 0)), new Setter(Visual.OpacityProperty, 0.82));
        AddStyle(x => x.OfType<Grid>().Class("nav-content").Class("inventory-child").Descendant().OfType<TextBlock>().Class("nav-label"),
            new Setter(TemplatedControl.FontSizeProperty, 13d));
        AddStyle(x => x.OfType<Border>().Class("collapsed").Descendant().OfType<Grid>().Class("nav-content").Class("inventory-child"),
            new Setter(Layoutable.MarginProperty, new Thickness(0)));
    }

    private void AddStyle(Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector);
        foreach (var setter in setters) style.Setters.Add(setter);
        Styles.Add(style);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DashboardViewModel.SidebarCollapsed))
                {
                    UpdateSidebar(vm.SidebarCollapsed);
                }

                if (args.PropertyName == nameof(DashboardViewModel.IsDarkTheme))
                {
                    UpdateThemeIcon(vm.IsDarkTheme);
                }

            };
        }
    }

    private void UpdateSidebar(bool collapsed)
    {
        _sidebarBorder.Classes.Set("collapsed", collapsed);
        Grid.SetColumn(_toggleButton, collapsed ? 0 : 1);
        Grid.SetColumnSpan(_toggleButton, collapsed ? 2 : 1);
        _toggleButton.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        _toggleButton.Margin = collapsed ? new Thickness(0) : new Thickness(4, 0, 0, 0);
        _userFooter.Padding = collapsed ? new Thickness(0, 12) : new Thickness(12);
        _userFooterContent.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;

        if (collapsed)
        {
            _sidebarBorder.Width = 55;
            _toggleIcon.Kind = LucideIconKind.PanelLeftOpen;
            _brandPanel.IsVisible = false;
            _userPanel.IsVisible = false;
        }
        else
        {
            _sidebarBorder.Width = 230;
            _toggleIcon.Kind = LucideIconKind.PanelLeftClose;
            _brandPanel.IsVisible = true;
            _userPanel.IsVisible = true;
        }
    }

    private void UpdateThemeIcon(bool isDark)
    {
        // Theme toggle handled by ViewModel - we just update the toggle icon
    }

    private void OnNavItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Control { DataContext: NavItem { Tag: "InventoryProducts", IsChild: false } } control ||
            DataContext is not DashboardViewModel { SidebarCollapsed: true })
            return;

        _inventoryFlyout = FlyoutBase.GetAttachedFlyout(control);
        FlyoutBase.ShowAttachedFlyout(control);
    }

    private void OnInventoryFlyoutItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: string tag } && DataContext is DashboardViewModel viewModel)
            viewModel.OpenInventorySection(tag);
        _inventoryFlyout?.Hide();
    }

}
