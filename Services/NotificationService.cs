namespace AvaloniaApp.Services;

public class NotificationService
{
    public event Action<NotificationMessage>? OnMessage;

    public void Show(string message, NotificationType type = NotificationType.Info)
    {
        OnMessage?.Invoke(new NotificationMessage(message, type));
    }

    public void Success(string message) => Show(message, NotificationType.Success);
    public void Error(string message) => Show(message, NotificationType.Error);
    public void Info(string message) => Show(message, NotificationType.Info);
    public void Warning(string message) => Show(message, NotificationType.Warning);
}

public record NotificationMessage(string Text, NotificationType Type);

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
