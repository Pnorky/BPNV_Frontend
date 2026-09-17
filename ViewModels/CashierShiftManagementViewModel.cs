using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public sealed record CashierChoice(Guid Id, string DisplayName, string Username)
{
    public string SearchText => $"{DisplayName} {Username}";
    public override string ToString() => $"{DisplayName} ({Username})";
}

public partial class ShiftDefinitionEditor : ObservableObject
{
    [ObservableProperty] private Guid _id;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private TimeSpan? _startTime = TimeSpan.FromHours(6);
    [ObservableProperty] private TimeSpan? _endTime = TimeSpan.FromHours(14);

    public bool IsEditing => Id != Guid.Empty;
    public string Title => IsEditing ? "Edit shift definition" : "New shift definition";
    public string ActionText => IsEditing ? "Save changes" : "Create shift";
    public bool IsOvernight => StartTime.HasValue && EndTime.HasValue && EndTime <= StartTime;
    public string RangePreview => !StartTime.HasValue || !EndTime.HasValue
        ? "Choose distinct start and end times."
        : $"{FormatTime(StartTime.Value)} - {FormatTime(EndTime.Value)}{(IsOvernight ? " next day" : "")}";

    partial void OnIdChanged(Guid value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ActionText));
    }

    partial void OnStartTimeChanged(TimeSpan? value) => NotifyRange();
    partial void OnEndTimeChanged(TimeSpan? value) => NotifyRange();

    public void Load(ShiftDefinitionResponse definition)
    {
        Id = definition.Id;
        Name = definition.Name;
        StartTime = definition.StartLocalTime.ToTimeSpan();
        EndTime = definition.EndLocalTime.ToTimeSpan();
    }

    public void Clear()
    {
        Id = Guid.Empty;
        Name = "";
        StartTime = TimeSpan.FromHours(6);
        EndTime = TimeSpan.FromHours(14);
    }

    private void NotifyRange()
    {
        OnPropertyChanged(nameof(IsOvernight));
        OnPropertyChanged(nameof(RangePreview));
    }

    private static string FormatTime(TimeSpan value) => DateTime.Today.Add(value).ToString("h:mm tt");
}

public partial class ShiftDefinitionsViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;

    public ShiftDefinitionsViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
    }

    public ShiftDefinitionEditor Editor { get; } = new();
    [ObservableProperty] private IReadOnlyList<ShiftDefinitionResponse> _items = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _statusMessage = "Loading shift definitions...";

    public bool CanEdit => !IsBusy;
    public int ActiveCount => Items.Count(item => item.IsActive);

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Loading shift definitions...";
        try
        {
            Items = (await _api.GetShiftDefinitionsAsync(includeInactive: true))
                .OrderByDescending(item => item.IsActive)
                .ThenBy(item => item.StartLocalTime)
                .ThenBy(item => item.Name)
                .ToArray();
            OnPropertyChanged(nameof(ActiveCount));
            StatusMessage = $"Loaded {Items.Count} shift definition{Plural(Items.Count)}.";
        }
        catch (Exception exception) when (ShiftManagementErrors.IsApiFailure(exception))
        {
            ErrorMessage = ShiftManagementErrors.Message(exception);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewDefinition() => Editor.Clear();

    [RelayCommand]
    private void EditDefinition(ShiftDefinitionResponse? definition)
    {
        if (definition is not null) Editor.Load(definition);
    }

    [RelayCommand]
    private void CancelEdit() => Editor.Clear();

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Editor.Name))
        {
            ShowError("Shift not saved", "Shift name is required.");
            return;
        }
        if (Editor.Name.Trim().Length > 80)
        {
            ShowError("Shift not saved", "Shift name cannot exceed 80 characters.");
            return;
        }
        if (!ValidTime(Editor.StartTime) || !ValidTime(Editor.EndTime) || Editor.StartTime == Editor.EndTime)
        {
            ShowError("Shift not saved", "Choose valid, distinct start and end times.");
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var name = Editor.Name.Trim();
            var start = TimeOnly.FromTimeSpan(Editor.StartTime!.Value);
            var end = TimeOnly.FromTimeSpan(Editor.EndTime!.Value);
            var action = Editor.IsEditing ? "updated" : "created";
            if (Editor.IsEditing)
                await _api.UpdateShiftDefinitionAsync(Editor.Id, new UpdateShiftDefinitionRequest(name, start, end));
            else
                await _api.CreateShiftDefinitionAsync(new CreateShiftDefinitionRequest(name, start, end));

            Editor.Clear();
            StatusMessage = $"{name} was {action}.";
            _notifications.ShowSuccess($"Shift {action}", StatusMessage);
        }
        catch (Exception exception) when (ShiftManagementErrors.IsApiFailure(exception))
        {
            ShowError("Shift not saved", ShiftManagementErrors.Message(exception));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await LoadAsync();
    }

    [RelayCommand]
    public Task DeactivateAsync(ShiftDefinitionResponse? definition) =>
        ChangeActiveStateAsync(definition, false);

    [RelayCommand]
    public Task ReactivateAsync(ShiftDefinitionResponse? definition) =>
        ChangeActiveStateAsync(definition, true);

    private async Task ChangeActiveStateAsync(ShiftDefinitionResponse? definition, bool active)
    {
        if (definition is null || IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (active) await _api.ReactivateShiftDefinitionAsync(definition.Id);
            else await _api.DeactivateShiftDefinitionAsync(definition.Id);
            var action = active ? "reactivated" : "deactivated";
            StatusMessage = $"{definition.Name} was {action}.";
            _notifications.ShowSuccess($"Shift {action}", StatusMessage);
        }
        catch (Exception exception) when (ShiftManagementErrors.IsApiFailure(exception))
        {
            ShowError(active ? "Shift not reactivated" : "Shift not deactivated", ShiftManagementErrors.Message(exception));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await LoadAsync();
    }

    private void ShowError(string title, string message)
    {
        ErrorMessage = message;
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }

    private static bool ValidTime(TimeSpan? value) =>
        value.HasValue && value.Value >= TimeSpan.Zero && value.Value < TimeSpan.FromDays(1);
    private static string Plural(int count) => count == 1 ? "" : "s";
}

public partial class ShiftScheduleEditor : ObservableObject
{
    [ObservableProperty] private Guid _id;
    [ObservableProperty] private CashierChoice? _selectedCashier;
    [ObservableProperty] private ShiftDefinitionResponse? _selectedShift;
    [ObservableProperty] private bool _monday;
    [ObservableProperty] private bool _tuesday;
    [ObservableProperty] private bool _wednesday;
    [ObservableProperty] private bool _thursday;
    [ObservableProperty] private bool _friday;
    [ObservableProperty] private bool _saturday;
    [ObservableProperty] private bool _sunday;
    [ObservableProperty] private DateTimeOffset? _effectiveFrom = StoreDateTime.AtStoreMidnight(StoreDateTime.StoreToday);
    [ObservableProperty] private DateTimeOffset? _effectiveTo;

    public bool IsEditing => Id != Guid.Empty;
    public string Title => IsEditing ? "Edit recurring schedule" : "New recurring schedule";
    public string ActionText => IsEditing ? "Save changes" : "Create schedule";
    public byte DaysOfWeekMask => (byte)((Monday ? 1 : 0) | (Tuesday ? 2 : 0) | (Wednesday ? 4 : 0) |
        (Thursday ? 8 : 0) | (Friday ? 16 : 0) | (Saturday ? 32 : 0) | (Sunday ? 64 : 0));

    partial void OnIdChanged(Guid value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ActionText));
    }

    public void Load(CashierShiftScheduleResponse schedule, IReadOnlyList<CashierChoice> cashiers, IReadOnlyList<ShiftDefinitionResponse> definitions)
    {
        Id = schedule.Id;
        SelectedCashier = cashiers.FirstOrDefault(item => item.Id == schedule.CashierUserId);
        SelectedShift = definitions.FirstOrDefault(item => item.Id == schedule.ShiftDefinitionId);
        Monday = (schedule.DaysOfWeekMask & 1) != 0;
        Tuesday = (schedule.DaysOfWeekMask & 2) != 0;
        Wednesday = (schedule.DaysOfWeekMask & 4) != 0;
        Thursday = (schedule.DaysOfWeekMask & 8) != 0;
        Friday = (schedule.DaysOfWeekMask & 16) != 0;
        Saturday = (schedule.DaysOfWeekMask & 32) != 0;
        Sunday = (schedule.DaysOfWeekMask & 64) != 0;
        EffectiveFrom = StoreDateTime.AtStoreMidnight(schedule.EffectiveFrom.ToDateTime(TimeOnly.MinValue));
        EffectiveTo = schedule.EffectiveTo is { } end
            ? StoreDateTime.AtStoreMidnight(end.ToDateTime(TimeOnly.MinValue))
            : null;
    }

    public void Clear()
    {
        Id = Guid.Empty;
        SelectedCashier = null;
        SelectedShift = null;
        Monday = Tuesday = Wednesday = Thursday = Friday = false;
        Saturday = Sunday = false;
        EffectiveFrom = StoreDateTime.AtStoreMidnight(StoreDateTime.StoreToday);
        EffectiveTo = null;
    }
}

public partial class ShiftSchedulesViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private IReadOnlyList<CashierChoice> _cashiers = [];

    public ShiftSchedulesViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
    }

    public ShiftScheduleEditor Editor { get; } = new();
    [ObservableProperty] private IReadOnlyList<CashierShiftScheduleResponse> _items = [];
    [ObservableProperty] private IReadOnlyList<ShiftDefinitionResponse> _shiftDefinitions = [];
    [ObservableProperty] private IReadOnlyList<CashierChoice> _filteredCashiers = [];
    [ObservableProperty] private string _cashierSearchText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _statusMessage = "Loading recurring schedules...";

    public bool CanEdit => !IsBusy;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnCashierSearchTextChanged(string value) => ApplyCashierFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Loading recurring schedules...";
        try
        {
            var schedulesTask = _api.GetCashierShiftSchedulesAsync(includeInactive: true);
            var definitionsTask = _api.GetShiftDefinitionsAsync(includeInactive: true);
            var usersTask = _api.GetUsersAsync(includeInactive: false);
            await Task.WhenAll(schedulesTask, definitionsTask, usersTask);

            Items = schedulesTask.Result
                .OrderByDescending(item => item.IsActive)
                .ThenBy(item => item.ShiftName)
                .ThenBy(item => item.CashierName)
                .ToArray();
            ShiftDefinitions = definitionsTask.Result.Where(item => item.IsActive).OrderBy(item => item.StartLocalTime).ToArray();
            _cashiers = usersTask.Result
                .Where(user => user.IsActive && user.Roles.Contains("Cashier", StringComparer.OrdinalIgnoreCase))
                .OrderBy(user => user.DisplayName)
                .Select(user => new CashierChoice(user.Id, user.DisplayName, user.Username))
                .ToArray();
            ApplyCashierFilter();
            StatusMessage = $"Loaded {Items.Count} recurring schedule{(Items.Count == 1 ? "" : "s")}.";
        }
        catch (Exception exception) when (ShiftManagementErrors.IsApiFailure(exception))
        {
            ErrorMessage = ShiftManagementErrors.Message(exception);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewSchedule()
    {
        CashierSearchText = "";
        Editor.Clear();
    }

    [RelayCommand]
    private void EditSchedule(CashierShiftScheduleResponse? schedule)
    {
        if (schedule is null) return;
        CashierSearchText = "";
        Editor.Load(schedule, _cashiers, ShiftDefinitions);
    }

    [RelayCommand]
    private void CancelEdit() => Editor.Clear();

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (IsBusy) return;
        if (Editor.SelectedCashier is null || Editor.SelectedShift is null)
        {
            ShowError("Schedule not saved", "Select an active Cashier user and an active shift definition.");
            return;
        }
        if (Editor.DaysOfWeekMask == 0)
        {
            ShowError("Schedule not saved", "Select at least one weekday.");
            return;
        }
        if (Editor.EffectiveFrom is null)
        {
            ShowError("Schedule not saved", "An effective start date is required.");
            return;
        }

        var from = ToDateOnly(Editor.EffectiveFrom.Value);
        DateOnly? to = Editor.EffectiveTo is { } end ? ToDateOnly(end) : null;
        if (to < from)
        {
            ShowError("Schedule not saved", "The effective end date cannot be before the start date.");
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var action = Editor.IsEditing ? "updated" : "created";
            if (Editor.IsEditing)
                await _api.UpdateCashierShiftScheduleAsync(Editor.Id, new UpdateCashierShiftScheduleRequest(
                    Editor.SelectedCashier.Id, Editor.SelectedShift.Id, Editor.DaysOfWeekMask, from, to));
            else
                await _api.CreateCashierShiftScheduleAsync(new CreateCashierShiftScheduleRequest(
                    Editor.SelectedCashier.Id, Editor.SelectedShift.Id, Editor.DaysOfWeekMask, from, to));

            Editor.Clear();
            StatusMessage = $"Recurring schedule was {action}.";
            _notifications.ShowSuccess($"Schedule {action}", StatusMessage);
        }
        catch (Exception exception) when (ShiftManagementErrors.IsApiFailure(exception))
        {
            ShowError("Schedule not saved", ShiftManagementErrors.Message(exception));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await LoadAsync();
    }

    [RelayCommand]
    public Task DeactivateAsync(CashierShiftScheduleResponse? schedule) => ChangeActiveStateAsync(schedule, false);

    [RelayCommand]
    public Task ReactivateAsync(CashierShiftScheduleResponse? schedule) => ChangeActiveStateAsync(schedule, true);

    private async Task ChangeActiveStateAsync(CashierShiftScheduleResponse? schedule, bool active)
    {
        if (schedule is null || IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (active) await _api.ReactivateCashierShiftScheduleAsync(schedule.Id);
            else await _api.DeactivateCashierShiftScheduleAsync(schedule.Id);
            var action = active ? "reactivated" : "deactivated";
            StatusMessage = $"{schedule.CashierName}'s {schedule.ShiftName} schedule was {action}.";
            _notifications.ShowSuccess($"Schedule {action}", StatusMessage);
        }
        catch (Exception exception) when (ShiftManagementErrors.IsApiFailure(exception))
        {
            ShowError(active ? "Schedule not reactivated" : "Schedule not deactivated", ShiftManagementErrors.Message(exception));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await LoadAsync();
    }

    private void ApplyCashierFilter()
    {
        var search = CashierSearchText.Trim();
        FilteredCashiers = _cashiers.Where(item => search.Length == 0 || item.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private void ShowError(string title, string message)
    {
        ErrorMessage = message;
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }

    private static DateOnly ToDateOnly(DateTimeOffset value) => DateOnly.FromDateTime(value.Date);
}

public sealed class ResolvedDailyShiftRow
{
    public ResolvedDailyShiftRow(
        DateOnly businessDate,
        ShiftDefinitionResponse definition,
        CashierShiftScheduleResponse? originalSchedule,
        CashierShiftOverrideResponse? assignmentOverride,
        ResolvedCashierShiftAssignmentResponse? resolvedAssignment,
        CashierShiftSessionResponse? existingSession)
    {
        BusinessDate = businessDate;
        Definition = definition;
        OriginalSchedule = originalSchedule;
        AssignmentOverride = assignmentOverride;
        ResolvedAssignment = resolvedAssignment;
        ExistingSession = existingSession;
    }

    public DateOnly BusinessDate { get; }
    public ShiftDefinitionResponse Definition { get; }
    public CashierShiftScheduleResponse? OriginalSchedule { get; }
    public CashierShiftOverrideResponse? AssignmentOverride { get; }
    public ResolvedCashierShiftAssignmentResponse? ResolvedAssignment { get; }
    public CashierShiftSessionResponse? ExistingSession { get; }
    public string StateDisplay => IsOccupied ? "Occupied" : AssignmentOverride is not null ? "Replaced" : ResolvedAssignment is not null ? "Scheduled" : "Unassigned";
    public string OriginalCashierDisplay => OriginalSchedule?.CashierName ?? "Unassigned";
    public string AssignedCashierDisplay => ResolvedAssignment?.CashierName ?? "Unassigned";
    public string ReasonDisplay => AssignmentOverride?.Reason ?? "-";
    public bool HasOverride => AssignmentOverride is not null;
    public bool IsOccupied => ExistingSession?.Status == ApiCashierShiftSessionStatus.Open;
    public bool CanReplace => ExistingSession is null || ExistingSession.CloseType == ApiCashierShiftCloseType.AdministrativeClockOut;
}

public partial class ShiftReplacementEditor : ObservableObject
{
    [ObservableProperty] private Guid _id;
    [ObservableProperty] private ResolvedDailyShiftRow? _selectedShift;
    [ObservableProperty] private CashierChoice? _selectedCashier;
    [ObservableProperty] private string _reason = "";

    public bool IsEditing => Id != Guid.Empty;
    public string Title => IsEditing ? "Edit replacement" : "Create replacement";
    public string ActionText => IsEditing ? "Save replacement" : "Create replacement";

    partial void OnIdChanged(Guid value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ActionText));
    }

    public void Load(ResolvedDailyShiftRow row, IReadOnlyList<CashierChoice> cashiers)
    {
        Id = row.AssignmentOverride?.Id ?? Guid.Empty;
        SelectedShift = row;
        SelectedCashier = row.AssignmentOverride is { } assignmentOverride
            ? cashiers.FirstOrDefault(item => item.Id == assignmentOverride.ReplacementCashierUserId)
            : null;
        Reason = row.AssignmentOverride?.Reason ?? "";
    }

    public void Clear()
    {
        Id = Guid.Empty;
        SelectedShift = null;
        SelectedCashier = null;
        Reason = "";
    }
}

public partial class ShiftReplacementsViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private IReadOnlyList<CashierChoice> _cashiers = [];
    private int _loadVersion;

    public ShiftReplacementsViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
    }

    public ShiftReplacementEditor Editor { get; } = new();
    [ObservableProperty] private DateTimeOffset? _selectedBusinessDate = StoreDateTime.AtStoreMidnight(StoreDateTime.StoreToday);
    [ObservableProperty] private IReadOnlyList<ResolvedDailyShiftRow> _dailyShifts = [];
    [ObservableProperty] private IReadOnlyList<CashierChoice> _filteredCashiers = [];
    [ObservableProperty] private string _cashierSearchText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _statusMessage = "Loading the resolved daily schedule...";

    public bool CanEdit => !IsBusy;
    public string SelectedDateDisplay => SelectedBusinessDate?.ToString("MMMM d, yyyy") ?? "No business date selected";

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnCashierSearchTextChanged(string value) => ApplyCashierFilter();
    partial void OnSelectedBusinessDateChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(SelectedDateDisplay));
        Editor.Clear();
        if (value.HasValue) _ = LoadDateAsync(++_loadVersion, ToDateOnly(value.Value));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (SelectedBusinessDate is null)
        {
            ErrorMessage = "Select a business date.";
            StatusMessage = ErrorMessage;
            return;
        }
        await LoadDateAsync(++_loadVersion, ToDateOnly(SelectedBusinessDate.Value));
    }

    [RelayCommand]
    private void PreviousDay() => MoveDate(-1);

    [RelayCommand]
    private void NextDay() => MoveDate(1);

    [RelayCommand]
    private void Today() => SelectedBusinessDate = StoreDateTime.AtStoreMidnight(StoreDateTime.StoreToday);

    [RelayCommand]
    private void EditReplacement(ResolvedDailyShiftRow? row)
    {
        if (row is null) return;
        CashierSearchText = "";
        Editor.Load(row, _cashiers);
    }

    [RelayCommand]
    private void CancelEdit() => Editor.Clear();

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (IsBusy) return;
        if (SelectedBusinessDate is null || Editor.SelectedShift is null)
        {
            ShowError("Replacement not saved", "Select a business date and shift.");
            return;
        }
        if (!Editor.SelectedShift.CanReplace)
        {
            ShowError("Replacement not saved", Editor.SelectedShift.IsOccupied
                ? "This shift is occupied. Administratively clock out the open session before replacing its cashier."
                : "A replacement can only be saved before the scheduled session starts.");
            return;
        }
        if (Editor.SelectedCashier is null)
        {
            ShowError("Replacement not saved", "Select an active Cashier user as the replacement.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Editor.Reason))
        {
            ShowError("Replacement not saved", "A replacement reason is required.");
            return;
        }
        if (Editor.Reason.Trim().Length > 1000)
        {
            ShowError("Replacement not saved", "Replacement reason cannot exceed 1,000 characters.");
            return;
        }

        var date = ToDateOnly(SelectedBusinessDate.Value);
        var row = Editor.SelectedShift;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var action = Editor.IsEditing ? "updated" : "created";
            if (Editor.IsEditing)
                await _api.UpdateCashierShiftOverrideAsync(Editor.Id, new UpdateCashierShiftOverrideRequest(
                    date, row.Definition.Id, Editor.SelectedCashier.Id, row.OriginalSchedule?.Id, Editor.Reason.Trim()));
            else
                await _api.CreateCashierShiftOverrideAsync(new CreateCashierShiftOverrideRequest(
                    date, row.Definition.Id, Editor.SelectedCashier.Id, row.OriginalSchedule?.Id, Editor.Reason.Trim()));
            Editor.Clear();
            StatusMessage = $"{row.Definition.Name} replacement was {action} for {date:MMMM d, yyyy}.";
            _notifications.ShowSuccess($"Replacement {action}", StatusMessage);
        }
        catch (Exception exception) when (ShiftManagementErrors.IsApiFailure(exception))
        {
            ShowError("Replacement not saved", ShiftManagementErrors.Message(exception));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await LoadAsync();
    }

    [RelayCommand]
    public async Task DeleteAsync(ResolvedDailyShiftRow? row)
    {
        if (row?.AssignmentOverride is not { } assignmentOverride || IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _api.DeleteCashierShiftOverrideAsync(assignmentOverride.Id);
            if (Editor.Id == assignmentOverride.Id) Editor.Clear();
            StatusMessage = $"{row.Definition.Name} replacement was deleted for {row.BusinessDate:MMMM d, yyyy}.";
            _notifications.ShowSuccess("Replacement deleted", StatusMessage);
        }
        catch (Exception exception) when (ShiftManagementErrors.IsApiFailure(exception))
        {
            ShowError("Replacement not deleted", ShiftManagementErrors.Message(exception));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await LoadAsync();
    }

    private async Task LoadDateAsync(int version, DateOnly date)
    {
        IsBusy = true;
        if (version == _loadVersion)
        {
            ErrorMessage = null;
            StatusMessage = $"Loading the resolved schedule for {date:MMMM d, yyyy}...";
        }
        try
        {
            var end = date.AddDays(1);
            var definitionsTask = _api.GetShiftDefinitionsAsync(includeInactive: true);
            var schedulesTask = _api.GetCashierShiftSchedulesAsync(date, end, includeInactive: true);
            var overridesTask = _api.GetCashierShiftOverridesAsync(date, end);
            var resolvedTask = _api.GetResolvedCashierScheduleAsync(date, end);
            var sessionsTask = _api.GetCashierShiftSessionsAsync(date, end, pageSize: 100);
            var usersTask = _api.GetUsersAsync(includeInactive: false);
            await Task.WhenAll(definitionsTask, schedulesTask, overridesTask, resolvedTask, sessionsTask, usersTask);
            if (version != _loadVersion) return;

            var definitions = definitionsTask.Result;
            var schedules = schedulesTask.Result;
            var overrides = overridesTask.Result;
            var resolved = resolvedTask.Result;
            var sessions = sessionsTask.Result.Items;
            var visibleDefinitions = definitions
                .Where(definition => definition.IsActive || resolved.Any(item => item.ShiftDefinitionId == definition.Id) || overrides.Any(item => item.ShiftDefinitionId == definition.Id))
                .OrderBy(definition => definition.StartLocalTime)
                .ToArray();

            DailyShifts = visibleDefinitions.Select(definition =>
            {
                var assignmentOverride = overrides.FirstOrDefault(item => item.ShiftDefinitionId == definition.Id);
                var assignment = resolved.FirstOrDefault(item => item.ShiftDefinitionId == definition.Id);
                var originalId = assignmentOverride?.ReplacedScheduleId ?? assignment?.ScheduleId;
                var original = originalId.HasValue ? schedules.FirstOrDefault(item => item.Id == originalId.Value) : null;
                var existing = sessions.Where(item => item.ShiftDefinitionId == definition.Id)
                    .OrderByDescending(item => item.ClockedInAtUtc).FirstOrDefault();
                return new ResolvedDailyShiftRow(date, definition, original, assignmentOverride, assignment, existing);
            }).ToArray();
            _cashiers = usersTask.Result
                .Where(user => user.IsActive && user.Roles.Contains("Cashier", StringComparer.OrdinalIgnoreCase))
                .OrderBy(user => user.DisplayName)
                .Select(user => new CashierChoice(user.Id, user.DisplayName, user.Username))
                .ToArray();
            ApplyCashierFilter();
            StatusMessage = $"Resolved {DailyShifts.Count} shift{(DailyShifts.Count == 1 ? "" : "s")} for {date:MMMM d, yyyy}.";
        }
        catch (Exception exception) when (ShiftManagementErrors.IsApiFailure(exception))
        {
            if (version != _loadVersion) return;
            ErrorMessage = ShiftManagementErrors.Message(exception);
            StatusMessage = ErrorMessage;
        }
        finally
        {
            if (version == _loadVersion) IsBusy = false;
        }
    }

    private void MoveDate(int days)
    {
        var date = SelectedBusinessDate?.Date ?? StoreDateTime.StoreToday;
        SelectedBusinessDate = StoreDateTime.AtStoreMidnight(date.AddDays(days));
    }

    private void ApplyCashierFilter()
    {
        var search = CashierSearchText.Trim();
        FilteredCashiers = _cashiers.Where(item => search.Length == 0 || item.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private void ShowError(string title, string message)
    {
        ErrorMessage = message;
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }

    private static DateOnly ToDateOnly(DateTimeOffset value) => DateOnly.FromDateTime(value.Date);
}

public partial class CashierShiftManagementViewModel : ObservableObject
{
    public CashierShiftManagementViewModel(StoreApiClient api, INotificationService notifications)
    {
        Definitions = new ShiftDefinitionsViewModel(api, notifications);
        Schedules = new ShiftSchedulesViewModel(api, notifications);
        Replacements = new ShiftReplacementsViewModel(api, notifications);
        _ = LoadAsync();
    }

    public ShiftDefinitionsViewModel Definitions { get; }
    public ShiftSchedulesViewModel Schedules { get; }
    public ShiftReplacementsViewModel Replacements { get; }
    [ObservableProperty] private int _selectedTabIndex;

    public bool IsBusy => Definitions.IsBusy || Schedules.IsBusy || Replacements.IsBusy;

    [RelayCommand]
    public async Task LoadAsync() => await Task.WhenAll(
        Definitions.LoadAsync(), Schedules.LoadAsync(), Replacements.LoadAsync());
}

internal static class ShiftManagementErrors
{
    public static bool IsApiFailure(Exception exception) =>
        exception is ApiClientException or HttpRequestException or TaskCanceledException;

    public static string Message(Exception exception) => exception switch
    {
        HttpRequestException => "Cannot reach the store API.",
        TaskCanceledException => "The store API did not respond in time.",
        _ => exception.Message
    };
}
