using System.Text;

namespace IntuneMonitor.Notifications;

using IntuneMonitor.Config;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sends drift detection notifications to Slack via an incoming webhook.
/// Posts a Block Kit message with a summary of detected changes.
/// </summary>
public class SlackWebhookSender : WebhookNotificationSender
{
    private readonly SlackWebhookConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlackWebhookSender"/> class.
    /// </summary>
    /// <param name="config">Slack webhook configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="httpClient">Optional HttpClient for testing.</param>
    public SlackWebhookSender(SlackWebhookConfig config, ILogger<SlackWebhookSender> logger, HttpClient? httpClient = null)
        : base(logger, httpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public override string ChannelName => "Slack";

    /// <inheritdoc />
    protected override string WebhookUrl => _config.WebhookUrl;

    /// <inheritdoc />
    protected override object BuildPayload(ChangeReport report) => BuildBlockKitPayload(report);

    // ----------------------------------------------------------------
    // Private helpers
    // ----------------------------------------------------------------

    private static object BuildBlockKitPayload(ChangeReport report)
    {
        var blocks = new List<object>
        {
            // Header
            new
            {
                type = "header",
                text = new
                {
                    type = "plain_text",
                    text = "IntuneMonitor \u2013 Configuration Drift Detected",
                    emoji = true
                }
            },
            // Summary section
            new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"*{report.TotalCount} change(s)* in tenant *{report.TenantName}*"
                }
            },
            // Counts as fields
            new
            {
                type = "section",
                fields = new object[]
                {
                    new { type = "mrkdwn", text = $"*Added:*\n{report.AddedCount}" },
                    new { type = "mrkdwn", text = $"*Removed:*\n{report.RemovedCount}" },
                    new { type = "mrkdwn", text = $"*Modified:*\n{report.ModifiedCount}" }
                }
            },
            new { type = "divider" }
        };

        // Change details (up to 10)
        var displayChanges = report.Changes.Take(10).ToList();
        if (displayChanges.Count > 0)
        {
            var lines = new StringBuilder();
            foreach (var change in displayChanges)
            {
                var icon = change.ChangeType switch
                {
                    ChangeType.Added => "\u271A",
                    ChangeType.Removed => "\u2716",
                    ChangeType.Modified => "\u270E",
                    _ => "\u2022"
                };
                lines.AppendLine($"{icon} *{change.ChangeType}* \u2013 [{change.ContentType}] {change.PolicyName}");
            }

            if (report.TotalCount > 10)
            {
                lines.AppendLine($"_... and {report.TotalCount - 10} more_");
            }

            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = lines.ToString().TrimEnd()
                }
            });
        }

        return new { blocks };
    }
}
