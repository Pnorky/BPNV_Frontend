using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class AdminNotificationsViewModel : ObservableObject, IDisposable
{
    private readonly INotifyCollectionChanged _observableItems;
    private bool _disposed;

    public AdminNotificationsViewModel(AdminNotificationState state)
    {
        State = state;
        _observableItems = State.Items;
        State.PropertyChanged += OnStatePropertyChanged;
        _observableItems.CollectionChanged += OnItemsChanged;
    }

    public AdminNotificationState State { get; }
    public ReadOnlyObservableCollection<AdminNotificationResponse> Items => State.Items;
    public int UnreadCount => State.UnreadCount;
    public bool IsLoading => State.IsLoading;
    public string? ErrorMessage => State.ErrorMessage;
    public bool HasItems => Items.Count > 0;
    public bool HasUnread => UnreadCount > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool ShowEmptyState => !IsLoading && !HasItems && !HasError;
    public string InboxSummary => $"Showing {Items.Count:N0} recent notifications | {UnreadCount:N0} unread";

    public event Action<Guid>? ShiftSessionRequested;

    private bool CanRefresh() => !IsLoading;
    private bool CanMarkRead(AdminNotificationResponse? notification) =>
        !IsLoading && notification is { IsRead: false };
    private bool CanMarkAllRead() => !IsLoading && HasUnread;
    private static bool CanOpenLinkedSession(AdminNotificationResponse? notification) =>
        notification?.ShiftSessionId is not null;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync() => await State.RefreshAsync();

    [RelayCommand(CanExecute = nameof(CanMarkRead))]
    public async Task MarkReadAsync(AdminNotificationResponse? notification)
    {
        if (notification is null) return;
        await State.MarkReadAsync(notification.Id);
    }

    [RelayCommand(CanExecute = nameof(CanMarkAllRead))]
    public async Task MarkAllReadAsync() => await State.MarkAllReadAsync();

    [RelayCommand(CanExecute = nameof(CanOpenLinkedSession))]
    public async Task OpenLinkedSessionAsync(AdminNotificationResponse? notification)
    {
        if (notification?.ShiftSessionId is { } shiftSessionId)
        {
            if (!notification.IsRead) await State.MarkReadAsync(notification.Id);
            ShiftSessionRequested?.Invoke(shiftSessionId);
        }
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not null)
            OnPropertyChanged(e.PropertyName);

        if (e.PropertyName == nameof(AdminNotificationState.UnreadCount))
        {
            OnPropertyChanged(nameof(HasUnread));
            OnPropertyChanged(nameof(InboxSummary));
        }

        if (e.PropertyName == nameof(AdminNotificationState.ErrorMessage))
        {
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyState));
        }

        if (e.PropertyName == nameof(AdminNotificationState.IsLoading))
            OnPropertyChanged(nameof(ShowEmptyState));

        RefreshCommand.NotifyCanExecuteChanged();
        MarkReadCommand.NotifyCanExecuteChanged();
        MarkAllReadCommand.NotifyCanExecuteChanged();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(InboxSummary));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        State.PropertyChanged -= OnStatePropertyChanged;
        _observableItems.CollectionChanged -= OnItemsChanged;
    }
}
