using System.Net;
using System.Text.Json;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Integration tests for <see cref="IntuneExporter"/> using a mock HttpMessageHandler.
/// Covers pagination, SettingsCatalog special handling, assignment resolution, and errors.
/// </summary>
public class IntuneExporterTests
{
    private readonly MockHttpHandler _handler = new();
    private readonly IntuneExporter _exporter;

    public IntuneExporterTests()
    {
        _exporter = new IntuneExporter(GraphTestHelpers.FakeCredential);
        _exporter.HttpClientFactory = GraphTestHelpers.CreateClientFactory(_handler);
    }

    // -----------------------------------------------------------------------
    // Pagination
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExportContentTypeAsync_SinglePage_ReturnsItems()
    {
        // Use AssignmentFilter to skip assignment fetching
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new { id = "1", displayName = "Filter A" },
                new { id = "2", displayName = "Filter B" }
            }
        });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.AssignmentFilter);

        Assert.Equal(2, result.Count);
        Assert.Equal("Filter A", result[0].Name);
        Assert.Equal("Filter B", result[1].Name);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("2", result[1].Id);
        Assert.Equal(IntuneContentTypes.AssignmentFilter, result[0].ContentType);
        Assert.Single(_handler.Requests); // Only one HTTP call
    }

    [Fact]
    public async Task ExportContentTypeAsync_MultiplePagesFollowsNextLink()
    {
        var handler = new MockHttpHandler();
        var page1 = """
        {
            "value": [{"id": "1", "displayName": "Filter A"}],
            "@odata.nextLink": "https://graph.microsoft.com/beta/deviceManagement/assignmentFilters?$skiptoken=page2"
        }
        """;
        var page2 = """
        {
            "value": [{"id": "2", "displayName": "Filter B"}]
        }
        """;
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(page1, System.Text.Encoding.UTF8, "application/json")
        });
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(page2, System.Text.Encoding.UTF8, "application/json")
        });

        var exporter = new IntuneExporter(GraphTestHelpers.FakeCredential);
        exporter.HttpClientFactory = GraphTestHelpers.CreateClientFactory(handler);

        var result = await exporter.ExportContentTypeAsync(IntuneContentTypes.AssignmentFilter);

        Assert.Equal(2, result.Count);
        Assert.Equal("Filter A", result[0].Name);
        Assert.Equal("Filter B", result[1].Name);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ExportContentTypeAsync_EmptyValueArray_ReturnsEmptyList()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.AssignmentFilter);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExportContentTypeAsync_MissingValueProperty_ReturnsEmptyList()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { count = 0 });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.AssignmentFilter);

        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // Content type validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExportContentTypeAsync_UnsupportedContentType_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _exporter.ExportContentTypeAsync("UnsupportedType"));
        Assert.Contains("Unsupported content type", ex.Message);
    }

    // -----------------------------------------------------------------------
    // HTTP error handling
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExportContentTypeAsync_HttpError_ThrowsHttpRequestException()
    {
        _handler.EnqueueError(HttpStatusCode.Forbidden, "Access denied");

        // EnsureSuccessStatusCode throws HttpRequestException on non-2xx
        await Assert.ThrowsAsync<HttpRequestException>(
            () => _exporter.ExportContentTypeAsync(IntuneContentTypes.AssignmentFilter));
    }

    [Fact]
    public async Task ExportAllAsync_CatchesPerTypeErrors_ReturnsEmptyListForFailed()
    {
        // First content type: error (403)
        _handler.EnqueueError(HttpStatusCode.Forbidden, "Access denied");
        // Second content type: success
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "1", displayName = "Policy1" } }
        });
        // Assignments for second type's item
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var types = new[] { IntuneContentTypes.AssignmentFilter, IntuneContentTypes.ConditionalAccessPolicy };
        var exporter = new IntuneExporter(GraphTestHelpers.FakeCredential);
        exporter.HttpClientFactory = GraphTestHelpers.CreateClientFactory(_handler);

        var result = await exporter.ExportAllAsync(types);

        Assert.Equal(2, result.Count);
        Assert.Empty(result[IntuneContentTypes.AssignmentFilter]);
        Assert.Single(result[IntuneContentTypes.ConditionalAccessPolicy]);
    }

    // -----------------------------------------------------------------------
    // Detail fetch (NeedsDetailFetch)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExportContentTypeAsync_NonDetailType_UsesListItemDirectly()
    {
        // DeviceCompliancePolicy doesn't need detail fetch
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "1", displayName = "Compliance", description = "Test", extraField = "value" } }
        });
        // Assignments for the item
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.DeviceCompliancePolicy);

        Assert.Single(result);
        Assert.Equal("Compliance", result[0].Name);
        // Only 2 requests: list + assignments
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task ExportContentTypeAsync_DetailType_FetchesIndividualItem()
    {
        // PowerShellScript needs detail fetch
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "ps1", displayName = "Script1" } }
        });
        // Detail fetch
        _handler.Enqueue(HttpStatusCode.OK, new { id = "ps1", displayName = "Script1", scriptContent = "Write-Host Hello" });
        // Assignments
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.PowerShellScript);

        Assert.Single(result);
        Assert.Equal("Script1", result[0].Name);
        // 3 requests: list + detail + assignments
        Assert.Equal(3, _handler.Requests.Count);
        Assert.Contains("/deviceManagementScripts/ps1", _handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task ExportContentTypeAsync_DetailFetchFails_ReturnsNullPolicyData()
    {
        // SettingsCatalog needs detail fetch
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "sc1", displayName = "Catalog1" } }
        });
        // Detail fetch fails
        _handler.EnqueueError(HttpStatusCode.NotFound, "Not found");

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.SettingsCatalog);

        Assert.Single(result);
        // PolicyData is null when detail fetch fails (fullData = null → no settings or assignments merge)
        Assert.Null(result[0].PolicyData);
    }

    // -----------------------------------------------------------------------
    // SettingsCatalog special handling
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExportContentTypeAsync_SettingsCatalog_MergesSettings()
    {
        // List
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "sc1", displayName = "CatalogPolicy" } }
        });
        // Detail fetch
        _handler.Enqueue(HttpStatusCode.OK, new { id = "sc1", displayName = "CatalogPolicy", platforms = "windows10" });
        // Settings
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new { settingInstance = new { settingDefinitionId = "setting1" } }
            }
        });
        // Assignments (empty)
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.SettingsCatalog);

        Assert.Single(result);
        var policyJson = result[0].PolicyData!.Value;

        // Verify settings were merged
        Assert.True(policyJson.TryGetProperty("settings", out var settings));
        Assert.Equal(JsonValueKind.Array, settings.ValueKind);
        Assert.Equal(1, settings.GetArrayLength());

        // Verify settings endpoint was called
        Assert.Contains("/configurationPolicies/sc1/settings", _handler.Requests[2].RequestUri!.ToString());
    }

    [Fact]
    public async Task ExportContentTypeAsync_SettingsCatalog_PaginatedSettings()
    {
        // List
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "sc1", displayName = "CatalogPolicy" } }
        });
        // Detail fetch
        _handler.Enqueue(HttpStatusCode.OK, new { id = "sc1", displayName = "CatalogPolicy" });
        // Settings page 1 with nextLink
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "value": [{"settingInstance":{"settingDefinitionId":"s1"}}],
                "@odata.nextLink": "https://graph.microsoft.com/beta/deviceManagement/configurationPolicies/sc1/settings?$skip=1"
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });
        // Settings page 2
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { settingInstance = new { settingDefinitionId = "s2" } } }
        });
        // Assignments
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.SettingsCatalog);

        Assert.Single(result);
        var settings = result[0].PolicyData!.Value.GetProperty("settings");
        Assert.Equal(2, settings.GetArrayLength());
    }

    [Fact]
    public async Task ExportContentTypeAsync_SettingsCatalog_SettingsFetchFails_ReturnsPolicyWithoutSettings()
    {
        // List
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "sc1", displayName = "CatalogPolicy" } }
        });
        // Detail fetch succeeds
        _handler.Enqueue(HttpStatusCode.OK, new { id = "sc1", displayName = "CatalogPolicy", platforms = "windows10" });
        // Settings fetch fails
        _handler.EnqueueError(HttpStatusCode.InternalServerError, "Server error");
        // Assignments still called after settings failure
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.SettingsCatalog);

        Assert.Single(result);
        // Policy data should still be present (returned as-is from detail)
        Assert.NotNull(result[0].PolicyData);
        Assert.Equal("CatalogPolicy", result[0].Name);
    }

    // -----------------------------------------------------------------------
    // Assignment resolution
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExportContentTypeAsync_MergesAssignmentsWithGroupNames()
    {
        var groupId = "00000000-0000-0000-0000-000000000001";

        // List (DeviceCompliancePolicy — no detail fetch needed)
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "pol1", displayName = "CompPolicy" } }
        });
        // Assignments with one group target
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "assign1",
                    target = new
                    {
                        groupId = groupId,
                        // @odata.type is special — will be serialized literally
                    }
                }
            }
        });
        // Group name resolution
        _handler.Enqueue(HttpStatusCode.OK, new { displayName = "Marketing Team" });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.DeviceCompliancePolicy);

        Assert.Single(result);
        var policy = result[0].PolicyData!.Value;
        Assert.True(policy.TryGetProperty("assignments", out var assignments));
        Assert.Equal(1, assignments.GetArrayLength());

        var target = assignments[0].GetProperty("target");
        Assert.Equal("Marketing Team", target.GetProperty("groupDisplayName").GetString());

        // Verify group resolution URL
        Assert.Contains($"/groups/{groupId}", _handler.Requests[2].RequestUri!.ToString());
    }

    [Fact]
    public async Task ExportContentTypeAsync_WellKnownGroupId_ResolvesWithoutApiCall()
    {
        var allUsersId = "acacacac-9df4-4c7d-9d50-4ef0226f57a9";

        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "pol1", displayName = "CompPolicy" } }
        });
        // Assignments with well-known "All Users" group
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "assign1",
                    target = new { groupId = allUsersId }
                }
            }
        });
        // No group resolution request expected — well-known IDs are resolved locally

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.DeviceCompliancePolicy);

        Assert.Single(result);
        var target = result[0].PolicyData!.Value.GetProperty("assignments")[0].GetProperty("target");
        Assert.Equal("All Users", target.GetProperty("groupDisplayName").GetString());

        // Only 2 requests (list + assignments), no group resolution call
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task ExportContentTypeAsync_VirtualTargetAssignment_ResolvesDisplayName()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "pol1", displayName = "CompPolicy" } }
        });
        // Use raw JSON so we can include @odata.type in the target
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "value": [{
                    "id": "assign1",
                    "target": {
                        "@odata.type": "#microsoft.graph.allLicensedUsersAssignmentTarget"
                    }
                }]
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.DeviceCompliancePolicy);

        Assert.Single(result);
        var target = result[0].PolicyData!.Value.GetProperty("assignments")[0].GetProperty("target");
        Assert.Equal("All Users", target.GetProperty("groupDisplayName").GetString());

        // Only 2 requests: list + assignments (no group API call)
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task ExportContentTypeAsync_AllDevicesVirtualTarget_ResolvesCorrectly()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "pol1", displayName = "CompPolicy" } }
        });
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "value": [{
                    "id": "assign1",
                    "target": {
                        "@odata.type": "#microsoft.graph.allDevicesAssignmentTarget"
                    }
                }]
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.DeviceCompliancePolicy);

        var target = result[0].PolicyData!.Value.GetProperty("assignments")[0].GetProperty("target");
        Assert.Equal("All Devices", target.GetProperty("groupDisplayName").GetString());
    }

    [Fact]
    public async Task ExportContentTypeAsync_GroupResolutionFails_UsesRawGroupId()
    {
        var groupId = "00000000-0000-0000-0000-000000000099";

        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "pol1", displayName = "CompPolicy" } }
        });
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "assign1",
                    target = new { groupId = groupId }
                }
            }
        });
        // Group resolution fails with 404
        _handler.EnqueueError(HttpStatusCode.NotFound, "Group not found");

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.DeviceCompliancePolicy);

        Assert.Single(result);
        var target = result[0].PolicyData!.Value.GetProperty("assignments")[0].GetProperty("target");
        // Falls back to raw group ID
        Assert.Equal(groupId, target.GetProperty("groupDisplayName").GetString());
    }

    [Fact]
    public async Task ExportContentTypeAsync_GroupNamesCached_OnlyOneApiCallPerGroup()
    {
        var groupId = "00000000-0000-0000-0000-000000000001";

        // List with 2 items
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new { id = "pol1", displayName = "Policy1" },
                new { id = "pol2", displayName = "Policy2" }
            }
        });
        // Assignments for pol1
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "a1", target = new { groupId = groupId } } }
        });
        // Group resolution (called once)
        _handler.Enqueue(HttpStatusCode.OK, new { displayName = "Engineering" });
        // Assignments for pol2 (same group)
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "a2", target = new { groupId = groupId } } }
        });
        // No second group resolution — cached!

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.DeviceCompliancePolicy);

        Assert.Equal(2, result.Count);
        // Both should have the resolved group name
        var target1 = result[0].PolicyData!.Value.GetProperty("assignments")[0].GetProperty("target");
        var target2 = result[1].PolicyData!.Value.GetProperty("assignments")[0].GetProperty("target");
        Assert.Equal("Engineering", target1.GetProperty("groupDisplayName").GetString());
        Assert.Equal("Engineering", target2.GetProperty("groupDisplayName").GetString());

        // 4 requests: list + assignments×2 + 1 group resolution (not 2)
        Assert.Equal(4, _handler.Requests.Count);
    }

    [Fact]
    public async Task ExportContentTypeAsync_AssignmentsFetchFails_ReturnsPolicyAsIs()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "pol1", displayName = "CompPolicy", enabled = true } }
        });
        // Assignments fetch returns error (non-success breaks loop gracefully)
        _handler.EnqueueError(HttpStatusCode.Forbidden, "Not authorized for assignments");

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.DeviceCompliancePolicy);

        Assert.Single(result);
        Assert.NotNull(result[0].PolicyData);
        Assert.Equal("CompPolicy", result[0].Name);
        // Policy data should have the original fields — assignments key with empty array
        // (MergeAssignmentsAsync catches errors and returns policyDetail, but the empty-assignments
        // response causes the break from the while loop, resulting in an empty assignments merge)
    }

    [Fact]
    public async Task ExportContentTypeAsync_NoAssignmentsType_SkipsAssignments()
    {
        // AssignmentFilter is in NoAssignmentsTypes
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "f1", displayName = "MyFilter" } }
        });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.AssignmentFilter);

        Assert.Single(result);
        Assert.Equal("MyFilter", result[0].Name);
        // Only 1 request (list) — no assignments fetch
        Assert.Single(_handler.Requests);
    }

    [Theory]
    [InlineData(IntuneContentTypes.ConditionalAccessPolicy)]
    [InlineData(IntuneContentTypes.RoleDefinition)]
    [InlineData(IntuneContentTypes.NamedLocation)]
    [InlineData(IntuneContentTypes.EnrollmentRestriction)]
    public async Task ExportContentTypeAsync_AllNoAssignmentTypes_SkipAssignments(string contentType)
    {
        var handler = new MockHttpHandler();
        handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "item1", displayName = "Item1" } }
        });

        var exporter = new IntuneExporter(GraphTestHelpers.FakeCredential);
        exporter.HttpClientFactory = GraphTestHelpers.CreateClientFactory(handler);

        var result = await exporter.ExportContentTypeAsync(contentType);

        Assert.Single(result);
        // Only 1 request (list) — no assignments
        Assert.Single(handler.Requests);
    }

    // -----------------------------------------------------------------------
    // ExportAllAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExportAllAsync_MultipleTypes_ReturnsAllResults()
    {
        // AssignmentFilter: success
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "f1", displayName = "Filter1" } }
        });
        // ConditionalAccessPolicy: success
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "ca1", displayName = "CA1" }, new { id = "ca2", displayName = "CA2" } }
        });

        var types = new[] { IntuneContentTypes.AssignmentFilter, IntuneContentTypes.ConditionalAccessPolicy };
        var result = await _exporter.ExportAllAsync(types);

        Assert.Equal(2, result.Count);
        Assert.Single(result[IntuneContentTypes.AssignmentFilter]);
        Assert.Equal(2, result[IntuneContentTypes.ConditionalAccessPolicy].Count);
    }

    // -----------------------------------------------------------------------
    // Metadata extraction
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExportContentTypeAsync_ExtractsMetadataCorrectly()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new
                {
                    id = "item1",
                    displayName = "Test Policy",
                    description = "A test policy",
                    platforms = "windows10",
                    createdDateTime = "2024-01-15T10:00:00Z",
                    lastModifiedDateTime = "2024-06-20T14:30:00Z"
                }
            }
        });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.AssignmentFilter);

        var item = Assert.Single(result);
        Assert.Equal("item1", item.Id);
        Assert.Equal("Test Policy", item.Name);
        Assert.Equal("A test policy", item.Description);
        Assert.Equal("windows10", item.Platform);
        Assert.NotNull(item.CreatedDateTime);
        Assert.NotNull(item.LastModifiedDateTime);
    }

    [Fact]
    public async Task ExportContentTypeAsync_UsesNameFallback_WhenNoDisplayName()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new { id = "item1", name = "Fallback Name" }
            }
        });

        var result = await _exporter.ExportContentTypeAsync(IntuneContentTypes.AssignmentFilter);

        Assert.Equal("Fallback Name", result[0].Name);
    }

    [Fact]
    public async Task ExportContentTypeAsync_ReportsProgress()
    {
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "1", displayName = "Policy1" } }
        });

        var reported = new List<string>();
        var progress = new SynchronousProgress<string>(msg => reported.Add(msg));

        await _exporter.ExportContentTypeAsync(IntuneContentTypes.AssignmentFilter, progress);

        Assert.NotEmpty(reported);
        Assert.Contains(reported, msg => msg.Contains("Policy1"));
    }

    /// <summary>
    /// Synchronous IProgress implementation that invokes the callback immediately
    /// on the calling thread, avoiding SynchronizationContext timing issues.
    /// </summary>
    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SynchronousProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}
