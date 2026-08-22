using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace AvaloniaApp.Services;

public interface INotificationService
{
    void ShowInformation(string title, string message);
    void ShowSuccess(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
}

public sealed class NotificationService(TopLevel host) : INotificationService
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromSeconds(4);
    private readonly WindowNotificationManager _manager = new(host)
    {
        Position = NotificationPosition.TopRight,
        MaxItems = 4
    };

    public void ShowInformation(string title, string message) => Show(title, message, NotificationType.Information);
    public void ShowSuccess(string title, string message) => Show(title, message, NotificationType.Success);
    public void ShowWarning(string title, string message) => Show(title, message, NotificationType.Warning);
    public void ShowError(string title, string message) => Show(title, message, NotificationType.Error);

    private void Show(string title, string message, NotificationType type) =>
        _manager.Show(new Notification(title, message, type, DefaultExpiration));
}
