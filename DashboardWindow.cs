using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaApp.Converters;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;
using Lucide.Avalonia;

namespace AvaloniaApp.Views;

public class DashboardWindow : Window
{
    private readonly Flyout _inventoryFlyout;
    private readonly Border _inventoryFlyoutContent;
    private readonly TranslateTransform _inventoryFlyoutOffset;
    private readonly Border _sidebarBorder;
    private readonly StackPanel _brandPanel;
    private readonly TextBlock _brandSubtitle;
    private readonly Button _toggleButton;
    private readonly AnimatedMenuIcon _toggleIcon;
    private readonly Border _userFooter;
    private readonly DockPanel _userFooterContent;
    private readonly Avatar _userAvatar;
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

        _inventoryFlyoutOffset = new TranslateTransform(0, -4);
        _inventoryFlyoutContent = CreateInventoryFlyoutContent();
        _inventoryFlyoutContent.RenderTransform = _inventoryFlyoutOffset;
        _inventoryFlyoutContent.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(120) },
            new DoubleTransition { Property = TranslateTransform.YProperty, Duration = TimeSpan.FromMilliseconds(120) }
        };
        _inventoryFlyout = new Flyout
        {
            Placement = PlacementMode.RightEdgeAlignedTop,
            HorizontalOffset = 14,
            Content = _inventoryFlyoutContent
        };
        _inventoryFlyout.FlyoutPresenterClasses.Add("inventory-flyout");

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

        _toggleIcon = new AnimatedMenuIcon { Width = 20, Height = 20 };
        _toggleIcon.BindResource(AnimatedMenuIcon.ForegroundProperty, "SidebarForeground");
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

        _userAvatar = new Avatar { AvatarSize = 32 };
        _userAvatar.Bind(Avatar.FallbackProperty, new Binding(nameof(DashboardViewModel.UserInitials)));
        var staff = new TextBlock { FontSize = 13, FontWeight = FontWeight.SemiBold };
        staff.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.UserDisplayName)));
        staff.BindResource(TextBlock.ForegroundProperty, "SidebarForeground");
        var access = new TextBlock { FontSize = 11 };
        access.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.RoleDisplay)));
        access.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        var themeSwitch = new ShadcnSwitch();
        themeSwitch.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(DashboardViewModel.IsDarkTheme)) { Mode = BindingMode.OneWay });
        themeSwitch.Bind(Button.CommandProperty, new Binding(nameof(DashboardViewModel.ToggleThemeCommand)));
        var themeControl = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { new TextBlock { Text = "Theme", FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, themeSwitch }
        };
        var logoutButton = CreateFooterButton("Logout", nameof(DashboardViewModel.LogoutCommand));
        _userPanel = new StackPanel
        {
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                staff, access,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { themeControl, logoutButton } }
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
        var icon = new LucideIcon
        {
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
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
            ColumnDefinitions = new ColumnDefinitions("24,*"),
            Children = { icon, label }
        };
        content.Classes.Add("nav-content");
        content.Classes.Set("inventory-child", item.IsChild);
        content.AddHandler(InputElement.PointerPressedEvent, (_, eventArgs) =>
        {
            if (DataContext is not DashboardViewModel viewModel) return;
            if (!item.IsChild && item.Tag == "InventoryProducts" && viewModel.SidebarCollapsed)
            {
                ShowInventoryFlyout(content);
                eventArgs.Handled = true;
                return;
            }

            viewModel.SelectNavItem(item);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        return content;
    }

    private Border CreateInventoryFlyoutContent()
    {
        var content = new Border
        {
            Width = 232,
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    CreateInventoryFlyoutItem("Products", "InventoryProducts", LucideIconKind.Package),
                    CreateInventoryFlyoutItem("Add Product", "InventoryAddProduct", LucideIconKind.Package),
                    CreateInventoryFlyoutItem("Receive Stock", "InventoryReceiveStock", LucideIconKind.Boxes),
                    CreateInventoryFlyoutItem("Import Excel", "InventoryImport", LucideIconKind.FileSpreadsheet),
                    CreateInventoryFlyoutItem("Suppliers", "InventorySuppliers", LucideIconKind.Truck),
                    CreateInventoryFlyoutItem("Stock Movements", "InventoryMovements", LucideIconKind.ArrowLeftRight)
                }
            }
        };
        content.Classes.Add("theme-card");
        content.BindResource(Border.BackgroundProperty, "Card");
        content.BindResource(Border.BorderBrushProperty, "Border");
        return content;
    }

    private Button CreateInventoryFlyoutItem(string text, string tag, LucideIconKind icon)
    {
        var iconControl = new LucideIcon
        {
            Kind = icon,
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var label = new TextBlock
        {
            Text = text,
            Margin = new Thickness(10, 0, 0, 0),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);

        var item = new Button
        {
            Tag = tag,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("24,*"),
                Children = { iconControl, label }
            }
        };
        item.Classes.Add("inventory-flyout-item");
        item.BindResource(ForegroundProperty, "Foreground");
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
        AddStyle(x => x.OfType<FlyoutPresenter>().Class("inventory-flyout"),
            new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
            new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(0)));
        AddStyle(x => x.OfType<Button>().Class("inventory-flyout-item"),
            new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
            new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(7)),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 8)),
            new Setter(Layoutable.MinHeightProperty, 40d),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));
        AddStyle(x => x.OfType<Button>().Class("inventory-flyout-item").Class(":pointerover"),
            new Setter(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("NavHover")));
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
            _toggleIcon.IsOpen = true;
            _brandPanel.IsVisible = false;
            _userPanel.IsVisible = false;
        }
        else
        {
            _inventoryFlyout.Hide();
            _sidebarBorder.Width = 230;
            _toggleIcon.IsOpen = false;
            _brandPanel.IsVisible = true;
            _userPanel.IsVisible = true;
        }
    }

    private void UpdateThemeIcon(bool isDark)
    {
        // Theme toggle handled by ViewModel - we just update the toggle icon
    }

    private void ShowInventoryFlyout(Control control)
    {
        _inventoryFlyoutContent.Opacity = 0;
        _inventoryFlyoutOffset.Y = -4;
        _inventoryFlyout.ShowAt(control);
        Dispatcher.UIThread.Post(() =>
        {
            _inventoryFlyoutContent.Opacity = 1;
            _inventoryFlyoutOffset.Y = 0;
        });
    }

    private void OnInventoryFlyoutItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: string tag } && DataContext is DashboardViewModel viewModel)
            viewModel.OpenInventorySection(tag);
        _inventoryFlyout.Hide();
    }

}
