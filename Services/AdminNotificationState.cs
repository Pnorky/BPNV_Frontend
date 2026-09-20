using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApp.Services;

public partial class AdminNotificationState : ObservableObject, IDisposable
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private readonly bool _notifyOnNewUnread;
    private readonly ObservableCollection<AdminNotificationResponse> _items = [];
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private bool _hasLoaded;
    private bool _refreshFailureToastShown;
    private volatile bool _disposed;

    [ObservableProperty] private int _unreadCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public AdminNotificationState(
        StoreApiClient api,
        INotificationService notifications,
        bool notifyOnNewUnread = true)
    {
        _api = api;
        _notifications = notifications;
        _notifyOnNewUnread = notifyOnNewUnread;
        _lifetimeToken = _lifetimeCancellation.Token;
        Items = new ReadOnlyObservableCollection<AdminNotificationResponse>(_items);
    }

    public ReadOnlyObservableCollection<AdminNotificationResponse> Items { get; }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBeginOperation()) return false;

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        IsLoading = true;
        try
        {
            var response = await _api.GetAdminNotificationsAsync(cancellationToken: linkedCancellation.Token);
            var knownIds = _items.Select(item => item.Id).ToHashSet();
            var newUnread = _hasLoaded
                ? response.Items.Where(item => !item.IsRead && !knownIds.Contains(item.Id)).ToArray()
                : [];

            ReplaceItems(response.Items);
            UnreadCount = response.UnreadCount;
            ErrorMessage = null;
            _refreshFailureToastShown = false;
            _hasLoaded = true;

            if (_notifyOnNewUnread && newUnread.Length > 0)
            {
                var message = newUnread.Length == 1
                    ? newUnread[0].Title
                    : $"{newUnread.Length:N0} new shift notifications are waiting for review.";
                _notifications.ShowInformation("New admin notification", message);
            }

            return true;
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ErrorMessage = FailureMessage(exception);
            if (!_refreshFailureToastShown)
            {
                _refreshFailureToastShown = true;
                _notifications.ShowWarning("Notifications could not be refreshed", "Please try again.");
            }

            return false;
        }
        finally
        {
            IsLoading = false;
            _operationGate.Release();
        }
    }

    public async Task<bool> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var current = _items.FirstOrDefault(item => item.Id == notificationId);
        if (current?.IsRead == true) return true;
        if (!TryBeginOperation()) return false;

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        IsLoading = true;
        try
        {
            await _api.MarkAdminNotificationReadAsync(notificationId, linkedCancellation.Token);
            var index = IndexOf(notificationId);
            if (index >= 0 && !_items[index].IsRead)
            {
                _items[index] = _items[index] with { IsRead = true };
                UnreadCount = Math.Max(0, UnreadCount - 1);
            }

            ErrorMessage = null;
            return true;
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ErrorMessage = FailureMessage(exception);
            _notifications.ShowError("Notification could not be marked as read", "Please try again.");
            return false;
        }
        finally
        {
            IsLoading = false;
            _operationGate.Release();
        }
    }

    public async Task<bool> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        if (UnreadCount == 0) return true;
        if (!TryBeginOperation()) return false;

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        IsLoading = true;
        try
        {
            await _api.MarkAllAdminNotificationsReadAsync(linkedCancellation.Token);
            for (var index = 0; index < _items.Count; index++)
            {
                if (!_items[index].IsRead)
                    _items[index] = _items[index] with { IsRead = true };
            }

            UnreadCount = 0;
            ErrorMessage = null;
            return true;
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ErrorMessage = FailureMessage(exception);
            _notifications.ShowError("Notifications could not be marked as read", "Please try again.");
            return false;
        }
        finally
        {
            IsLoading = false;
            _operationGate.Release();
        }
    }

    private bool TryBeginOperation() => !_disposed && _operationGate.Wait(0);

    private void ReplaceItems(IEnumerable<AdminNotificationResponse> items)
    {
        _items.Clear();
        foreach (var item in items)
            _items.Add(item);
    }

    private int IndexOf(Guid notificationId)
    {
        for (var index = 0; index < _items.Count; index++)
        {
            if (_items[index].Id == notificationId) return index;
        }

        return -1;
    }

    private static bool IsApiFailure(Exception exception) =>
        exception is ApiClientException or HttpRequestException or TaskCanceledException;

    private static string FailureMessage(Exception exception) => exception switch
    {
        HttpRequestException => "Cannot reach the store API. The last notification inbox is still shown.",
        TaskCanceledException => "The store API did not respond in time. The last notification inbox is still shown.",
        _ => exception.Message
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }
}
