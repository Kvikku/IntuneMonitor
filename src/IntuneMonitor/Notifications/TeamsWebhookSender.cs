using System.Text;
using System.Text.Json;

namespace IntuneMonitor.Notifications;

using IntuneMonitor.Config;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sends drift detection notifications to Microsoft Teams via an incoming webhook.
/// Posts an Adaptive Card with a summary of detected changes.
/// </summary>
public class TeamsWebhookSender : INotificationSender
{
    private readonly TeamsWebhookConfig _config;
    private readonly ILogger<TeamsWebhookSender> _logger;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamsWebhookSender"/> class.
    /// </summary>
    /// <param name="config">Teams webhook configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="httpClient">Optional HttpClient for testing.</param>
    public TeamsWebhookSender(TeamsWebhookConfig config, ILogger<TeamsWebhookSender> logger, HttpClient? httpClient = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc />
    public string ChannelName => "Microsoft Teams";

    /// <inheritdoc />
    public async Task SendAsync(ChangeReport report, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Building Teams Adaptive Card for {ChangeCount} change(s)", report.TotalCount);

        var card = BuildAdaptiveCard(report);
        var payload = JsonSerializer.Serialize(card, JsonOptions);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(_config.WebhookUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Teams webhook returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogDebug("Teams webhook accepted the notification");
    }

    // ----------------------------------------------------------------
    // Private helpers
    // ----------------------------------------------------------------

    private static object BuildAdaptiveCard(ChangeReport report)
    {
        var bodyItems = new List<object>
        {
            new
            {
                type = "TextBlock",
                text = "IntuneMonitor \u2013 Configuration Drift Detected",
                weight = "Bolder",
                size = "Medium",
                wrap = true
            },
            new
            {
                type = "TextBlock",
                text = $"{report.TotalCount} change(s) detected in tenant {report.TenantName} at {report.GeneratedAt:u}",
                wrap = true,
                spacing = "Small"
            },
            new
            {
                type = "FactSet",
                facts = new object[]
                {
                    new { title = "Added", value = report.AddedCount.ToString() },
                    new { title = "Removed", value = report.RemovedCount.ToString() },
                    new { title = "Modified", value = report.ModifiedCount.ToString() }
                }
            }
        };

        var displayChanges = report.Changes.Take(10).ToList();
        foreach (var change in displayChanges)
        {
            var icon = change.ChangeType switch
            {
                ChangeType.Added => "\u271A",
                ChangeType.Removed => "\u2716",
                ChangeType.Modified => "\u270E",
                _ => "\u2022"
            };

            bodyItems.Add(new
            {
                type = "TextBlock",
                text = $"{icon} [{change.ContentType}] {change.PolicyName} \u2014 {change.ChangeType}",
                wrap = true,
                spacing = "Small"
            });
        }

        if (report.TotalCount > 10)
        {
            bodyItems.Add(new
            {
                type = "TextBlock",
                text = $"... and {report.TotalCount - 10} more",
                wrap = true,
                isSubtle = true,
                spacing = "Small"
            });
        }

        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    contentUrl = (string?)null,
                    content = new
                    {
                        schema = "https://adaptivecards.io/schemas/adaptive-card.json",
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = bodyItems
                    }
                }
            }
        };
    }
}
