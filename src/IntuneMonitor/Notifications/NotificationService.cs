namespace IntuneMonitor.Notifications;

using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Dispatches change notifications to all configured notification channels.
/// </summary>
public class NotificationService
{
    private readonly List<INotificationSender> _senders;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    /// <param name="senders">Configured notification senders.</param>
    /// <param name="logger">Logger instance.</param>
    public NotificationService(IEnumerable<INotificationSender> senders, ILogger<NotificationService> logger)
    {
        _senders = senders.ToList();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Gets a value indicating whether any notification senders are configured.</summary>
    public bool HasSenders => _senders.Count > 0;

    /// <summary>
    /// Sends the change report to all configured notification channels.
    /// Failures on individual channels are logged but do not prevent other channels from being notified.
    /// </summary>
    /// <param name="report">The change report to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task NotifyAsync(ChangeReport report, CancellationToken cancellationToken = default)
    {
        if (!report.HasChanges) return;

        foreach (var sender in _senders)
        {
            try
            {
                await sender.SendAsync(report, cancellationToken);
                _logger.LogInformation("Notification sent via {Channel}", sender.ChannelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification via {Channel}", sender.ChannelName);
            }
        }
    }
}
