using AvaloniaApp.Services;

namespace AvaloniaApp.Tests;

internal sealed record RecordedNotification(string Type, string Title, string Message);

internal sealed class TestNotificationService : INotificationService
{
    public List<RecordedNotification> Notifications { get; } = [];

    public void ShowInformation(string title, string message) => Add("Information", title, message);
    public void ShowSuccess(string title, string message) => Add("Success", title, message);
    public void ShowWarning(string title, string message) => Add("Warning", title, message);
    public void ShowError(string title, string message) => Add("Error", title, message);

    private void Add(string type, string title, string message) =>
        Notifications.Add(new RecordedNotification(type, title, message));
}
