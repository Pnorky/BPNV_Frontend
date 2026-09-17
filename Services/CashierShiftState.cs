using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApp.Services;

public partial class CashierShiftState(StoreApiClient api) : ObservableObject
{
    private ClockInRequest? _pendingClockIn;
    private Guid? _clockOutKey;

    [ObservableProperty] private CashierClockStatusResponse? _clockStatus;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public CashierShiftSessionResponse? OpenSession => ClockStatus?.OpenSession;
    public decimal? PendingClockInOpeningFloat => _pendingClockIn?.OpeningCashFloat;
    public ResolvedCashierShiftAssignmentResponse? Assignment => ClockStatus?.Assignment;
    public CashierTerminalOccupancyResponse? OccupiedTerminal => ClockStatus?.OccupiedTerminal;
    public bool IsClockedIn => OpenSession?.Status == ApiCashierShiftSessionStatus.Open;
    public bool CanCheckout => IsClockedIn && !IsLoading;
    public bool CanClockIn => ClockStatus?.CanClockIn == true && !IsLoading;
    public string StatusDisplay => OpenSession is not null
        ? $"{OpenSession.ShiftName} active"
        : OccupiedTerminal is not null
            ? $"Terminal: {OccupiedTerminal.CashierName}"
            : Assignment is not null ? $"{Assignment.ShiftName} ready" : "Not clocked in";

    partial void OnClockStatusChanged(CashierClockStatusResponse? value) => NotifyDerived();
    partial void OnIsLoadingChanged(bool value) => NotifyDerived();

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading) return false;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            ClockStatus = await api.GetCashierClockStatusAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ClockStatus = null;
            ErrorMessage = FailureMessage(exception);
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<CashierShiftSessionResponse> ClockInAsync(decimal openingCashFloat, CancellationToken cancellationToken = default)
    {
        if (openingCashFloat < 0 || decimal.Round(openingCashFloat, 2) != openingCashFloat)
            throw new ArgumentOutOfRangeException(nameof(openingCashFloat), "Opening cash must be non-negative and use at most two decimal places.");
        if (_pendingClockIn is null)
        {
            _pendingClockIn = new ClockInRequest(Guid.NewGuid(), openingCashFloat);
            OnPropertyChanged(nameof(PendingClockInOpeningFloat));
        }
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var session = await api.ClockInAsync(_pendingClockIn, cancellationToken);
            _pendingClockIn = null;
            OnPropertyChanged(nameof(PendingClockInOpeningFloat));
            var previous = ClockStatus;
            ClockStatus = new CashierClockStatusResponse(
                DateTime.UtcNow,
                previous?.StoreLocalTime ?? DateTimeOffset.Now,
                previous?.Assignment,
                session,
                null,
                previous?.AssignmentVarianceMinutes,
                false,
                null);
            return session;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ErrorMessage = FailureMessage(exception);
            if (exception is ApiClientException)
            {
                _pendingClockIn = null;
                OnPropertyChanged(nameof(PendingClockInOpeningFloat));
            }
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<CashierShiftSessionResponse> ClockOutAsync(CancellationToken cancellationToken = default)
    {
        _clockOutKey ??= Guid.NewGuid();
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var session = await api.ClockOutAsync(new(_clockOutKey.Value), cancellationToken);
            _clockOutKey = null;
            ClockStatus = ClockStatus is null ? null : ClockStatus with { OpenSession = null, CanClockIn = false, BlockReason = "This assignment is completed." };
            return session;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ErrorMessage = FailureMessage(exception);
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Reset()
    {
        _pendingClockIn = null;
        OnPropertyChanged(nameof(PendingClockInOpeningFloat));
        _clockOutKey = null;
        ClockStatus = null;
        ErrorMessage = null;
        IsLoading = false;
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(OpenSession));
        OnPropertyChanged(nameof(Assignment));
        OnPropertyChanged(nameof(OccupiedTerminal));
        OnPropertyChanged(nameof(IsClockedIn));
        OnPropertyChanged(nameof(CanCheckout));
        OnPropertyChanged(nameof(CanClockIn));
        OnPropertyChanged(nameof(StatusDisplay));
    }

    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException
        ? "Cannot reach the store API. Clock-in status is unknown."
        : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;
}
