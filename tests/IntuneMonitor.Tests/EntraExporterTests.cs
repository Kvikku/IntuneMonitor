using System.Net;
using System.Text.Json;
using IntuneMonitor.Graph;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for <see cref="EntraExporter"/>: fetches app registrations and enterprise apps
/// using separate $expand requests for owners and appRoleAssignments (Graph beta limitation),
/// and per-item calls for oauth2PermissionGrants.
/// </summary>
public class EntraExporterTests
{
    private readonly MockHttpHandler _handler = new();
    private readonly EntraExporter _exporter;

    public EntraExporterTests()
    {
        _exporter = new EntraExporter(GraphTestHelpers.FakeCredential, GraphTestHelpers.FakeGraphClientFactory);
        _exporter.HttpClientFactory = GraphTestHelpers.CreateClientFactory(_handler);
        _exporter.DelayFunc = (_, _) => Task.CompletedTask;
    }

    [Fact]
    public async Task ExportSnapshot_FetchesAppRegsWithOwners()
    {
        // App registrations list with owners inline via $expand
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new
            {
                id = "app-1",
                displayName = "My App",
                owners = new[] { new { id = "owner-1", userPrincipalName = "admin@contoso.com" } }
            } }
        });
        // Enterprise apps – owners request (empty)
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });
        // Enterprise apps – appRoleAssignments request (empty)
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var snapshot = await _exporter.ExportSnapshotAsync();

        Assert.Single(snapshot.AppRegistrations);
        Assert.Equal("My App", snapshot.AppRegistrations[0].DisplayName);

        var data = snapshot.AppRegistrations[0].Data!.Value;
        Assert.True(data.TryGetProperty("owners", out var owners));
        Assert.Equal(1, owners.GetArrayLength());

        // Should use $expand in the URL
        var url = _handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("$expand=owners", url);
    }

    [Fact]
    public async Task ExportSnapshot_FetchesEnterpriseAppsWithAllSubResources()
    {
        // App registrations (empty)
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });
        // Enterprise apps – owners request
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new
            {
                id = "sp-1",
                displayName = "My EA",
                owners = new[] { new { id = "owner-1" } }
            } }
        });
        // Enterprise apps – appRoleAssignments request
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new
            {
                id = "sp-1",
                displayName = "My EA",
                appRoleAssignments = new[] { new { id = "role-1", appRoleId = "role-guid" } }
            } }
        });
        // oauth2PermissionGrants (per-item call)
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "grant-1", scope = "User.Read" } }
        });

        var snapshot = await _exporter.ExportSnapshotAsync();

        Assert.Single(snapshot.EnterpriseApplications);
        var data = snapshot.EnterpriseApplications[0].Data!.Value;

        Assert.True(data.TryGetProperty("owners", out var owners));
        Assert.Equal(1, owners.GetArrayLength());

        Assert.True(data.TryGetProperty("appRoleAssignments", out var roles));
        Assert.Equal(1, roles.GetArrayLength());

        Assert.True(data.TryGetProperty("oauth2PermissionGrants", out var grants));
        Assert.Equal(1, grants.GetArrayLength());
    }

    [Fact]
    public async Task ExportSnapshot_EmptyTenant_ReturnsEmptyLists()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() }); // app regs
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() }); // EA owners
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() }); // EA roleAssignments

        var snapshot = await _exporter.ExportSnapshotAsync();

        Assert.Empty(snapshot.AppRegistrations);
        Assert.Empty(snapshot.EnterpriseApplications);
    }

    [Fact]
    public async Task ExportSnapshot_OAuth2FilterContainsServicePrincipalId()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() }); // app regs
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "sp-1", displayName = "EA", owners = Array.Empty<object>() } }
        }); // EA owners
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "sp-1", displayName = "EA", appRoleAssignments = Array.Empty<object>() } }
        }); // EA roleAssignments
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() }); // oauth2PermissionGrants

        await _exporter.ExportSnapshotAsync();

        // Request 4 (index 3) should be oauth2PermissionGrants filtered by clientId
        Assert.Equal(4, _handler.Requests.Count);
        var url = _handler.Requests[3].RequestUri!.ToString();
        Assert.Contains("oauth2PermissionGrants", url);
        Assert.Contains("sp-1", url);
    }

    [Fact]
    public async Task ExportSnapshot_UsesExpandQueryParameter()
    {
        // App registrations
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });
        // Enterprise apps – owners
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });
        // Enterprise apps – appRoleAssignments
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        await _exporter.ExportSnapshotAsync();

        // Verify app regs URL uses $expand=owners
        var appRegsUrl = _handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("applications", appRegsUrl);
        Assert.Contains("$expand=owners", appRegsUrl);

        // Verify enterprise apps uses two separate $expand requests (Graph beta limitation)
        var eaOwnersUrl = _handler.Requests[1].RequestUri!.ToString();
        Assert.Contains("servicePrincipals", eaOwnersUrl);
        Assert.Contains("$expand=owners", eaOwnersUrl);

        var eaRolesUrl = _handler.Requests[2].RequestUri!.ToString();
        Assert.Contains("servicePrincipals", eaRolesUrl);
        Assert.Contains("$expand=appRoleAssignments", eaRolesUrl);
    }

    [Fact]
    public async Task ExportSnapshot_Pagination_FollowsNextLink()
    {
        // App registrations page 1 with nextLink
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "value": [{"id":"app-1","displayName":"App 1","owners":[]}],
                "@odata.nextLink": "https://graph.microsoft.com/beta/applications?$skiptoken=page2"
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });
        // App registrations page 2
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[] { new { id = "app-2", displayName = "App 2", owners = Array.Empty<object>() } }
        });
        // Enterprise apps – owners (empty)
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });
        // Enterprise apps – appRoleAssignments (empty)
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() });

        var snapshot = await _exporter.ExportSnapshotAsync();

        Assert.Equal(2, snapshot.AppRegistrations.Count);
        Assert.Equal("App 1", snapshot.AppRegistrations[0].DisplayName);
        Assert.Equal("App 2", snapshot.AppRegistrations[1].DisplayName);
    }

    [Fact]
    public async Task ExportSnapshot_MultipleEnterpriseApps_PacesOAuth2Requests()
    {
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() }); // app regs
        // Two enterprise apps – owners request
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new { id = "sp-1", displayName = "EA 1", owners = Array.Empty<object>() },
                new { id = "sp-2", displayName = "EA 2", owners = Array.Empty<object>() }
            }
        });
        // Two enterprise apps – appRoleAssignments request
        _handler.Enqueue(HttpStatusCode.OK, new
        {
            value = new[]
            {
                new { id = "sp-1", displayName = "EA 1", appRoleAssignments = Array.Empty<object>() },
                new { id = "sp-2", displayName = "EA 2", appRoleAssignments = Array.Empty<object>() }
            }
        });
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() }); // oauth2 for sp-1
        _handler.Enqueue(HttpStatusCode.OK, new { value = Array.Empty<object>() }); // oauth2 for sp-2

        var delayCount = 0;
        _exporter.DelayFunc = (_, _) => { delayCount++; return Task.CompletedTask; };

        await _exporter.ExportSnapshotAsync();

        // Should have paced between the two oauth2 requests (1 delay between 2 items)
        Assert.Equal(1, delayCount);
        Assert.Equal(5, _handler.Requests.Count);
    }
}
