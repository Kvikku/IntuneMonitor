using System.Net;
using System.Text.Json;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Integration tests for <see cref="IntuneImporter"/> using a mock HttpMessageHandler.
/// Covers import success, error responses, payload preparation, and validation.
/// </summary>
public class IntuneImporterTests
{
    private readonly MockHttpHandler _handler = new();
    private readonly IntuneImporter _importer;

    public IntuneImporterTests()
    {
        _importer = new IntuneImporter(GraphTestHelpers.FakeCredential);
        _importer.HttpClientFactory = GraphTestHelpers.CreateClientFactory(_handler);
    }

    private static IntuneItem MakeItem(string name, string contentType, string json) =>
        new()
        {
            Id = "test-id",
            Name = name,
            ContentType = contentType,
            PolicyData = JsonSerializer.Deserialize<JsonElement>(json)
        };

    // -----------------------------------------------------------------------
    // Import success
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ImportItemAsync_Success_ReturnsItemName()
    {
        _handler.Enqueue(HttpStatusCode.Created, new { id = "new-id", displayName = "MyPolicy" });

        var item = MakeItem("MyPolicy", IntuneContentTypes.DeviceCompliancePolicy,
            """{"displayName":"MyPolicy","enabled":true}""");

        var result = await _importer.ImportItemAsync(item);

        Assert.Equal("MyPolicy", result);
        Assert.Single(_handler.Requests);

        // Verify the request was a POST to the correct endpoint
        var request = _handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("deviceCompliancePolicies", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task ImportItemAsync_SendsCorrectPayload()
    {
        _handler.Enqueue(HttpStatusCode.Created, new { id = "new-id" });

        var item = MakeItem("TestPolicy", IntuneContentTypes.DeviceConfigurationPolicy,
            """{"displayName":"TestPolicy","description":"Test","enabled":true}""");

        await _importer.ImportItemAsync(item);

        var request = _handler.Requests[0];
        var body = await request.Content!.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal("TestPolicy", payload.GetProperty("displayName").GetString());
        Assert.Equal("Test", payload.GetProperty("description").GetString());
        Assert.True(payload.GetProperty("enabled").GetBoolean());
    }

    // -----------------------------------------------------------------------
    // Read-only field stripping
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ImportItemAsync_StripsReadOnlyFields()
    {
        _handler.Enqueue(HttpStatusCode.Created, new { id = "new-id" });

        var item = MakeItem("TestPolicy", IntuneContentTypes.DeviceCompliancePolicy,
            """
            {
                "id": "old-id",
                "displayName": "TestPolicy",
                "createdDateTime": "2024-01-01T00:00:00Z",
                "lastModifiedDateTime": "2024-06-01T00:00:00Z",
                "version": 5,
                "@odata.context": "https://graph.microsoft.com/...",
                "@odata.type": "#microsoft.graph.whatever",
                "roleScopeTagIds": ["0"],
                "settingsCount": 10,
                "isAssigned": true,
                "enabled": true
            }
            """);

        await _importer.ImportItemAsync(item);

        var body = await _handler.Requests[0].Content!.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(body);

        // Read-only fields should be stripped
        Assert.False(payload.TryGetProperty("id", out _));
        Assert.False(payload.TryGetProperty("createdDateTime", out _));
        Assert.False(payload.TryGetProperty("lastModifiedDateTime", out _));
        Assert.False(payload.TryGetProperty("version", out _));
        Assert.False(payload.TryGetProperty("@odata.context", out _));
        Assert.False(payload.TryGetProperty("@odata.type", out _));
        Assert.False(payload.TryGetProperty("roleScopeTagIds", out _));
        Assert.False(payload.TryGetProperty("settingsCount", out _));
        Assert.False(payload.TryGetProperty("isAssigned", out _));

        // Writable fields should be preserved
        Assert.True(payload.TryGetProperty("displayName", out _));
        Assert.True(payload.TryGetProperty("enabled", out _));
    }

    [Fact]
    public async Task ImportItemAsync_PreservesNestedObjects()
    {
        _handler.Enqueue(HttpStatusCode.Created, new { id = "new-id" });

        var item = MakeItem("TestPolicy", IntuneContentTypes.DeviceCompliancePolicy,
            """
            {
                "displayName": "TestPolicy",
                "scheduledActionsForRule": [
                    {
                        "ruleName": "rule1",
                        "scheduledActionConfigurations": [
                            {"gracePeriodHours": 12, "actionType": "block"}
                        ]
                    }
                ]
            }
            """);

        await _importer.ImportItemAsync(item);

        var body = await _handler.Requests[0].Content!.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.True(payload.TryGetProperty("scheduledActionsForRule", out var rules));
        Assert.Equal(JsonValueKind.Array, rules.ValueKind);
        Assert.Equal(1, rules.GetArrayLength());
    }

    // -----------------------------------------------------------------------
    // Validation and error handling
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ImportItemAsync_NullPolicyData_ThrowsArgumentException()
    {
        var item = new IntuneItem
        {
            Name = "TestPolicy",
            ContentType = IntuneContentTypes.DeviceCompliancePolicy,
            PolicyData = null
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _importer.ImportItemAsync(item));
        Assert.Contains("PolicyData", ex.Message);
    }

    [Fact]
    public async Task ImportItemAsync_UnsupportedContentType_ThrowsArgumentException()
    {
        var item = MakeItem("TestPolicy", "UnsupportedType", """{"displayName":"Test"}""");

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _importer.ImportItemAsync(item));
        Assert.Contains("Unsupported content type", ex.Message);
    }

    [Fact]
    public async Task ImportItemAsync_NullContentType_ThrowsArgumentException()
    {
        var item = new IntuneItem
        {
            Name = "TestPolicy",
            ContentType = null,
            PolicyData = JsonSerializer.Deserialize<JsonElement>("""{"displayName":"Test"}""")
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _importer.ImportItemAsync(item));
        Assert.Contains("Unsupported content type", ex.Message);
    }

    [Fact]
    public async Task ImportItemAsync_HttpError_ThrowsInvalidOperationException()
    {
        _handler.EnqueueError(HttpStatusCode.BadRequest, "Invalid payload");

        var item = MakeItem("TestPolicy", IntuneContentTypes.DeviceCompliancePolicy,
            """{"displayName":"TestPolicy"}""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _importer.ImportItemAsync(item));
        Assert.Contains("TestPolicy", ex.Message);
        Assert.Contains("BadRequest", ex.Message);
    }

    [Fact]
    public async Task ImportItemAsync_Forbidden_ThrowsInvalidOperationException()
    {
        _handler.EnqueueError(HttpStatusCode.Forbidden, "Insufficient privileges");

        var item = MakeItem("TestPolicy", IntuneContentTypes.DeviceCompliancePolicy,
            """{"displayName":"TestPolicy"}""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _importer.ImportItemAsync(item));
        Assert.Contains("Forbidden", ex.Message);
    }

    [Fact]
    public async Task ImportItemAsync_Conflict_ThrowsInvalidOperationException()
    {
        _handler.EnqueueError(HttpStatusCode.Conflict, "Policy already exists");

        var item = MakeItem("TestPolicy", IntuneContentTypes.DeviceCompliancePolicy,
            """{"displayName":"TestPolicy"}""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _importer.ImportItemAsync(item));
        Assert.Contains("Conflict", ex.Message);
    }

    [Fact]
    public async Task ImportItemAsync_ServerError_ThrowsInvalidOperationException()
    {
        _handler.EnqueueError(HttpStatusCode.InternalServerError, "Server error");

        var item = MakeItem("TestPolicy", IntuneContentTypes.DeviceCompliancePolicy,
            """{"displayName":"TestPolicy"}""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _importer.ImportItemAsync(item));
        Assert.Contains("InternalServerError", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Constructor validation
    // -----------------------------------------------------------------------

    [Fact]
    public void IntuneImporter_NullCredential_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new IntuneImporter(null!));
    }

    // -----------------------------------------------------------------------
    // Correct endpoint mapping
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(IntuneContentTypes.SettingsCatalog, "configurationPolicies")]
    [InlineData(IntuneContentTypes.DeviceCompliancePolicy, "deviceCompliancePolicies")]
    [InlineData(IntuneContentTypes.DeviceConfigurationPolicy, "deviceConfigurations")]
    [InlineData(IntuneContentTypes.ConditionalAccessPolicy, "conditionalAccess/policies")]
    public async Task ImportItemAsync_UsesCorrectEndpoint(string contentType, string expectedPathPart)
    {
        _handler.Enqueue(HttpStatusCode.Created, new { id = "new-id" });

        var item = MakeItem("TestPolicy", contentType, """{"displayName":"TestPolicy"}""");

        await _importer.ImportItemAsync(item);

        Assert.Contains(expectedPathPart, _handler.Requests[0].RequestUri!.ToString());
    }
}
