using System.Text;
using System.Text.Json;

namespace IntuneMonitor.Notifications;

using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for notification senders that post JSON payloads to a webhook URL.
/// Handles the common HTTP POST, error handling, and logging. Subclasses provide
/// the channel name, webhook URL, and payload construction.
/// </summary>
public abstract class WebhookNotificationSender : INotificationSender
{
    private readonly HttpClient _httpClient;

    /// <summary>Logger for diagnostics.</summary>
    protected readonly ILogger Logger;

    /// <summary>JSON serialization options used for payload serialization.</summary>
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookNotificationSender"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="httpClient">Optional HttpClient for testing.</param>
    protected WebhookNotificationSender(ILogger logger, HttpClient? httpClient = null)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc />
    public abstract string ChannelName { get; }

    /// <summary>Gets the webhook URL to send notifications to.</summary>
    protected abstract string WebhookUrl { get; }

    /// <summary>
    /// Builds the platform-specific JSON payload for the given change report.
    /// </summary>
    /// <param name="report">The change report to format.</param>
    /// <returns>A serializable payload object.</returns>
    protected abstract object BuildPayload(ChangeReport report);

    /// <inheritdoc />
    public async Task SendAsync(ChangeReport report, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Building {Channel} payload for {ChangeCount} change(s)", ChannelName, report.TotalCount);

        var payload = BuildPayload(report);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(WebhookUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Logger.LogError("{Channel} webhook returned {StatusCode}: {Body}",
                ChannelName, (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        Logger.LogDebug("{Channel} webhook accepted the notification", ChannelName);
    }
}
