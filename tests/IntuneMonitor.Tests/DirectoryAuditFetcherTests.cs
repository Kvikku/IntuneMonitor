using System.Net;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for <see cref="DirectoryAuditFetcher"/> covering pagination, error handling,
/// event parsing, and the monitored activity list.
/// </summary>
public class DirectoryAuditFetcherTests
{
    private readonly MockHttpHandler _handler = new();
    private readonly DirectoryAuditFetcher _fetcher;

    public DirectoryAuditFetcherTests()
    {
        _fetcher = new DirectoryAuditFetcher(GraphTestHelpers.FakeCredential, GraphTestHelpers.FakeGraphClientFactory);
        _fetcher.HttpClientFactory = GraphTestHelpers.CreateClientFactory(_handler);
        _fetcher.DelayFunc = (_, _) => Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // Input validation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(31)]
    [InlineData(100)]
    public async Task FetchDirectoryAuditEventsAsync_InvalidDays_ThrowsArgumentOutOfRangeException(int days)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _fetcher.FetchDirectoryAuditEventsAsync(days));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(30)]
    public async Task FetchDirectoryAuditEventsAsync_ValidDaysRange_DoesNotThrow(int days)
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(days);
        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // Pagination
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_SinglePage_ReturnsEvents()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "d1",
                    activityDisplayName = "Add owner to application",
                    activityDateTime = "2024-06-15T10:00:00Z",
                    category = "ApplicationManagement",
                    result = "success",
                    operationType = "Add",
                    correlationId = "corr-1"
                },
                new
                {
                    id = "d2",
                    activityDisplayName = "Update application",
                    activityDateTime = "2024-06-15T11:00:00Z",
                    category = "ApplicationManagement",
                    result = "success",
                    operationType = "Update",
                    correlationId = "corr-2"
                }
            }
        });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        Assert.Equal(2, result.Count);
        Assert.Equal("d1", result[0].Id);
        Assert.Equal("Add owner to application", result[0].ActivityDisplayName);
        Assert.Equal("ApplicationManagement", result[0].Category);
        Assert.Equal("success", result[0].Result);
        Assert.Equal("Add", result[0].OperationType);
        Assert.Equal("corr-1", result[0].CorrelationId);
    }

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_MultiplePagesFollowsNextLink()
    {
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "value": [
                    {"id":"d1","activityDisplayName":"Add owner to application","activityDateTime":"2024-06-15T10:00:00Z","category":"ApplicationManagement","result":"success","operationType":"Add","correlationId":"c1"}
                ],
                "@odata.nextLink": "https://graph.microsoft.com/v1.0/auditLogs/directoryAudits?$skiptoken=page2"
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "d2", activityDisplayName = "Remove owner from application",
                    activityDateTime = "2024-06-15T11:00:00Z",
                    category = "ApplicationManagement", result = "success",
                    operationType = "Delete", correlationId = "c2"
                }
            }
        });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        Assert.Equal(2, result.Count);
        Assert.Equal("d1", result[0].Id);
        Assert.Equal("d2", result[1].Id);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_EmptyPage_ReturnsEmptyList()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // Throttling
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_429Throttled_RetriesAfterDelay()
    {
        _handler.Enqueue429(1);
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "d1", activityDisplayName = "Add owner to application",
                    activityDateTime = "2024-06-15T10:00:00Z",
                    category = "ApplicationManagement", result = "success",
                    operationType = "Add", correlationId = "c1"
                }
            }
        });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        Assert.Single(result);
        Assert.Equal("d1", result[0].Id);
        Assert.Equal(2, _handler.Requests.Count);
    }

    // -----------------------------------------------------------------------
    // Error handling
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_NonRetryableError_ReturnsEmpty()
    {
        _handler.EnqueueError(HttpStatusCode.Forbidden, "Access denied");

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        Assert.Empty(result);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_ServerError_RetriesThenSucceeds()
    {
        _handler.EnqueueError(HttpStatusCode.InternalServerError, "Server error");
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "d1", activityDisplayName = "Add owner to application",
                    activityDateTime = "2024-06-15T10:00:00Z",
                    category = "ApplicationManagement", result = "success",
                    operationType = "Add", correlationId = "c1"
                }
            }
        });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        Assert.Single(result);
        Assert.Equal(2, _handler.Requests.Count);
    }

    // -----------------------------------------------------------------------
    // Event parsing — initiatedBy and targetResources
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_ParsesInitiatedByUser()
    {
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "value": [{
                    "id": "d1",
                    "activityDisplayName": "Add owner to application",
                    "activityDateTime": "2024-06-15T10:00:00Z",
                    "category": "ApplicationManagement",
                    "result": "success",
                    "operationType": "Add",
                    "correlationId": "c1",
                    "initiatedBy": {
                        "user": {
                            "userPrincipalName": "admin@contoso.com",
                            "displayName": "Admin User",
                            "id": "user-id-1",
                            "ipAddress": "10.0.0.1"
                        }
                    },
                    "targetResources": [
                        {
                            "id": "app-id-1",
                            "displayName": "My App",
                            "type": "Application",
                            "modifiedProperties": [
                                {
                                    "displayName": "Owner",
                                    "oldValue": "[]",
                                    "newValue": "[\"admin@contoso.com\"]"
                                }
                            ]
                        }
                    ]
                }]
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        var evt = Assert.Single(result);
        Assert.NotNull(evt.InitiatedBy);
        Assert.Equal("admin@contoso.com", evt.InitiatedBy!.UserPrincipalName);
        Assert.Equal("Admin User", evt.InitiatedBy.UserDisplayName);
        Assert.Equal("user-id-1", evt.InitiatedBy.UserId);
        Assert.Equal("10.0.0.1", evt.InitiatedBy.IpAddress);

        Assert.Single(evt.TargetResources);
        Assert.Equal("app-id-1", evt.TargetResources[0].Id);
        Assert.Equal("My App", evt.TargetResources[0].DisplayName);
        Assert.Equal("Application", evt.TargetResources[0].Type);

        Assert.Single(evt.TargetResources[0].ModifiedProperties);
        Assert.Equal("Owner", evt.TargetResources[0].ModifiedProperties[0].DisplayName);
        Assert.Equal("[\"admin@contoso.com\"]", evt.TargetResources[0].ModifiedProperties[0].NewValue);
    }

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_ParsesInitiatedByApp()
    {
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "value": [{
                    "id": "d1",
                    "activityDisplayName": "Consent to application",
                    "activityDateTime": "2024-06-15T10:00:00Z",
                    "category": "ApplicationManagement",
                    "result": "success",
                    "operationType": "Assign",
                    "correlationId": "c1",
                    "initiatedBy": {
                        "app": {
                            "displayName": "Azure Portal",
                            "appId": "app-guid-1"
                        }
                    }
                }]
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        var evt = Assert.Single(result);
        Assert.NotNull(evt.InitiatedBy);
        Assert.Null(evt.InitiatedBy!.UserPrincipalName);
        Assert.Equal("Azure Portal", evt.InitiatedBy.AppDisplayName);
        Assert.Equal("app-guid-1", evt.InitiatedBy.AppId);
    }

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_MissingInitiatedBy_ReturnsNull()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "d1", activityDisplayName = "Update application",
                    activityDateTime = "2024-06-15T10:00:00Z",
                    category = "ApplicationManagement", result = "success",
                    operationType = "Update", correlationId = "c1"
                }
            }
        });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        var evt = Assert.Single(result);
        Assert.Null(evt.InitiatedBy);
    }

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_MissingTargetResources_ReturnsEmptyList()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "d1", activityDisplayName = "Update application",
                    activityDateTime = "2024-06-15T10:00:00Z",
                    category = "ApplicationManagement", result = "success",
                    operationType = "Update", correlationId = "c1"
                }
            }
        });

        var result = await _fetcher.FetchDirectoryAuditEventsAsync(7);

        var evt = Assert.Single(result);
        Assert.Empty(evt.TargetResources);
    }

    // -----------------------------------------------------------------------
    // Monitored activities
    // -----------------------------------------------------------------------

    [Fact]
    public void MonitoredActivities_ContainsExpectedEntries()
    {
        Assert.Contains("Add owner to application", DirectoryAuditFetcher.MonitoredActivities);
        Assert.Contains("Remove owner from application", DirectoryAuditFetcher.MonitoredActivities);
        Assert.Contains("Add owner to service principal", DirectoryAuditFetcher.MonitoredActivities);
        Assert.Contains("Remove owner from service principal", DirectoryAuditFetcher.MonitoredActivities);
        Assert.Contains("Add app role assignment to service principal", DirectoryAuditFetcher.MonitoredActivities);
        Assert.Contains("Consent to application", DirectoryAuditFetcher.MonitoredActivities);
        Assert.Contains("Update application", DirectoryAuditFetcher.MonitoredActivities);
    }

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_RequestUrlContainsFilterForMonitoredActivities()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        await _fetcher.FetchDirectoryAuditEventsAsync(7);

        var requestUrl = _handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("auditLogs/directoryAudits", requestUrl);
        Assert.Contains("activityDateTime", requestUrl);
        Assert.Contains("activityDisplayName", requestUrl);
    }

    [Fact]
    public async Task FetchDirectoryAuditEventsAsync_UsesV1Endpoint()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        await _fetcher.FetchDirectoryAuditEventsAsync(7);

        var requestUrl = _handler.Requests[0].RequestUri!.ToString();
        Assert.StartsWith("https://graph.microsoft.com/v1.0/", requestUrl);
    }
}
