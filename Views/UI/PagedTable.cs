using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AvaloniaApp.Views.UI;

public class PagedTable : UserControl
{
    private static readonly Cursor SelectableRowCursor = new(StandardCursorType.Hand);
    private static bool IsMobilePlatform => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<PagedTable, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<object?> SelectedItemProperty = AvaloniaProperty.Register<PagedTable, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<bool> IsSelectableProperty = AvaloniaProperty.Register<PagedTable, bool>(nameof(IsSelectable));
    public static readonly StyledProperty<int> PageSizeProperty = AvaloniaProperty.Register<PagedTable, int>(nameof(PageSize), 10, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<int> ExternalTotalCountProperty = AvaloniaProperty.Register<PagedTable, int>(nameof(ExternalTotalCount), -1);
    public static readonly StyledProperty<int> ExternalPageProperty = AvaloniaProperty.Register<PagedTable, int>(nameof(ExternalPage), 1, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<ICommand?> ExternalPreviousCommandProperty = AvaloniaProperty.Register<PagedTable, ICommand?>(nameof(ExternalPreviousCommand));
    public static readonly StyledProperty<ICommand?> ExternalNextCommandProperty = AvaloniaProperty.Register<PagedTable, ICommand?>(nameof(ExternalNextCommand));
    public static readonly StyledProperty<double> MinTableWidthProperty = AvaloniaProperty.Register<PagedTable, double>(nameof(MinTableWidth), 720);
    public static readonly StyledProperty<string> ItemNameProperty = AvaloniaProperty.Register<PagedTable, string>(nameof(ItemName), "record");
    public static readonly StyledProperty<string> ItemNamePluralProperty = AvaloniaProperty.Register<PagedTable, string>(nameof(ItemNamePlural), "records");
    public static readonly StyledProperty<bool> IsFilteredProperty = AvaloniaProperty.Register<PagedTable, bool>(nameof(IsFiltered));
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<PagedTable, bool>(nameof(IsLoading));
    public static readonly StyledProperty<string?> ErrorMessageProperty = AvaloniaProperty.Register<PagedTable, string?>(nameof(ErrorMessage));
    public static readonly StyledProperty<bool> IsFirstTimeSetupProperty = AvaloniaProperty.Register<PagedTable, bool>(nameof(IsFirstTimeSetup));
    public static readonly StyledProperty<string?> EmptyActionTextProperty = AvaloniaProperty.Register<PagedTable, string?>(nameof(EmptyActionText));
    public static readonly StyledProperty<ICommand?> EmptyActionCommandProperty = AvaloniaProperty.Register<PagedTable, ICommand?>(nameof(EmptyActionCommand));
    public static readonly StyledProperty<ICommand?> ClearFiltersCommandProperty = AvaloniaProperty.Register<PagedTable, ICommand?>(nameof(ClearFiltersCommand));
    public static readonly StyledProperty<ICommand?> RetryCommandProperty = AvaloniaProperty.Register<PagedTable, ICommand?>(nameof(RetryCommand));
    public static readonly StyledProperty<IBrush?> TableBackgroundProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(TableBackground));
    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(HeaderBackground));
    public static readonly StyledProperty<IBrush?> HeaderForegroundProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(HeaderForeground));
    public static readonly StyledProperty<IBrush?> RowBackgroundProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(RowBackground));
    public static readonly StyledProperty<IBrush?> SelectedRowBackgroundProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(SelectedRowBackground));
    public static readonly StyledProperty<IBrush?> RowForegroundProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(RowForeground));
    public static readonly StyledProperty<IBrush?> TableBorderBrushProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(TableBorderBrush));
    public static readonly StyledProperty<IBrush?> MutedForegroundProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(MutedForeground));
    public static readonly StyledProperty<IBrush?> StateBackgroundProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(StateBackground));
    public static readonly StyledProperty<IBrush?> StateAccentBackgroundProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(StateAccentBackground));
    public static readonly StyledProperty<IBrush?> AccentBrushProperty = AvaloniaProperty.Register<PagedTable, IBrush?>(nameof(AccentBrush));

    private readonly List<object> _baseItems = [];
    private readonly List<object> _sortedItems = [];
    private readonly List<SortDescriptor> _sorts = [];
    private readonly Dictionary<Button, PagedTableColumn> _headerButtons = [];
    private INotifyCollectionChanged? _observableSource;
    private int _currentPage = 1;
    private bool _rebuildScheduled;
    private bool _updatingPageSize;

    private readonly StackPanel _backgroundPanel;
    private readonly Grid HeaderGrid;
    private readonly StackPanel RowsPanel;
    private readonly Border StatePanel;
    private readonly TextBlock StateIcon;
    private readonly SkeletonBox LoadingIndicator;
    private readonly TextBlock StateTitle;
    private readonly TextBlock StateMessage;
    private readonly Button StateActionButton;
    private readonly Border FooterBorder;
    private readonly Grid FooterGrid;
    private readonly StackPanel PageSizePanel;
    private readonly ComboBox PageSizeSelector;
    private readonly StackPanel PageSummaryPanel;
    private readonly TextBlock PageSummaryText;
    private readonly TextBlock SortSummaryText;
    private readonly StackPanel PaginationPanel;
    private readonly Button PreviousButton;
    private readonly Button NextButton;

    public PagedTable()
    {
        HeaderGrid = new Grid
        {
            Margin = new Thickness(0),
            MinHeight = 40,
            ColumnSpacing = 10,
            IsVisible = !IsMobilePlatform,
            Background = HeaderBackground
        };

        RowsPanel = new StackPanel();

        _backgroundPanel = new StackPanel
        {
            MinWidth = IsMobilePlatform ? 0 : MinTableWidth,
            Background = TableBackground,
            Children = { HeaderGrid, RowsPanel }
        };

        StateIcon = new TextBlock
        {
            Text = "\u25CB",
            FontSize = 26,
            Foreground = AccentBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        LoadingIndicator = new SkeletonBox
        {
            Width = 30,
            Height = 4,
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        StateTitle = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = RowForeground,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        StateMessage = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = MutedForeground,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        StateActionButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
            IsVisible = false
        };
        StateActionButton.Classes.Add("outline");
        StateActionButton.Click += OnStateActionClick;

        var stateStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 420,
            Spacing = 10,
            Margin = new Thickness(32),
            Children =
            {
                new Border
                {
                    Width = 54,
                    Height = 54,
                    CornerRadius = new CornerRadius(27),
                    Background = this.FindResource("SystemControlBackgroundBaseLowBrush") as IBrush ?? Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new Grid
                    {
                        Children =
                        {
                            new Border
                            {
                                CornerRadius = new CornerRadius(27),
                                Background = StateAccentBackground
                            },
                            StateIcon,
                            LoadingIndicator
                        }
                    }
                },
                StateTitle,
                StateMessage,
                StateActionButton
            }
        };

        StatePanel = new Border
        {
            Background = this.FindResource("SystemControlBackgroundAltHighBrush") as IBrush ?? Brushes.Transparent,
            IsVisible = false,
            MinHeight = 240,
            Child = new Grid
            {
                Background = StateBackground,
                Children = { stateStack }
            }
        };

        var contentArea = new Grid
        {
            Children =
            {
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility = IsMobilePlatform ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Background = this.FindResource("SystemControlBackgroundAltHighBrush") as IBrush ?? Brushes.Transparent,
                    Content = _backgroundPanel
                },
                StatePanel
            }
        };

        PageSummaryText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };

        SortSummaryText = new TextBlock
        {
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = MutedForeground,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.7
        };

        PageSummaryPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { PageSummaryText, SortSummaryText }
        };

        PageSizeSelector = new ComboBox
        {
            MinHeight = 34,
            Padding = new Thickness(10, 4)
        };
        PageSizeSelector.Classes.Add("form-select");
        PageSizeSelector.SelectionChanged += OnPageSizeChanged;

        PageSizePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Rows",
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.7,
                    Foreground = MutedForeground
                },
                PageSizeSelector
            }
        };

        PreviousButton = new Button
        {
            Content = "Previous",
            MinHeight = 34,
            Padding = new Thickness(10, 5),
            FontSize = 12
        };
        PreviousButton.Classes.Add("ghost");
        PreviousButton.Click += OnPreviousClick;

        NextButton = new Button
        {
            Content = "Next",
            MinHeight = 34,
            Padding = new Thickness(10, 5),
            FontSize = 12
        };
        NextButton.Classes.Add("ghost");
        NextButton.Click += OnNextClick;

        PaginationPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { PreviousButton, NextButton }
        };

        FooterGrid = new Grid();
        FooterGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto) { MinWidth = 0 });
        FooterGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        FooterGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto) { MinWidth = 0 });
        FooterGrid.Children.Add(PageSizePanel);
        FooterGrid.Children.Add(PageSummaryPanel);
        FooterGrid.Children.Add(PaginationPanel);
        Grid.SetColumn(PageSummaryPanel, 1);
        Grid.SetColumn(PaginationPanel, 2);

        PageSizePanel.HorizontalAlignment = HorizontalAlignment.Left;
        PageSizePanel.VerticalAlignment = VerticalAlignment.Center;
        PageSummaryPanel.HorizontalAlignment = HorizontalAlignment.Center;
        PageSummaryPanel.VerticalAlignment = VerticalAlignment.Center;
        PaginationPanel.HorizontalAlignment = HorizontalAlignment.Right;
        PaginationPanel.VerticalAlignment = VerticalAlignment.Center;

        FooterBorder = new Border
        {
            Padding = new Thickness(12, 8),
            Background = TableBackground ?? this.FindResource("SystemControlBackgroundAltHighBrush") as IBrush ?? Brushes.Transparent,
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = TableBorderBrush,
            Child = FooterGrid
        };

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children = { contentArea, FooterBorder }
        };
        Grid.SetRow(FooterBorder, 1);

        Content = rootGrid;

        Columns.CollectionChanged += (_, _) => ScheduleRebuildTable();
        PageSizeSelector.ItemsSource = PageSizeOptions;
        PageSizeSelector.SelectedItem = PageSize;
        SizeChanged += (_, _) =>
        {
            ApplyTableWidth();
            ApplyMobileFooterLayout();
        };
        ApplyTableWidth();
        Refresh();
    }

    public AvaloniaList<PagedTableColumn> Columns { get; } = [];
    public ObservableCollection<object> PageItems { get; } = [];
    public IReadOnlyList<int> PageSizeOptions { get; } = [5, 10, 20, 50];

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public bool IsSelectable { get => GetValue(IsSelectableProperty); set => SetValue(IsSelectableProperty, value); }
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }
    public int ExternalTotalCount { get => GetValue(ExternalTotalCountProperty); set => SetValue(ExternalTotalCountProperty, value); }
    public int ExternalPage { get => GetValue(ExternalPageProperty); set => SetValue(ExternalPageProperty, value); }
    public ICommand? ExternalPreviousCommand { get => GetValue(ExternalPreviousCommandProperty); set => SetValue(ExternalPreviousCommandProperty, value); }
    public ICommand? ExternalNextCommand { get => GetValue(ExternalNextCommandProperty); set => SetValue(ExternalNextCommandProperty, value); }
    public double MinTableWidth { get => GetValue(MinTableWidthProperty); set => SetValue(MinTableWidthProperty, value); }
    public string ItemName { get => GetValue(ItemNameProperty); set => SetValue(ItemNameProperty, value); }
    public string ItemNamePlural { get => GetValue(ItemNamePluralProperty); set => SetValue(ItemNamePluralProperty, value); }
    public bool IsFiltered { get => GetValue(IsFilteredProperty); set => SetValue(IsFilteredProperty, value); }
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public bool IsFirstTimeSetup { get => GetValue(IsFirstTimeSetupProperty); set => SetValue(IsFirstTimeSetupProperty, value); }
    public string? EmptyActionText { get => GetValue(EmptyActionTextProperty); set => SetValue(EmptyActionTextProperty, value); }
    public ICommand? EmptyActionCommand { get => GetValue(EmptyActionCommandProperty); set => SetValue(EmptyActionCommandProperty, value); }
    public ICommand? ClearFiltersCommand { get => GetValue(ClearFiltersCommandProperty); set => SetValue(ClearFiltersCommandProperty, value); }
    public ICommand? RetryCommand { get => GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }
    public IBrush? TableBackground { get => GetValue(TableBackgroundProperty); set => SetValue(TableBackgroundProperty, value); }
    public IBrush? HeaderBackground { get => GetValue(HeaderBackgroundProperty); set => SetValue(HeaderBackgroundProperty, value); }
    public IBrush? HeaderForeground { get => GetValue(HeaderForegroundProperty); set => SetValue(HeaderForegroundProperty, value); }
    public IBrush? RowBackground { get => GetValue(RowBackgroundProperty); set => SetValue(RowBackgroundProperty, value); }
    public IBrush? SelectedRowBackground { get => GetValue(SelectedRowBackgroundProperty); set => SetValue(SelectedRowBackgroundProperty, value); }
    public IBrush? RowForeground { get => GetValue(RowForegroundProperty); set => SetValue(RowForegroundProperty, value); }
    public IBrush? TableBorderBrush { get => GetValue(TableBorderBrushProperty); set => SetValue(TableBorderBrushProperty, value); }
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    public IBrush? StateBackground { get => GetValue(StateBackgroundProperty); set => SetValue(StateBackgroundProperty, value); }
    public IBrush? StateAccentBackground { get => GetValue(StateAccentBackgroundProperty); set => SetValue(StateAccentBackgroundProperty, value); }
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty) AttachItemsSource(change.GetNewValue<IEnumerable?>());
        else if (change.Property == PageSizeProperty)
        {
            _currentPage = 1;
            if (ExternalTotalCount >= 0) SetCurrentValue(ExternalPageProperty, 1);
            _updatingPageSize = true;
            PageSizeSelector.SelectedItem = PageSize;
            _updatingPageSize = false;
            Refresh();
        }
        else if (change.Property == ExternalTotalCountProperty || change.Property == ExternalPageProperty ||
                 change.Property == ExternalPreviousCommandProperty || change.Property == ExternalNextCommandProperty)
            Refresh();
        else if (change.Property == MinTableWidthProperty)
            ApplyTableWidth();
        else if (change.Property == SelectedItemProperty || change.Property == IsSelectableProperty)
            RenderRows();
        else if (change.Property == IsFilteredProperty || change.Property == IsLoadingProperty ||
                 change.Property == ErrorMessageProperty || change.Property == IsFirstTimeSetupProperty ||
                 change.Property == EmptyActionTextProperty || change.Property == EmptyActionCommandProperty ||
                 change.Property == ClearFiltersCommandProperty || change.Property == RetryCommandProperty ||
                 change.Property == ItemNameProperty || change.Property == ItemNamePluralProperty) RefreshState();
        else if (change.Property == TableBackgroundProperty || change.Property == HeaderBackgroundProperty ||
                 change.Property == HeaderForegroundProperty || change.Property == RowBackgroundProperty ||
                  change.Property == RowForegroundProperty || change.Property == SelectedRowBackgroundProperty ||
                  change.Property == TableBorderBrushProperty ||
                 change.Property == MutedForegroundProperty || change.Property == StateBackgroundProperty ||
                 change.Property == StateAccentBackgroundProperty || change.Property == AccentBrushProperty)
        {
            if (change.Property == TableBackgroundProperty)
            {
                _backgroundPanel.Background = TableBackground;
                FooterBorder.Background = TableBackground ?? this.FindResource("SystemControlBackgroundAltHighBrush") as IBrush ?? Brushes.Transparent;
            }
            if (change.Property == HeaderBackgroundProperty) HeaderGrid.Background = HeaderBackground;
            if (change.Property == TableBorderBrushProperty)
            {
                FooterBorder.BorderBrush = TableBorderBrush;
                PreviousButton.BorderBrush = TableBorderBrush;
                NextButton.BorderBrush = TableBorderBrush;
            }
            ScheduleRebuildTable();
        }
    }

    private void AttachItemsSource(IEnumerable? source)
    {
        if (_observableSource is not null) _observableSource.CollectionChanged -= OnSourceCollectionChanged;
        _observableSource = source as INotifyCollectionChanged;
        if (_observableSource is not null) _observableSource.CollectionChanged += OnSourceCollectionChanged;
        SnapshotSource();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => SnapshotSource();

    private void SnapshotSource()
    {
        SelectedItem = null;
        _baseItems.Clear();
        if (ItemsSource is not null)
            foreach (var item in ItemsSource)
                if (item is not null) _baseItems.Add(item);
        if (ExternalTotalCount < 0) _currentPage = 1;
        ApplySort();
        Refresh();
    }

    private void RebuildTable()
    {
        if (HeaderGrid is null) return;
        HeaderGrid.Children.Clear();
        HeaderGrid.ColumnDefinitions.Clear();
        _headerButtons.Clear();
        for (var index = 0; index < Columns.Count; index++)
        {
            var definition = Columns[index];
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(definition.Width));
            if (!definition.IsSortable && definition.HorizontalAlignment is HorizontalAlignment.Center or HorizontalAlignment.Right)
            {
                var textAlignment = definition.HorizontalAlignment == HorizontalAlignment.Center
                    ? TextAlignment.Center
                    : TextAlignment.Right;
                var header = new TextBlock
                {
                    Text = definition.Header,
                    Foreground = HeaderForeground,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    TextAlignment = textAlignment,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 10)
                };
                Grid.SetColumn(header, index);
                HeaderGrid.Children.Add(header);
                continue;
            }
            var button = new Button
            {
                Content = BuildHeaderContent(definition, definition.Header),
                Foreground = HeaderForeground,
                IsEnabled = definition.IsSortable,
                Padding = new Thickness(12, 10),
                HorizontalAlignment = definition.HorizontalAlignment == HorizontalAlignment.Right
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Stretch,
                HorizontalContentAlignment = definition.HorizontalAlignment
            };
            button.Classes.Add("table-header");
            if (definition.HorizontalAlignment == HorizontalAlignment.Right)
                button.Classes.Add("text-end");
            button.Click += OnHeaderClick;
            Grid.SetColumn(button, index);
            HeaderGrid.Children.Add(button);
            _headerButtons[button] = definition;
        }
        RenderRows();
        UpdateColumnHeaders();
    }

    private void ScheduleRebuildTable()
    {
        if (_rebuildScheduled) return;

        _rebuildScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildScheduled = false;
            RebuildTable();
        }, DispatcherPriority.Background);
    }

    private void OnHeaderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !_headerButtons.TryGetValue(button, out var column) || !column.IsSortable) return;
        var existing = _sorts.FindIndex(sort => ReferenceEquals(sort.Column, column));
        if (existing < 0) _sorts.Add(new SortDescriptor(column, true));
        else if (_sorts[existing].Ascending) _sorts[existing] = _sorts[existing] with { Ascending = false };
        else _sorts.RemoveAt(existing);
        _currentPage = 1;
        ApplySort();
        Refresh();
    }

    private void ApplySort()
    {
        _sortedItems.Clear();
        if (_sorts.Count == 0) { _sortedItems.AddRange(_baseItems); return; }
        IOrderedEnumerable<object>? ordered = null;
        foreach (var sort in _sorts)
        {
            Func<object, object?> selector = sort.Column.GetSortValue;
            ordered = ordered is null
                ? sort.Ascending ? _baseItems.OrderBy(selector, ValueComparer.Instance) : _baseItems.OrderByDescending(selector, ValueComparer.Instance)
                : sort.Ascending ? ordered.ThenBy(selector, ValueComparer.Instance) : ordered.ThenByDescending(selector, ValueComparer.Instance);
        }
        if (ordered is not null) _sortedItems.AddRange(ordered);
    }

    private void Refresh()
    {
        var pageSize = Math.Max(1, PageSize);
        var externalPaging = ExternalTotalCount >= 0;
        var totalCount = externalPaging ? ExternalTotalCount : _sortedItems.Count;
        var page = externalPaging ? Math.Max(1, ExternalPage) : _currentPage;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (!externalPaging && _currentPage > totalPages) _currentPage = totalPages;
        page = externalPaging ? Math.Min(page, totalPages) : _currentPage;
        PageItems.Clear();
        var visibleItems = externalPaging ? _sortedItems : _sortedItems.Skip((page - 1) * pageSize).Take(pageSize);
        foreach (var item in visibleItems) PageItems.Add(item);
        RenderRows();
        PageSummaryText.Text = totalCount == 0 ? "No items" :
            $"Showing {((page - 1) * pageSize) + 1}-{Math.Min((page - 1) * pageSize + PageItems.Count, totalCount)} of {totalCount}";
        SortSummaryText.Text = _sorts.Count == 0 ? "" :
            $"Sorted by {string.Join(", ", _sorts.Select((sort, index) => $"{sort.Column.Header} {(sort.Ascending ? "\u2191" : "\u2193")} {index + 1}"))}";
        PreviousButton.IsEnabled = page > 1 && (!externalPaging || ExternalPreviousCommand?.CanExecute(null) == true);
        NextButton.IsEnabled = page < totalPages && (!externalPaging || ExternalNextCommand?.CanExecute(null) == true);
        UpdateColumnHeaders();
        RefreshState();
    }

    private void RenderRows()
    {
        if (RowsPanel is null) return;
        RowsPanel.Children.Clear();
        foreach (var item in PageItems)
        {
            if (IsMobilePlatform)
            {
                RowsPanel.Children.Add(BuildMobileRow(item));
                continue;
            }

            var row = new Grid { ColumnSpacing = 10 };
            for (var index = 0; index < Columns.Count; index++)
            {
                var column = Columns[index];
                row.ColumnDefinitions.Add(new ColumnDefinition(column.Width));
                var cell = BuildCell(column, item, false);
                Grid.SetColumn(cell, index);
                row.Children.Add(cell);
            }
            var rowBorder = new Border
            {
                Child = row,
                Background = Equals(item, SelectedItem) ? SelectedRowBackground ?? RowBackground : RowBackground,
                Padding = new Thickness(0, 10),
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = TableBorderBrush ?? new SolidColorBrush(Color.FromArgb(40, 128, 128, 128))
            };
            if (IsSelectable)
            {
                rowBorder.Cursor = SelectableRowCursor;
                rowBorder.Tag = item;
                rowBorder.PointerPressed += OnRowPointerPressed;
            }
            RowsPanel.Children.Add(rowBorder);
        }
    }

    private Border BuildMobileRow(object item)
    {
        var fields = new StackPanel { Spacing = 10 };
        foreach (var column in Columns)
        {
            var label = new TextBlock
            {
                Text = column.Header.ToUpperInvariant(),
                Foreground = MutedForeground,
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = 0.6,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = column.HorizontalAlignment == HorizontalAlignment.Right ? TextAlignment.Right : TextAlignment.Left,
                HorizontalAlignment = column.HorizontalAlignment,
                VerticalAlignment = VerticalAlignment.Center
            };
            var cell = BuildCell(column, item, true);
            var field = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("96,*"),
                ColumnSpacing = 12,
                Children = { label, cell }
            };
            Grid.SetColumn(cell, 1);
            fields.Children.Add(field);
        }

        var rowBorder = new Border
        {
            Child = fields,
            Background = Equals(item, SelectedItem) ? SelectedRowBackground ?? RowBackground : RowBackground,
            Padding = new Thickness(12, 10),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = TableBorderBrush ?? new SolidColorBrush(Color.FromArgb(40, 128, 128, 128))
        };
        if (IsSelectable)
        {
            rowBorder.Cursor = SelectableRowCursor;
            rowBorder.Tag = item;
            rowBorder.PointerPressed += OnRowPointerPressed;
        }
        return rowBorder;
    }

    private Control BuildCell(PagedTableColumn column, object item, bool wrap) => column.CellTemplate is not null
        ? new ContentControl
        {
            Content = item,
            ContentTemplate = column.CellTemplate,
            ClipToBounds = true,
            Margin = wrap ? new Thickness(0) : new Thickness(8, 0),
            HorizontalAlignment = column.HorizontalAlignment,
            VerticalAlignment = VerticalAlignment.Center
        }
        : new TextBlock
        {
            Text = Convert.ToString(column.GetCellValue(item), CultureInfo.CurrentCulture) ?? "",
            Foreground = RowForeground,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = wrap ? Avalonia.Media.TextTrimming.None : Avalonia.Media.TextTrimming.CharacterEllipsis,
            ClipToBounds = true,
            Margin = wrap ? new Thickness(0) : new Thickness(8, 0),
            HorizontalAlignment = column.HorizontalAlignment,
            TextAlignment = column.HorizontalAlignment == HorizontalAlignment.Right ? TextAlignment.Right : TextAlignment.Left
        };

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Tag: { } item })
        {
            if (Equals(item, SelectedItem))
            {
                SelectedItem = null;
                Dispatcher.UIThread.Post(() => SelectedItem = item);
            }
            else
            {
                SelectedItem = item;
            }
            e.Handled = true;
        }
    }

    private void UpdateColumnHeaders()
    {
        foreach (var pair in _headerButtons)
        {
            var index = _sorts.FindIndex(sort => ReferenceEquals(sort.Column, pair.Value));
            var text = index < 0 ? pair.Value.Header :
                $"{pair.Value.Header} {(_sorts[index].Ascending ? "\u2191" : "\u2193")} {index + 1}";
            pair.Key.Content = BuildHeaderContent(pair.Value, text);
        }
    }

    private static object BuildHeaderContent(PagedTableColumn column, string text) =>
        column.HorizontalAlignment == HorizontalAlignment.Right
            ? new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Right,
                HorizontalAlignment = HorizontalAlignment.Right
            }
            : text;

    private void RefreshState()
    {
        if (StatePanel is null) return;
        var show = IsLoading || ErrorMessage is not null || IsFirstTimeSetup || _baseItems.Count == 0;
        StatePanel.IsVisible = show;
        if (!show) return;
        ICommand? action;
        if (IsLoading)
        {
            StateIcon.IsVisible = false; LoadingIndicator.IsVisible = true;
            StateTitle.Text = $"Loading {ItemNamePlural}"; StateMessage.Text = $"Please wait while the latest {ItemNamePlural} are retrieved.";
            action = null; StateActionButton.Content = null;
        }
        else if (ErrorMessage is not null)
        {
            StateIcon.IsVisible = true; StateIcon.Text = "!"; LoadingIndicator.IsVisible = false;
            StateTitle.Text = $"Unable to load {ItemNamePlural}"; StateMessage.Text = ErrorMessage;
            action = RetryCommand; StateActionButton.Content = "Try Again";
        }
        else if (IsFirstTimeSetup)
        {
            StateIcon.IsVisible = true; StateIcon.Text = "+"; LoadingIndicator.IsVisible = false;
            StateTitle.Text = "Let's get you set up"; StateMessage.Text = $"Create your first {ItemName} to start using this area.";
            action = EmptyActionCommand; StateActionButton.Content = EmptyActionText;
        }
        else if (IsFiltered)
        {
            StateIcon.IsVisible = true; StateIcon.Text = "?"; LoadingIndicator.IsVisible = false;
            StateTitle.Text = "No matching results"; StateMessage.Text = "Try a different search or clear the current filters.";
            action = ClearFiltersCommand; StateActionButton.Content = "Clear Filters";
        }
        else
        {
            StateIcon.IsVisible = true; StateIcon.Text = "\u25A1"; LoadingIndicator.IsVisible = false;
            StateTitle.Text = $"No {ItemNamePlural} available"; StateMessage.Text = $"There are no {ItemNamePlural} to display yet.";
            action = EmptyActionCommand; StateActionButton.Content = EmptyActionText;
        }
        StateActionButton.Tag = action;
        StateActionButton.IsVisible = action?.CanExecute(null) == true && StateActionButton.Content is not null;
    }

    private void OnStateActionClick(object? sender, RoutedEventArgs e)
    {
        if (StateActionButton.Tag is ICommand command && command.CanExecute(null)) command.Execute(null);
    }

    private void ApplyMobileFooterLayout()
    {
        if (!IsMobilePlatform) return;

        FooterGrid.ColumnDefinitions.Clear();
        FooterGrid.RowDefinitions.Clear();
        FooterGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        FooterGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        FooterGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        FooterGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var i = 0; i < FooterGrid.Children.Count; i++)
        {
            var child = FooterGrid.Children[i];
            Grid.SetColumn(child, 0);
            Grid.SetRow(child, i);
            child.HorizontalAlignment = HorizontalAlignment.Center;
        }
        FooterGrid.RowSpacing = 8;
        PageSizePanel.IsVisible = false;
    }

    private void ApplyTableWidth()
    {
        if (_backgroundPanel is null) return;
        _backgroundPanel.Width = Math.Max(MinTableWidth, Bounds.Width);
    }

    private void OnPageSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingPageSize && PageSizeSelector.SelectedItem is int size) PageSize = size;
    }
    private void OnPreviousClick(object? sender, RoutedEventArgs e)
    {
        if (ExternalTotalCount >= 0)
        {
            if (ExternalPreviousCommand?.CanExecute(null) == true) ExternalPreviousCommand.Execute(null);
            return;
        }
        if (_currentPage > 1) { _currentPage--; Refresh(); }
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (ExternalTotalCount >= 0)
        {
            if (ExternalNextCommand?.CanExecute(null) == true) ExternalNextCommand.Execute(null);
            return;
        }
        _currentPage++;
        Refresh();
    }

    private sealed record SortDescriptor(PagedTableColumn Column, bool Ascending);

    private sealed class ValueComparer : IComparer<object?>
    {
        public static ValueComparer Instance { get; } = new();
        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            if (x is IComparable comparable && x.GetType() == y.GetType()) return comparable.CompareTo(y);
            var left = Convert.ToString(x, CultureInfo.CurrentCulture) ?? "";
            var right = Convert.ToString(y, CultureInfo.CurrentCulture) ?? "";
            var leftNumber = new string(left.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            var rightNumber = new string(right.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (decimal.TryParse(leftNumber, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) &&
                decimal.TryParse(rightNumber, NumberStyles.Number, CultureInfo.InvariantCulture, out var b)) return a.CompareTo(b);
            return StringComparer.CurrentCultureIgnoreCase.Compare(left, right);
        }
    }
}
