namespace IntuneMonitor.Notifications;

using IntuneMonitor.Config;
using Microsoft.Extensions.Logging;

/// <summary>
/// Creates notification senders based on configuration.
/// </summary>
public static class NotificationFactory
{
    /// <summary>
    /// Inspects the <see cref="NotificationConfig"/> and creates a sender for each
    /// channel that has been configured with the required settings.
    /// </summary>
    /// <param name="config">Notification configuration.</param>
    /// <param name="loggerFactory">Logger factory for creating typed loggers.</param>
    /// <returns>A list of configured notification senders (may be empty).</returns>
    public static List<INotificationSender> Create(NotificationConfig config, ILoggerFactory loggerFactory)
    {
        var senders = new List<INotificationSender>();

        if (config.Teams != null && !string.IsNullOrWhiteSpace(config.Teams.WebhookUrl))
            senders.Add(new TeamsWebhookSender(config.Teams, loggerFactory.CreateLogger<TeamsWebhookSender>()));

        if (config.Slack != null && !string.IsNullOrWhiteSpace(config.Slack.WebhookUrl))
            senders.Add(new SlackWebhookSender(config.Slack, loggerFactory.CreateLogger<SlackWebhookSender>()));

        if (config.Email != null && !string.IsNullOrWhiteSpace(config.Email.SmtpServer) && config.Email.ToAddresses.Count > 0)
            senders.Add(new EmailNotificationSender(config.Email, loggerFactory.CreateLogger<EmailNotificationSender>()));

        return senders;
    }
}
