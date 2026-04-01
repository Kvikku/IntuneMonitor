using System.Net;
using System.Text.Json;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Integration tests for <see cref="AuditLogFetcher"/> using a mock HttpMessageHandler.
/// Covers pagination, 429 throttling with Retry-After, error handling, and event parsing.
/// </summary>
public class AuditLogFetcherTests
{
    private readonly MockHttpHandler _handler = new();
    private readonly AuditLogFetcher _fetcher;

    public AuditLogFetcherTests()
    {
        _fetcher = new AuditLogFetcher(GraphTestHelpers.FakeCredential);
        _fetcher.HttpClientFactory = GraphTestHelpers.CreateClientFactory(_handler);
        _fetcher.DelayFunc = (_, _) => Task.CompletedTask; // bypass real delays in tests
    }

    // -----------------------------------------------------------------------
    // Input validation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(31)]
    [InlineData(100)]
    public async Task FetchAuditEventsAsync_InvalidDays_ThrowsArgumentOutOfRangeException(int days)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fetcher.FetchAuditEventsAsync(days));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(30)]
    public async Task FetchAuditEventsAsync_ValidDaysRange_DoesNotThrow(int days)
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _fetcher.FetchAuditEventsAsync(days);
        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // Pagination
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchAuditEventsAsync_SinglePage_ReturnsEvents()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "evt1",
                    displayName = "Event 1",
                    componentName = "DeviceConfiguration",
                    activity = "Create Policy",
                    activityType = "Create",
                    activityResult = "Success",
                    activityDateTime = "2024-06-15T10:00:00Z"
                },
                new
                {
                    id = "evt2",
                    displayName = "Event 2",
                    componentName = "DeviceConfiguration",
                    activity = "Update Policy",
                    activityType = "Patch",
                    activityResult = "Success",
                    activityDateTime = "2024-06-15T11:00:00Z"
                }
            }
        });

        var result = await _fetcher.FetchAuditEventsAsync(7);

        Assert.Equal(2, result.Count);
        Assert.Equal("evt1", result[0].Id);
        Assert.Equal("Event 1", result[0].DisplayName);
        Assert.Equal("DeviceConfiguration", result[0].ComponentName);
        Assert.Equal("Create Policy", result[0].Activity);
        Assert.Equal("Create", result[0].ActivityType);
        Assert.Equal("Success", result[0].ActivityResult);
    }

    [Fact]
    public async Task FetchAuditEventsAsync_MultiplePagesFollowsNextLink()
    {
        // Page 1 with nextLink
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "value": [
                    {"id":"evt1","displayName":"Event 1","componentName":"DC","activity":"Create","activityType":"Create","activityResult":"Success","activityDateTime":"2024-06-15T10:00:00Z"}
                ],
                "@odata.nextLink": "https://graph.microsoft.com/beta/deviceManagement/auditEvents?$skiptoken=page2"
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });
        // Page 2 (no nextLink)
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "evt2", displayName = "Event 2", componentName = "DC",
                    activity = "Update", activityType = "Patch",
                    activityResult = "Success", activityDateTime = "2024-06-15T11:00:00Z"
                }
            }
        });

        var result = await _fetcher.FetchAuditEventsAsync(7);

        Assert.Equal(2, result.Count);
        Assert.Equal("evt1", result[0].Id);
        Assert.Equal("evt2", result[1].Id);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task FetchAuditEventsAsync_EmptyPage_ReturnsEmptyList()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _fetcher.FetchAuditEventsAsync(7);

        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // Throttling (429)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchAuditEventsAsync_429Throttled_RetriesAfterDelay()
    {
        // First request: 429 with Retry-After: 1 second
        _handler.Enqueue429(1);
        // Second request: success
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "evt1", displayName = "Event 1", componentName = "DC",
                    activity = "Create", activityType = "Create",
                    activityResult = "Success", activityDateTime = "2024-06-15T10:00:00Z"
                }
            }
        });

        var result = await _fetcher.FetchAuditEventsAsync(7);

        Assert.Single(result);
        Assert.Equal("evt1", result[0].Id);
        // Should have made 2 requests (429 + retry)
        Assert.Equal(2, _handler.Requests.Count);
    }

    // -----------------------------------------------------------------------
    // Error handling
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchAuditEventsAsync_NonRetryableError_ReturnsEmpty()
    {
        // 403 is not retryable
        _handler.EnqueueError(HttpStatusCode.Forbidden, "Access denied");

        var result = await _fetcher.FetchAuditEventsAsync(7);

        Assert.Empty(result);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task FetchAuditEventsAsync_NotFoundError_ReturnsEmpty()
    {
        _handler.EnqueueError(HttpStatusCode.NotFound, "Not found");

        var result = await _fetcher.FetchAuditEventsAsync(7);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchAuditEventsAsync_ServerError_RetriesThenSucceeds()
    {
        // First request: 500 (retryable)
        _handler.EnqueueError(HttpStatusCode.InternalServerError, "Server error");
        // Second request: success
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "evt1", displayName = "Event 1", componentName = "DC",
                    activity = "Create", activityType = "Create",
                    activityResult = "Success", activityDateTime = "2024-06-15T10:00:00Z"
                }
            }
        });

        var result = await _fetcher.FetchAuditEventsAsync(7);

        Assert.Single(result);
        Assert.Equal(2, _handler.Requests.Count);
    }

    // -----------------------------------------------------------------------
    // Event parsing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchAuditEventsAsync_ParsesActorAndResources()
    {
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "value": [{
                    "id": "evt1",
                    "displayName": "Event 1",
                    "componentName": "DeviceConfiguration",
                    "activity": "Create Policy",
                    "activityType": "Create",
                    "activityResult": "Success",
                    "activityDateTime": "2024-06-15T10:00:00Z",
                    "actor": {
                        "applicationDisplayName": "IntunePortal",
                        "userPrincipalName": "admin@contoso.com",
                        "ipAddress": "10.0.0.1"
                    },
                    "resources": [
                        {
                            "displayName": "WiFi Profile",
                            "type": "DeviceConfiguration"
                        }
                    ]
                }]
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });

        var result = await _fetcher.FetchAuditEventsAsync(7);

        var evt = Assert.Single(result);
        Assert.NotNull(evt.Actor);
        Assert.Equal("IntunePortal", evt.Actor!.ApplicationDisplayName);
        Assert.Equal("admin@contoso.com", evt.Actor.UserPrincipalName);
        Assert.Equal("10.0.0.1", evt.Actor.IpAddress);

        Assert.Single(evt.Resources);
        Assert.Equal("WiFi Profile", evt.Resources[0].DisplayName);
        Assert.Equal("DeviceConfiguration", evt.Resources[0].ResourceType);
    }

    [Fact]
    public async Task FetchAuditEventsAsync_MissingActor_ReturnsNullActor()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "evt1", displayName = "Event 1", componentName = "DC",
                    activity = "Create", activityType = "Create",
                    activityResult = "Success", activityDateTime = "2024-06-15T10:00:00Z"
                    // No actor
                }
            }
        });

        var result = await _fetcher.FetchAuditEventsAsync(7);

        var evt = Assert.Single(result);
        Assert.Null(evt.Actor);
    }

    [Fact]
    public async Task FetchAuditEventsAsync_MissingResources_ReturnsEmptyList()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "evt1", displayName = "Event 1", componentName = "DC",
                    activity = "Create", activityType = "Create",
                    activityResult = "Success", activityDateTime = "2024-06-15T10:00:00Z"
                    // No resources
                }
            }
        });

        var result = await _fetcher.FetchAuditEventsAsync(7);

        var evt = Assert.Single(result);
        Assert.Empty(evt.Resources);
    }

    [Fact]
    public async Task FetchAuditEventsAsync_ParsesActivityDateTime()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "evt1", displayName = "Event 1", componentName = "DC",
                    activity = "Create", activityType = "Create",
                    activityResult = "Success", activityDateTime = "2024-06-15T10:30:00Z"
                }
            }
        });

        var result = await _fetcher.FetchAuditEventsAsync(7);

        var evt = Assert.Single(result);
        Assert.Equal(2024, evt.ActivityDateTime.Year);
        Assert.Equal(6, evt.ActivityDateTime.Month);
        Assert.Equal(15, evt.ActivityDateTime.Day);
    }

    // -----------------------------------------------------------------------
    // Request URL validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchAuditEventsAsync_UsesCorrectEndpointAndFilter()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        await _fetcher.FetchAuditEventsAsync(7);

        var requestUrl = _handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("deviceManagement/auditEvents", requestUrl);
        Assert.Contains("$filter", requestUrl);
        Assert.Contains("$orderby", requestUrl);
        Assert.Contains("$top=100", requestUrl);
    }

    // -----------------------------------------------------------------------
    // Constructor validation
    // -----------------------------------------------------------------------

    [Fact]
    public void AuditLogFetcher_NullCredential_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditLogFetcher(null!));
    }
}
