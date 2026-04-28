using System.Text.Json;
using IntuneMonitor.Comparison;
using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for <see cref="EntraSnapshotComparer"/>: detects added/removed apps,
/// owner changes, and permission changes with detailed before/after diffs.
/// </summary>
public class EntraSnapshotComparerTests
{
    private static EntraAppItem MakeApp(string id, string name, string? ownersJson = null, string? extraJson = null)
    {
        var dict = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["displayName"] = name,
        };

        if (ownersJson != null)
            dict["owners"] = JsonSerializer.Deserialize<JsonElement>(ownersJson);

        if (extraJson != null)
        {
            var extra = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(extraJson);
            if (extra != null)
            {
                foreach (var kv in extra)
                    dict[kv.Key] = kv.Value;
            }
        }

        var json = JsonSerializer.Serialize(dict);
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        return new EntraAppItem
        {
            Id = id,
            DisplayName = name,
            Data = data
        };
    }

    // -----------------------------------------------------------------------
    // No previous snapshot
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_NoPrevious_ReturnsEmpty()
    {
        var current = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A") },
            EnterpriseApplications = new() { MakeApp("2", "EA B") }
        };

        var changes = EntraSnapshotComparer.Compare(current, null);

        Assert.Empty(changes);
    }

    // -----------------------------------------------------------------------
    // Added / Removed
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_NewAppRegistration_DetectsAdded()
    {
        var previous = new EntraSnapshot { AppRegistrations = new() };
        var current = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "New App") }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        var change = Assert.Single(changes);
        Assert.Equal("AppRegistration", change.Category);
        Assert.Equal("Added", change.ChangeType);
        Assert.Equal("New App", change.AppName);
    }

    [Fact]
    public void Compare_RemovedEnterpriseApp_DetectsRemoved()
    {
        var previous = new EntraSnapshot
        {
            EnterpriseApplications = new() { MakeApp("1", "Old EA") }
        };
        var current = new EntraSnapshot { EnterpriseApplications = new() };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        var change = Assert.Single(changes);
        Assert.Equal("EnterpriseApplication", change.Category);
        Assert.Equal("Removed", change.ChangeType);
        Assert.Equal("Old EA", change.AppName);
    }

    // -----------------------------------------------------------------------
    // Owner changes — shows UPN or displayName when available
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_OwnerAdded_ShowsUPN()
    {
        var previous = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", """[{"id":"owner1","userPrincipalName":"alice@contoso.com"}]""") }
        };
        var current = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", """[{"id":"owner1","userPrincipalName":"alice@contoso.com"},{"id":"owner2","userPrincipalName":"bob@contoso.com"}]""") }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        var change = Assert.Single(changes);
        Assert.Equal("OwnerAdded", change.ChangeType);
        Assert.Contains("bob@contoso.com", change.Details!);
    }

    [Fact]
    public void Compare_OwnerAdded_FallsBackToId()
    {
        var previous = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", """[{"id":"owner1"}]""") }
        };
        var current = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", """[{"id":"owner1"},{"id":"owner2"}]""") }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        var change = Assert.Single(changes);
        Assert.Equal("OwnerAdded", change.ChangeType);
        Assert.Contains("owner2", change.Details!);
    }

    [Fact]
    public void Compare_OwnerRemoved_DetectsOwnerRemoved()
    {
        var previous = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", """[{"id":"owner1"},{"id":"owner2"}]""") }
        };
        var current = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", """[{"id":"owner1"}]""") }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        var change = Assert.Single(changes);
        Assert.Equal("OwnerRemoved", change.ChangeType);
        Assert.Contains("owner2", change.Details!);
    }

    [Fact]
    public void Compare_NoOwnerChange_ReturnsEmpty()
    {
        var owners = """[{"id":"owner1"}]""";
        var previous = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", owners) }
        };
        var current = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", owners) }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        Assert.Empty(changes);
    }

    // -----------------------------------------------------------------------
    // Permission changes — detailed diffs
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_AppRoleAssignmentAdded_ShowsAddedDetail()
    {
        var prevExtra = """{"appRoleAssignments":[{"id":"role1","appRoleId":"aaa"}]}""";
        var currExtra = """{"appRoleAssignments":[{"id":"role1","appRoleId":"aaa"},{"id":"role2","appRoleId":"bbb"}]}""";

        var previous = new EntraSnapshot
        {
            EnterpriseApplications = new() { MakeApp("1", "EA A", null, prevExtra) }
        };
        var current = new EntraSnapshot
        {
            EnterpriseApplications = new() { MakeApp("1", "EA A", null, currExtra) }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        var change = Assert.Single(changes);
        Assert.Equal("PermissionChanged", change.ChangeType);
        Assert.Contains("App role assignments", change.Details!);
        Assert.Contains("Added", change.Details!);
        Assert.Contains("bbb", change.Details!);
    }

    [Fact]
    public void Compare_OAuth2ScopeAdded_ShowsScopeDetail()
    {
        var prevExtra = """{"oauth2PermissionGrants":[{"id":"g1","scope":"User.Read"}]}""";
        var currExtra = """{"oauth2PermissionGrants":[{"id":"g1","scope":"User.Read Mail.Read"}]}""";

        var previous = new EntraSnapshot
        {
            EnterpriseApplications = new() { MakeApp("1", "EA A", null, prevExtra) }
        };
        var current = new EntraSnapshot
        {
            EnterpriseApplications = new() { MakeApp("1", "EA A", null, currExtra) }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        var change = Assert.Single(changes);
        Assert.Equal("PermissionChanged", change.ChangeType);
        Assert.Contains("Scopes added", change.Details!);
        Assert.Contains("Mail.Read", change.Details!);
    }

    [Fact]
    public void Compare_OAuth2ScopeRemoved_ShowsScopeDetail()
    {
        var prevExtra = """{"oauth2PermissionGrants":[{"id":"g1","scope":"User.Read Mail.Read"}]}""";
        var currExtra = """{"oauth2PermissionGrants":[{"id":"g1","scope":"User.Read"}]}""";

        var previous = new EntraSnapshot
        {
            EnterpriseApplications = new() { MakeApp("1", "EA A", null, prevExtra) }
        };
        var current = new EntraSnapshot
        {
            EnterpriseApplications = new() { MakeApp("1", "EA A", null, currExtra) }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        var change = Assert.Single(changes);
        Assert.Contains("Scopes removed", change.Details!);
        Assert.Contains("Mail.Read", change.Details!);
    }

    [Fact]
    public void Compare_RequiredResourceAccessAdded_ShowsDetail()
    {
        var prevExtra = """{"requiredResourceAccess":[{"resourceAppId":"00000003-0000-0000-c000-000000000000","resourceAccess":[{"id":"e1","type":"Scope"}]}]}""";
        var currExtra = """{"requiredResourceAccess":[{"resourceAppId":"00000003-0000-0000-c000-000000000000","resourceAccess":[{"id":"e1","type":"Scope"},{"id":"e2","type":"Role"}]}]}""";

        var previous = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", null, prevExtra) }
        };
        var current = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", null, currExtra) }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        var change = Assert.Single(changes);
        Assert.Equal("PermissionChanged", change.ChangeType);
        Assert.Contains("Required resource access", change.Details!);
        Assert.Contains("Added", change.Details!);
        Assert.Contains("e2", change.Details!);
        Assert.Contains("Role", change.Details!);
    }

    [Fact]
    public void Compare_NoPermissionChange_ReturnsEmpty()
    {
        var extra = """{"requiredResourceAccess":[{"resourceAppId":"res1","resourceAccess":[{"id":"e1","type":"Scope"}]}]}""";

        var previous = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", null, extra) }
        };
        var current = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", null, extra) }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_JsonPropertyOrderDoesNotCauseFalsePositive()
    {
        // Same permissions, different JSON property order
        var prevExtra = """{"requiredResourceAccess":[{"resourceAppId":"res1","resourceAccess":[{"id":"e1","type":"Scope"}]}]}""";
        var currExtra = """{"requiredResourceAccess":[{"resourceAccess":[{"type":"Scope","id":"e1"}],"resourceAppId":"res1"}]}""";

        var previous = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", null, prevExtra) }
        };
        var current = new EntraSnapshot
        {
            AppRegistrations = new() { MakeApp("1", "App A", null, currExtra) }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        Assert.Empty(changes);
    }

    // -----------------------------------------------------------------------
    // Multiple changes
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_MultipleChanges_ReturnsAll()
    {
        var previous = new EntraSnapshot
        {
            AppRegistrations = new()
            {
                MakeApp("1", "App A", """[{"id":"owner1"}]"""),
                MakeApp("2", "App B")
            },
            EnterpriseApplications = new()
            {
                MakeApp("3", "EA C")
            }
        };
        var current = new EntraSnapshot
        {
            AppRegistrations = new()
            {
                MakeApp("1", "App A", """[{"id":"owner1"},{"id":"owner2"}]"""),
                // App B removed
                MakeApp("4", "App D") // new
            },
            EnterpriseApplications = new()
            {
                MakeApp("3", "EA C")
            }
        };

        var changes = EntraSnapshotComparer.Compare(current, previous);

        Assert.Equal(3, changes.Count);
        Assert.Contains(changes, c => c.ChangeType == "Added" && c.AppName == "App D");
        Assert.Contains(changes, c => c.ChangeType == "Removed" && c.AppName == "App B");
        Assert.Contains(changes, c => c.ChangeType == "OwnerAdded" && c.AppName == "App A");
    }
}
