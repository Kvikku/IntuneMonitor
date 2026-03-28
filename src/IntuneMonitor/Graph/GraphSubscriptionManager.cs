using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace IntuneMonitor.Graph;

/// <summary>
/// Manages Microsoft Graph change notification subscriptions for Intune resources.
/// Subscriptions push change events to a configured webhook URL when policies are modified.
/// </summary>
public class GraphSubscriptionManager
{
    private readonly TokenCredential _credential;
    private readonly ILogger<GraphSubscriptionManager> _logger;
    private readonly string[] _scopes = { "https://graph.microsoft.com/.default" };

    /// <summary>
    /// Graph resources that support change notifications for Intune.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SupportedResources =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "DeviceConfigurations", "/deviceManagement/deviceConfigurations" },
            { "CompliancePolicies", "/deviceManagement/deviceCompliancePolicies" },
            { "ConfigurationPolicies", "/deviceManagement/configurationPolicies" },
            { "ConditionalAccess", "/identity/conditionalAccess/policies" },
        };

    public GraphSubscriptionManager(TokenCredential credential, ILogger<GraphSubscriptionManager> logger)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a Graph change notification subscription.
    /// </summary>
    /// <param name="resource">The Graph resource path to watch (e.g., "/deviceManagement/deviceConfigurations").</param>
    /// <param name="notificationUrl">The HTTPS webhook URL that will receive notifications.</param>
    /// <param name="changeTypes">Change types to subscribe to (e.g., "created", "updated", "deleted").</param>
    /// <param name="expirationMinutes">Minutes until the subscription expires (max 4230 for most resources).</param>
    /// <param name="clientState">Optional secret value included in notifications for validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The subscription ID if successful, null otherwise.</returns>
    public async Task<string?> CreateSubscriptionAsync(
        string resource,
        string notificationUrl,
        IEnumerable<string>? changeTypes = null,
        int expirationMinutes = 4230,
        string? clientState = null,
        CancellationToken cancellationToken = default)
    {
        var types = changeTypes?.ToList() ?? new List<string> { "created", "updated", "deleted" };

        var payload = new Dictionary<string, object>
        {
            ["changeType"] = string.Join(",", types),
            ["notificationUrl"] = notificationUrl,
            ["resource"] = resource,
            ["expirationDateTime"] = DateTime.UtcNow.AddMinutes(expirationMinutes).ToString("o"),
        };

        if (!string.IsNullOrWhiteSpace(clientState))
            payload["clientState"] = clientState;

        var json = JsonSerializer.Serialize(payload);
        _logger.LogInformation("Creating subscription for resource: {Resource}", resource);

        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(
                "https://graph.microsoft.com/v1.0/subscriptions",
                content,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                var id = result.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                _logger.LogInformation("Subscription created: {SubscriptionId} for {Resource}", id, resource);
                return id;
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create subscription for {Resource}: {StatusCode} {Error}",
                resource, response.StatusCode, error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription for {Resource}", resource);
            return null;
        }
    }

    /// <summary>
    /// Lists all active Graph change notification subscriptions.
    /// </summary>
    public async Task<List<SubscriptionInfo>> ListSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var subscriptions = new List<SubscriptionInfo>();

        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var response = await httpClient.GetAsync(
                "https://graph.microsoft.com/v1.0/subscriptions",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to list subscriptions: {StatusCode}", response.StatusCode);
                return subscriptions;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var root = JsonSerializer.Deserialize<JsonElement>(json);

            if (root.TryGetProperty("value", out var valueArray))
            {
                foreach (var item in valueArray.EnumerateArray())
                {
                    subscriptions.Add(new SubscriptionInfo
                    {
                        Id = GetStringProp(item, "id") ?? "",
                        Resource = GetStringProp(item, "resource") ?? "",
                        ChangeType = GetStringProp(item, "changeType") ?? "",
                        NotificationUrl = GetStringProp(item, "notificationUrl") ?? "",
                        ExpirationDateTime = item.TryGetProperty("expirationDateTime", out var exp)
                            && DateTime.TryParse(exp.GetString(), out var dt)
                            ? dt : DateTime.MinValue
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing subscriptions");
        }

        return subscriptions;
    }

    /// <summary>
    /// Deletes (unsubscribes) a Graph change notification subscription.
    /// </summary>
    public async Task<bool> DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var response = await httpClient.DeleteAsync(
                $"https://graph.microsoft.com/v1.0/subscriptions/{subscriptionId}",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Subscription deleted: {SubscriptionId}", subscriptionId);
                return true;
            }

            _logger.LogWarning("Failed to delete subscription {SubscriptionId}: {StatusCode}",
                subscriptionId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subscription {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    /// <summary>
    /// Renews (extends) an existing subscription's expiration.
    /// </summary>
    public async Task<bool> RenewSubscriptionAsync(
        string subscriptionId,
        int expirationMinutes = 4230,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object>
        {
            ["expirationDateTime"] = DateTime.UtcNow.AddMinutes(expirationMinutes).ToString("o")
        };

        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Patch,
                $"https://graph.microsoft.com/v1.0/subscriptions/{subscriptionId}")
            {
                Content = content
            };

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Subscription renewed: {SubscriptionId}", subscriptionId);
                return true;
            }

            _logger.LogWarning("Failed to renew subscription {SubscriptionId}: {StatusCode}",
                subscriptionId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing subscription {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    private async Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken)
    {
        var tokenRequestContext = new TokenRequestContext(_scopes);
        var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);

        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string? GetStringProp(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}

/// <summary>Information about an active Graph change notification subscription.</summary>
public record SubscriptionInfo
{
    /// <summary>The subscription ID.</summary>
    public required string Id { get; init; }

    /// <summary>The Graph resource being watched.</summary>
    public required string Resource { get; init; }

    /// <summary>The change types subscribed to.</summary>
    public required string ChangeType { get; init; }

    /// <summary>The webhook notification URL.</summary>
    public required string NotificationUrl { get; init; }

    /// <summary>When the subscription expires.</summary>
    public DateTime ExpirationDateTime { get; init; }
}
