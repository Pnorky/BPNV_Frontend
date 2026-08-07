using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public sealed class TablePager<T> : ObservableObject
{
    private List<T> _source;
    private List<T> _baseSource;
    private int _currentPage = 1;
    private int _selectedPageSize = 10;
    private readonly List<SortDescriptor> _sorts = [];
    private bool _isFiltered;
    private bool _isLoading;
    private bool _isFirstTimeSetup;
    private string? _errorMessage;
    private readonly string _recordName;
    private readonly string _recordPlural;
    private string? _primaryActionText;
    private Action? _primaryAction;
    private Action? _clearFiltersAction;
    private Action? _retryAction;

    public TablePager(IEnumerable<T> source, string recordName = "record", string recordPlural = "records")
    {
        _baseSource = source.ToList();
        _source = _baseSource.ToList();
        _recordName = recordName;
        _recordPlural = recordPlural;
        SortCommand = new RelayCommand<string?>(Sort);
        PreviousPageCommand = new RelayCommand(PreviousPage, () => CanGoPrevious);
        NextPageCommand = new RelayCommand(NextPage, () => CanGoNext);
        StateActionCommand = new RelayCommand(ExecuteStateAction, () => HasStateAction);
        Refresh();
    }

    public ObservableCollection<T> Items { get; } = [];
    public IReadOnlyList<T> SourceItems => _source;
    public IReadOnlyList<int> PageSizeOptions { get; } = [5, 10, 20, 50];
    public IRelayCommand<string?> SortCommand { get; }
    public IRelayCommand PreviousPageCommand { get; }
    public IRelayCommand NextPageCommand { get; }
    public IRelayCommand StateActionCommand { get; }

    public int SelectedPageSize
    {
        get => _selectedPageSize;
        set
        {
            if (value <= 0 || !SetProperty(ref _selectedPageSize, value)) return;
            _currentPage = 1;
            Refresh();
        }
    }

    public int CurrentPage => _currentPage;
    public int TotalItemCount => _source.Count;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_source.Count / (double)SelectedPageSize));
    public bool HasItems => _source.Count > 0;
    public bool IsEmpty => !HasItems;
    public bool ShowState => IsLoading || ErrorMessage is not null || IsFirstTimeSetup || IsEmpty;
    public bool ShowStateIcon => !IsLoading;
    public bool IsLoading => _isLoading;
    public bool IsFirstTimeSetup => _isFirstTimeSetup;
    public string? ErrorMessage => _errorMessage;
    public bool IsFiltered => _isFiltered;
    public string RecordName => _recordName;
    public string RecordPlural => _recordPlural;
    public string StateIcon => ErrorMessage is not null ? "CircleAlert"
        : IsFirstTimeSetup ? "Rocket"
        : _isFiltered ? "SearchX"
        : "Inbox";
    public string StateTitle => IsLoading ? $"Loading {_recordPlural}"
        : ErrorMessage is not null ? $"Unable to load {_recordPlural}"
        : IsFirstTimeSetup ? "Let's get you set up"
        : _isFiltered ? "No matching results"
        : $"No {_recordPlural} available";
    public string StateMessage => IsLoading ? $"Please wait while the latest {_recordPlural} are retrieved."
        : ErrorMessage is not null ? ErrorMessage
        : IsFirstTimeSetup ? $"Create your first {_recordName} to start using this area."
        : _isFiltered ? "Try a different search or clear the current filters."
        : $"There are no {_recordPlural} to display yet.";
    public string StateActionText => ErrorMessage is not null ? "Try Again"
        : _isFiltered ? "Clear Filters"
        : _primaryActionText ?? "";
    public bool HasStateAction => ErrorMessage is not null ? _retryAction is not null
        : _isFiltered ? _clearFiltersAction is not null
        : _primaryAction is not null && !string.IsNullOrWhiteSpace(_primaryActionText);
    public bool CanGoPrevious => _currentPage > 1;
    public bool CanGoNext => _currentPage < TotalPages;
    public string PageSummary => _source.Count == 0
        ? "No items"
        : $"{((_currentPage - 1) * SelectedPageSize) + 1}-{Math.Min(_currentPage * SelectedPageSize, _source.Count)} of {_source.Count}";
    public string SortSummary => _sorts.Count == 0
        ? ""
        : $"Sorted by {string.Join(", ", _sorts.Select((sort, index) => $"{SplitName(sort.PropertyName)} {(sort.Ascending ? "↑" : "↓")} {index + 1}"))}";

    public void SetItems(IEnumerable<T> items, bool isFiltered = false)
    {
        _baseSource = items.ToList();
        _source = _baseSource.ToList();
        _isFiltered = isFiltered;
        _errorMessage = null;
        _isLoading = false;
        if (_source.Count > 0) _isFirstTimeSetup = false;
        _currentPage = 1;
        ApplySort();
        Refresh();
    }

    public void ConfigureStateActions(string? primaryActionText = null, Action? primaryAction = null,
        Action? clearFiltersAction = null, Action? retryAction = null)
    {
        _primaryActionText = primaryActionText;
        _primaryAction = primaryAction;
        _clearFiltersAction = clearFiltersAction;
        _retryAction = retryAction;
        NotifyStateChanged();
    }

    public void SetLoading(bool value = true)
    {
        _isLoading = value;
        if (value) _errorMessage = null;
        NotifyStateChanged();
    }

    public void SetError(string message)
    {
        _errorMessage = message;
        _isLoading = false;
        NotifyStateChanged();
    }

    public void SetFirstTimeSetup(bool value = true)
    {
        _isFirstTimeSetup = value;
        NotifyStateChanged();
    }

    private void ExecuteStateAction()
    {
        if (ErrorMessage is not null) _retryAction?.Invoke();
        else if (_isFiltered) _clearFiltersAction?.Invoke();
        else _primaryAction?.Invoke();
    }

    private void Sort(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return;
        var existing = _sorts.FindIndex(sort => sort.PropertyName == propertyName);
        if (existing < 0)
            _sorts.Add(new SortDescriptor(propertyName, true));
        else if (_sorts[existing].Ascending)
            _sorts[existing] = _sorts[existing] with { Ascending = false };
        else
            _sorts.RemoveAt(existing);

        _currentPage = 1;
        ApplySort();
        Refresh();
    }

    private void ApplySort()
    {
        if (_sorts.Count == 0)
        {
            _source = _baseSource.ToList();
            return;
        }

        IOrderedEnumerable<T>? ordered = null;
        foreach (var sort in _sorts)
        {
            var property = typeof(T).GetProperty(sort.PropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null) continue;
            Func<T, object?> selector = item => property.GetValue(item);
            ordered = ordered is null
                ? sort.Ascending
                    ? _baseSource.OrderBy(selector, ValueComparer.Instance)
                    : _baseSource.OrderByDescending(selector, ValueComparer.Instance)
                : sort.Ascending
                    ? ordered.ThenBy(selector, ValueComparer.Instance)
                    : ordered.ThenByDescending(selector, ValueComparer.Instance);
        }

        if (ordered is not null) _source = ordered.ToList();
    }

    private void PreviousPage()
    {
        if (!CanGoPrevious) return;
        _currentPage--;
        Refresh();
    }

    private void NextPage()
    {
        if (!CanGoNext) return;
        _currentPage++;
        Refresh();
    }

    private void Refresh()
    {
        if (_currentPage > TotalPages) _currentPage = TotalPages;
        Items.Clear();
        foreach (var item in _source.Skip((_currentPage - 1) * SelectedPageSize).Take(SelectedPageSize))
            Items.Add(item);

        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(TotalItemCount));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(SortSummary));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(SourceItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowState));
        OnPropertyChanged(nameof(ShowStateIcon));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsFirstTimeSetup));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(StateIcon));
        OnPropertyChanged(nameof(StateTitle));
        OnPropertyChanged(nameof(StateMessage));
        OnPropertyChanged(nameof(StateActionText));
        OnPropertyChanged(nameof(HasStateAction));
        StateActionCommand.NotifyCanExecuteChanged();
    }

    private static string SplitName(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");

    private sealed record SortDescriptor(string PropertyName, bool Ascending);

    private sealed class ValueComparer : IComparer<object?>
    {
        public static ValueComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            var left = Convert.ToString(x, CultureInfo.InvariantCulture) ?? "";
            var right = Convert.ToString(y, CultureInfo.InvariantCulture) ?? "";
            var leftNumber = new string(left.Where(c => char.IsDigit(c) || c == '.').ToArray());
            var rightNumber = new string(right.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (decimal.TryParse(leftNumber, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) &&
                decimal.TryParse(rightNumber, NumberStyles.Number, CultureInfo.InvariantCulture, out var b))
                return a.CompareTo(b);
            return StringComparer.CurrentCultureIgnoreCase.Compare(left, right);
        }
    }
}
