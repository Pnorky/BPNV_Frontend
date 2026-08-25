using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using AvaloniaApp.Converters;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views;
using AvaloniaApp.Views.Controls;
using AvaloniaApp.Views.UI;
using LicenseType = QuestPDF.Infrastructure.LicenseType;
using QuestSettings = QuestPDF.Settings;

namespace AvaloniaApp;

public class App : Application
{
    public StoreState Store { get; private set; } = null!;
    public AuthSession AuthSession { get; private set; } = null!;
    public AuthApiClient AuthClient { get; private set; } = null!;
    public StoreApiClient StoreClient { get; private set; } = null!;

    public override void Initialize()
    {
        QuestSettings.License = LicenseType.Community;
        RequestedThemeVariant = ThemeVariant.Light;
        ConfigureResources();
        ThemeConverter.ApplyCssAsset(this, new Uri("avares://AvaloniaApp/Assets/index.css"));
        ConfigureDataTemplates();
        ConfigureStyles();
        // Start new local stores empty; existing persisted data is still loaded unchanged.
        Store = new StoreState(seedPrototypeData: false);
        AuthSession = new AuthSession();
        AuthClient = new AuthApiClient(new HttpClient
        {
            BaseAddress = ApiConfiguration.GetBaseAddress(),
            Timeout = TimeSpan.FromSeconds(15)
        }, AuthSession);
        StoreClient = new StoreApiClient(AuthClient);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = new MainViewModel(Store, AuthClient, StoreClient, AuthSession)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureResources()
    {
        var resources = new ResourceDictionary();
        resources.ThemeDictionaries[ThemeVariant.Light] = CreateThemeDictionary(
            "#8881AC", "#F3F7FF", "#3F3E50", "#F9FFFF", "#8881AC", "#DADCF3", "#D7DAEA",
            "#6C6B82", "#C3B7BE", "#3C3638", "#9E8464", "#CACBDF", "#E5E8F8", "#98ABA9",
            "#CECE9D", "#B5B9E7", "#ECF0FE", "#D2D4E7", "#E6E9F6", "#D8DCF0", "#CCD0E8");
        resources.ThemeDictionaries[ThemeVariant.Dark] = CreateThemeDictionary(
            "#A59ECB", "#1A1924", "#DDDFF6", "#242231", "#A59ECB", "#5A5771", "#242231",
            "#9D9DB1", "#343140", "#D1C7CD", "#AD957B", "#312F41", "#2B293B", "#98ABA9",
            "#CECE9D", "#B5B9E7", "#16151E", "#2B293B", "#1E1C2A", "#252433", "#2F2D3E");
        resources["StatusToColor"] = new StatusToColorConverter();
        resources["StatusToForeground"] = new StatusToForegroundConverter();
        resources["StringToIcon"] = new StringToIconConverter();
        resources["SortHeader"] = new SortHeaderConverter();
        Resources = resources;
    }

    private static ResourceDictionary CreateThemeDictionary(
        string primary, string background, string foreground, string card, string accentColor,
        string secondary, string muted, string mutedForeground, string accent, string accentForeground,
        string destructive, string border, string input, string chart3, string chart4, string chart5,
        string sidebar, string sidebarBorder, string sidebarHeader, string navHover, string navSelected)
    {
        var dictionary = new ResourceDictionary
        {
            ["SystemAccentColor"] = Color.Parse(accentColor)
        };

        void Brush(string key, string color) => dictionary[key] = new SolidColorBrush(Color.Parse(color));
        Brush("Background", background); Brush("Foreground", foreground);
        Brush("Card", card); Brush("CardForeground", foreground);
        Brush("Popover", card); Brush("PopoverForeground", foreground);
        Brush("Primary", primary); Brush("PrimaryForeground", background);
        Brush("Secondary", secondary); Brush("SecondaryForeground", foreground);
        Brush("Muted", muted); Brush("MutedForeground", mutedForeground);
        Brush("Accent", accent); Brush("AccentForeground", accentForeground);
        Brush("Destructive", destructive); Brush("DestructiveForeground", background);
        Brush("Border", border); Brush("Input", input); Brush("Ring", primary);
        Brush("Chart1", primary); Brush("Chart2", accent); Brush("Chart3", chart3);
        Brush("Chart4", chart4); Brush("Chart5", chart5);
        Brush("Sidebar", sidebar); Brush("SidebarForeground", foreground);
        Brush("SidebarPrimary", primary); Brush("SidebarPrimaryForeground", background);
        Brush("SidebarAccent", accent); Brush("SidebarAccentForeground", accentForeground);
        Brush("SidebarBorder", sidebarBorder); Brush("SidebarRing", primary); Brush("SidebarHeader", sidebarHeader);
        Brush("NavHover", navHover); Brush("NavSelected", navSelected);
        Brush("ErrorBorder", destructive); Brush("SuccessGreen", "#22C55E");
        Brush("WarningYellow", "#EAB308"); Brush("DestructiveRed", destructive);
        Brush("GrayBlue", mutedForeground);
        return dictionary;
    }

    private void ConfigureDataTemplates()
    {
        DataTemplates.Add(new FuncDataTemplate<DashboardPageViewModel>((_, _) => new DashboardView(), true));
        DataTemplates.Add(new FuncDataTemplate<SalesViewModel>((_, _) => new SalesView(), true));
        DataTemplates.Add(new FuncDataTemplate<InventoryViewModel>((_, _) => new InventoryView(), true));
        DataTemplates.Add(new FuncDataTemplate<ProductCatalogViewModel>((_, _) => new ProductCatalogView(), true));
        DataTemplates.Add(new FuncDataTemplate<AddProductViewModel>((_, _) => new AddProductView(), true));
        DataTemplates.Add(new FuncDataTemplate<StockReceivingViewModel>((_, _) => new StockReceivingView(), true));
        DataTemplates.Add(new FuncDataTemplate<BatchReceivingViewModel>((_, _) => new BatchReceivingView(), true));
        DataTemplates.Add(new FuncDataTemplate<ExcelInventoryImportViewModel>((_, _) => new ExcelInventoryImportView(), true));
        DataTemplates.Add(new FuncDataTemplate<SuppliersViewModel>((_, _) => new SuppliersView(), true));
        DataTemplates.Add(new FuncDataTemplate<ApiStockMovementsViewModel>((_, _) => new ApiStockMovementsView(), true));
        DataTemplates.Add(new FuncDataTemplate<ReportsViewModel>((_, _) => new ReportsView(), true));
    }

    private void ConfigureStyles()
    {
        Styles.Add(new FluentTheme());
        AddStyle(x => x.OfType<Border>().Class("theme-card"),
            ResourceSetter(Border.CornerRadiusProperty, "RadiusXl"),
            ResourceSetter(Border.BoxShadowProperty, "ShadowSm"));
        AddStyle(x => x.OfType<Border>().Class("theme-dialog"),
            ResourceSetter(Border.CornerRadiusProperty, "RadiusXl"),
            ResourceSetter(Border.BoxShadowProperty, "ShadowLg"));
        AddStyle(x => x.OfType<Button>().Class(":focus-visible"), ResourceSetter(Button.BorderBrushProperty, "Focus"));
        AddStyle(x => x.OfType<TextBox>().Class(":focus"), ResourceSetter(TextBox.BorderBrushProperty, "Focus"));
        AddStyle(x => x.OfType<TextBox>(),
            ResourceSetter(TextBox.SelectionBrushProperty, "Primary"),
            ResourceSetter(TextBox.SelectionForegroundBrushProperty, "PrimaryForeground"));
        AddStyle(x => x.OfType<SelectableTextBlock>(),
            ResourceSetter(SelectableTextBlock.SelectionBrushProperty, "Primary"),
            ResourceSetter(SelectableTextBlock.SelectionForegroundBrushProperty, "PrimaryForeground"));
        AddStyle(x => x.OfType<Button>().Class(":disabled"), ResourceSetter(Visual.OpacityProperty, "DisabledOpacity"));
        AddStyle(x => x.OfType<TextBox>().Class(":disabled"), ResourceSetter(Visual.OpacityProperty, "DisabledOpacity"));
        AddStyle(x => x.OfType<PagedTable>(),
            ResourceSetter(PagedTable.TableBackgroundProperty, "Card"),
            ResourceSetter(PagedTable.HeaderBackgroundProperty, "Card"),
            ResourceSetter(PagedTable.HeaderForegroundProperty, "MutedForeground"),
            ResourceSetter(PagedTable.RowBackgroundProperty, "Card"),
            ResourceSetter(PagedTable.SelectedRowBackgroundProperty, "Selected"),
            ResourceSetter(PagedTable.RowForegroundProperty, "Foreground"),
            ResourceSetter(PagedTable.TableBorderBrushProperty, "Border"),
            ResourceSetter(PagedTable.MutedForegroundProperty, "MutedForeground"),
            ResourceSetter(PagedTable.StateBackgroundProperty, "Card"),
            ResourceSetter(PagedTable.StateAccentBackgroundProperty, "Secondary"),
            ResourceSetter(PagedTable.AccentBrushProperty, "Primary"));
        AddStyle(x => x.OfType<TextBlock>().Class("h1"),
            new Setter(TextBlock.FontSizeProperty, 28d), new Setter(TextBlock.FontWeightProperty, FontWeight.Bold));
        AddStyle(x => x.OfType<TextBlock>().Class("h2"),
            new Setter(TextBlock.FontSizeProperty, 20d), new Setter(TextBlock.FontWeightProperty, FontWeight.SemiBold));
        AddStyle(x => x.OfType<TextBlock>().Class("h3"),
            new Setter(TextBlock.FontSizeProperty, 16d), new Setter(TextBlock.FontWeightProperty, FontWeight.SemiBold));
        AddStyle(x => x.OfType<Button>().Class("primary"),
            ResourceSetter(Button.BackgroundProperty, "Primary"), ResourceSetter(Button.ForegroundProperty, "PrimaryForeground"),
            new Setter(Button.CornerRadiusProperty, new CornerRadius(6)), new Setter(Button.PaddingProperty, new Thickness(16, 8)),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));
        AddStyle(x => x.OfType<Button>().Class("primary").Class(":pointerover").Template().OfType<ContentPresenter>(),
            ResourceSetter(Visual.OpacityProperty, "HoverOpacity"));
        AddStyle(x => x.OfType<Button>().Class("primary").Class(":pressed").Template().OfType<ContentPresenter>(),
            ResourceSetter(Visual.OpacityProperty, "PressedOpacity"));
        AddStyle(x => x.OfType<Button>().Class("primary").Template().OfType<ContentPresenter>(),
            ResourceSetter(Animatable.TransitionsProperty, "ThemeOpacityTransitions"));
        AddStyle(x => x.OfType<Button>().Class("table-header"),
            new Setter(Button.PaddingProperty, new Thickness(0)), new Setter(Button.BackgroundProperty, Brushes.Transparent),
            new Setter(Button.BorderThicknessProperty, new Thickness(0)),
            new Setter(Button.HorizontalContentAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Left),
            new Setter(Button.FontSizeProperty, 12d), new Setter(Button.FontWeightProperty, FontWeight.SemiBold),
            ResourceSetter(Button.ForegroundProperty, "MutedForeground"),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));
        AddStyle(x => x.OfType<Button>().Class("table-header").Class("text-end"),
            new Setter(Button.HorizontalContentAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Right));
        AddStyle(x => x.OfType<Button>().Class("table-header").Class("text-end").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.HorizontalAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Stretch),
            new Setter(ContentPresenter.HorizontalContentAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Right));
        AddStyle(x => x.OfType<Button>().Class("table-header").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.HorizontalAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Left),
            new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent),
            new Setter(ContentPresenter.CornerRadiusProperty, new CornerRadius(2)));
        AddStyle(x => x.OfType<Button>().Class("table-header").Class(":pointerover"), new Setter(Button.BackgroundProperty, Brushes.Transparent));
        AddStyle(x => x.OfType<Button>().Class("table-header").Class(":pointerover").Template().OfType<ContentPresenter>(), ResourceSetter(ContentPresenter.BackgroundProperty, "Hover"));
        AddStyle(x => x.OfType<Button>().Class("table-header").Class(":pressed"), new Setter(Button.BackgroundProperty, Brushes.Transparent));
        AddStyle(x => x.OfType<Button>().Class("table-header").Class(":pressed").Template().OfType<ContentPresenter>(), ResourceSetter(ContentPresenter.BackgroundProperty, "Pressed"));
        AddStyle(x => x.OfType<Button>().Class("table-header").Class(":focus-visible"),
            new Setter(Button.BackgroundProperty, Brushes.Transparent), new Setter(Button.BorderBrushProperty, Brushes.Transparent),
            ResourceSetter(Button.ForegroundProperty, "Foreground"));
        AddStyle(x => x.OfType<Button>().Class("table-header").Class(":focus-visible").Template().OfType<ContentPresenter>(), ResourceSetter(ContentPresenter.BackgroundProperty, "Selected"));
        AddStyle(x => x.OfType<TextBox>().Class("search"),
            new Setter(TextBox.CornerRadiusProperty, new CornerRadius(6)), new Setter(TextBox.PaddingProperty, new Thickness(12, 8)),
            ResourceSetter(TextBox.BorderBrushProperty, "Border"), ResourceSetter(TextBox.BackgroundProperty, "Card"),
            ResourceSetter(TextBox.ForegroundProperty, "Foreground"), new Setter(TextBox.PlaceholderTextProperty, "Search..."));

        AddStyle(x => x.OfType<TextBlock>().Class("form-label"),
            new Setter(TextBlock.FontSizeProperty, 11d), new Setter(TextBlock.FontWeightProperty, FontWeight.SemiBold),
            new Setter(TextBlock.LetterSpacingProperty, 0.7d),
            ResourceSetter(TextBlock.ForegroundProperty, "MutedForeground"));

        AddStyle(x => x.OfType<TextBox>().Class("form-input"),
            new Setter(TextBox.CornerRadiusProperty, new CornerRadius(7)), new Setter(TextBox.MinHeightProperty, 42d),
            new Setter(TextBox.PaddingProperty, new Thickness(13, 9)), new Setter(TextBox.BorderThicknessProperty, new Thickness(1)),
            new Setter(TextBox.VerticalContentAlignmentProperty, Avalonia.Layout.VerticalAlignment.Center),
            ResourceSetter(TextBox.BorderBrushProperty, "Border"), ResourceSetter(TextBox.BackgroundProperty, "Card"),
            ResourceSetter(TextBox.ForegroundProperty, "Foreground"),
            ResourceSetter(TextBox.PlaceholderForegroundProperty, "MutedForeground"));

        AddStyle(x => x.OfType<TextBox>().Class("form-input").Class(":pointerover"),
            ResourceSetter(TextBox.BorderBrushProperty, "Ring"));

        AddStyle(x => x.OfType<TextBox>().Class("form-input").Class(":focus"),
            new Setter(TextBox.BorderThicknessProperty, new Thickness(2)),
            ResourceSetter(TextBox.BorderBrushProperty, "Ring"));

        AddStyle(x => x.OfType<Border>().Class("number-field"),
            new Setter(Border.MinHeightProperty, 42d), new Setter(Border.CornerRadiusProperty, new CornerRadius(7)),
            new Setter(Border.BorderThicknessProperty, new Thickness(1)), new Setter(Border.ClipToBoundsProperty, true),
            ResourceSetter(Border.BorderBrushProperty, "Border"), ResourceSetter(Border.BackgroundProperty, "Card"));

        AddStyle(x => x.OfType<Border>().Class("number-field").Class(":pointerover"),
            ResourceSetter(Border.BorderBrushProperty, "Ring"));

        AddStyle(x => x.OfType<Border>().Class("number-field").Class("focused"),
            new Setter(Border.BorderThicknessProperty, new Thickness(2)),
            ResourceSetter(Border.BorderBrushProperty, "Ring"));

        AddStyle(x => x.OfType<TextBox>().Class("number-field-input"),
            new Setter(TextBox.PaddingProperty, new Thickness(13, 8)), new Setter(TextBox.FontSizeProperty, 14d),
            ResourceSetter(TextBox.ForegroundProperty, "Foreground"));

        AddStyle(x => x.OfType<Border>().Class("number-field-stepper"),
            ResourceSetter(Border.BorderBrushProperty, "Input"), ResourceSetter(Border.BackgroundProperty, "Card"));

        AddStyle(x => x.OfType<Border>().Class("number-field-step"),
            new Setter(Border.BackgroundProperty, Brushes.Transparent),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));

        AddStyle(x => x.OfType<Border>().Class("number-field-step").Class(":pointerover"),
            ResourceSetter(Border.BackgroundProperty, "Accent"));

        AddStyle(x => x.OfType<ComboBox>().Class("form-select"),
            new Setter(ComboBox.MinHeightProperty, 42d), new Setter(ComboBox.PaddingProperty, new Thickness(13, 7)),
            new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)),
            new Setter(ComboBox.VerticalContentAlignmentProperty, Avalonia.Layout.VerticalAlignment.Center),
            new Setter(ComboBox.CornerRadiusProperty, new CornerRadius(7)),
            ResourceSetter(ComboBox.BackgroundProperty, "Card"), ResourceSetter(ComboBox.BorderBrushProperty, "Border"),
            ResourceSetter(ComboBox.ForegroundProperty, "Foreground"));

        AddStyle(x => x.OfType<ComboBox>().Class("form-select").Class(":pointerover"),
            ResourceSetter(ComboBox.BorderBrushProperty, "Ring"));

        AddStyle(x => x.OfType<ComboBox>().Class("form-select").Class(":focus"),
            new Setter(ComboBox.BorderThicknessProperty, new Thickness(2)),
            ResourceSetter(ComboBox.BorderBrushProperty, "Ring"));

        AddStyle(x => x.OfType<HomisIcon>().Class("form-input-icon"),
            ResourceSetter(HomisIcon.ForegroundProperty, "MutedForeground"));

        AddStyle(x => x.OfType<Border>().Class("form-search"),
            ResourceSetter(Border.BackgroundProperty, "Secondary"),
            ResourceSetter(Border.BorderBrushProperty, "Border"));

        AddStyle(x => x.OfType<Button>().Class("secondary"),
            ResourceSetter(Button.BackgroundProperty, "Card"), ResourceSetter(Button.ForegroundProperty, "Foreground"),
            ResourceSetter(Button.BorderBrushProperty, "Border"), new Setter(Button.BorderThicknessProperty, new Thickness(1)),
            new Setter(Button.CornerRadiusProperty, new CornerRadius(4)), new Setter(Button.MinHeightProperty, 40d),
            new Setter(Button.PaddingProperty, new Thickness(16, 8)),
            new Setter(Button.HorizontalContentAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Center),
            new Setter(Button.VerticalContentAlignmentProperty, Avalonia.Layout.VerticalAlignment.Center),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));

        AddStyle(x => x.OfType<Button>().Class("ghost"),
            new Setter(Button.BackgroundProperty, Brushes.Transparent), new Setter(Button.BorderThicknessProperty, new Thickness(0)),
            ResourceSetter(Button.ForegroundProperty, "Foreground"), new Setter(Button.CornerRadiusProperty, new CornerRadius(4)),
            new Setter(Button.MinHeightProperty, 40d), new Setter(Button.PaddingProperty, new Thickness(12, 9)),
            new Setter(Button.HorizontalContentAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Center),
            new Setter(Button.VerticalContentAlignmentProperty, Avalonia.Layout.VerticalAlignment.Center),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));

        AddStyle(x => x.OfType<Button>().Class("ghost").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));

        AddStyle(x => x.OfType<Button>().Class("ghost").Class(":pointerover"),
            new Setter(Button.BackgroundProperty, Brushes.Transparent),
            ResourceSetter(Button.ForegroundProperty, "Primary"));

        AddStyle(x => x.OfType<Button>().Class("ghost").Class(":pointerover").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));

        AddStyle(x => x.OfType<Button>().Class("ghost").Class(":pressed"),
            new Setter(Button.BackgroundProperty, Brushes.Transparent),
            ResourceSetter(Button.ForegroundProperty, "Primary"));

        AddStyle(x => x.OfType<Button>().Class("ghost").Class(":pressed").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));

        AddStyle(x => x.OfType<Button>().Class("danger"),
            new Setter(Button.BackgroundProperty, Brushes.Transparent), new Setter(Button.BorderThicknessProperty, new Thickness(0)),
            ResourceSetter(Button.ForegroundProperty, "DestructiveRed"), new Setter(Button.CornerRadiusProperty, new CornerRadius(4)),
            new Setter(Button.MinHeightProperty, 40d), new Setter(Button.PaddingProperty, new Thickness(12, 9)),
            new Setter(Button.HorizontalContentAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Center),
            new Setter(Button.VerticalContentAlignmentProperty, Avalonia.Layout.VerticalAlignment.Center),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));

        AddStyle(x => x.OfType<Button>().Class("danger").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));

        AddStyle(x => x.OfType<Button>().Class("danger").Class(":pointerover"),
            new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.Parse("#FEE2E2"))),
            ResourceSetter(Button.ForegroundProperty, "DestructiveRed"));

        AddStyle(x => x.OfType<Button>().Class("danger").Class(":pointerover").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));

        AddStyle(x => x.OfType<Button>().Class("danger").Class(":pressed"),
            ResourceSetter(Button.BackgroundProperty, "Destructive"),
            ResourceSetter(Button.ForegroundProperty, "DestructiveForeground"));

        AddStyle(x => x.OfType<Button>().Class("danger").Class(":pressed").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));
    }

    private void AddStyle(Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector);
        foreach (var setter in setters) style.Setters.Add(setter);
        Styles.Add(style);
    }

    private static Setter ResourceSetter(AvaloniaProperty property, string key) =>
        new(property, new DynamicResourceExtension(key));
}
