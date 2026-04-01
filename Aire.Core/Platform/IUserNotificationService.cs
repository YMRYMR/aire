namespace Aire.Platform;

/// <summary>
/// Cross-platform abstraction for surfacing lightweight user notifications.
/// </summary>
public interface IUserNotificationService
{
    Task ShowInfoAsync(string title, string message, CancellationToken cancellationToken = default);
}
