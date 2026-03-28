namespace IntuneMonitor.Notifications;

using IntuneMonitor.Models;

/// <summary>
/// Interface for sending drift detection notifications.
/// </summary>
public interface INotificationSender
{
    /// <summary>The display name of this notification channel.</summary>
    string ChannelName { get; }

    /// <summary>
    /// Sends a notification about detected changes.
    /// </summary>
    Task SendAsync(ChangeReport report, CancellationToken cancellationToken = default);
}
