using System.Text.Json;
using IntuneMonitor.Comparison;
using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Advanced tests for PolicyComparer covering nested JSON diffing,
/// array changes, assignment diffing, and edge cases.
/// </summary>
public class PolicyComparerAdvancedTests
{
    private readonly PolicyComparer _comparer = new();

    private static IntuneItem MakeItem(string id, string name, string? jsonData = null) =>
        new()
        {
            Id = id,
            Name = name,
            ContentType = IntuneContentTypes.SettingsCatalog,
            PolicyData = jsonData != null
                ? JsonSerializer.Deserialize<JsonElement>(jsonData)
                : null
        };

    private static BackupDocument MakeBackup(params IntuneItem[] items) =>
        new()
        {
            ContentType = IntuneContentTypes.SettingsCatalog,
            Items = items.ToList()
        };

    // -----------------------------------------------------------------------
    // Nested JSON diffing
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_NestedObjectChange_DetectsDeepFieldPath()
    {
        var live = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","settings":{"firewall":{"enabled":false}}}""");
        var backup = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","settings":{"firewall":{"enabled":true}}}""");

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        Assert.Equal(ChangeType.Modified, modified.ChangeType);

        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("settings.firewall.enabled", field.FieldPath);
        Assert.Equal("true", field.OldValue);
        Assert.Equal("false", field.NewValue);
    }

    [Fact]
    public void Compare_MultipleNestedChanges_AllDetected()
    {
        var live = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","settings":{"a":"new1","b":"new2"}}""");
        var backup = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","settings":{"a":"old1","b":"old2"}}""");

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        Assert.Equal(2, modified.FieldChanges.Count);
        Assert.Contains(modified.FieldChanges, f => f.FieldPath == "settings.a");
        Assert.Contains(modified.FieldChanges, f => f.FieldPath == "settings.b");
    }

    [Fact]
    public void Compare_AddedNestedProperty_Detected()
    {
        var live = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","settings":{"existing":"val","newProp":"added"}}""");
        var backup = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","settings":{"existing":"val"}}""");

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("settings.newProp", field.FieldPath);
        Assert.Null(field.OldValue);
        Assert.Equal("added", field.NewValue);
    }

    [Fact]
    public void Compare_RemovedNestedProperty_Detected()
    {
        var live = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","settings":{"existing":"val"}}""");
        var backup = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","settings":{"existing":"val","oldProp":"removed"}}""");

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("settings.oldProp", field.FieldPath);
        Assert.Equal("removed", field.OldValue);
        Assert.Null(field.NewValue);
    }

    // -----------------------------------------------------------------------
    // Type changes
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_TypeChange_StringToNumber_Detected()
    {
        var live = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","value":42}""");
        var backup = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","value":"forty-two"}""");

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("value", field.FieldPath);
        Assert.Equal("forty-two", field.OldValue);
        Assert.Equal("42", field.NewValue);
    }

    [Fact]
    public void Compare_TypeChange_ScalarToObject_Detected()
    {
        var live = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","value":{"nested":true}}""");
        var backup = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","value":"simple"}""");

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("value", field.FieldPath);
    }

    // -----------------------------------------------------------------------
    // All ignored fields
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("version")]
    [InlineData("settingsCount")]
    [InlineData("isAssigned")]
    [InlineData("@odata.context")]
    [InlineData("lastModifiedDateTime")]
    public void Compare_IgnoresAutoFields(string fieldName)
    {
        var liveJson = $$"""{"id":"1","displayName":"Test","{{fieldName}}":"new-value"}""";
        var backupJson = $$"""{"id":"1","displayName":"Test","{{fieldName}}":"old-value"}""";

        var live = new[] { MakeItem("1", "Test", liveJson) };
        var backup = MakeBackup(MakeItem("1", "Test", backupJson));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        Assert.Empty(changes);
    }

    // -----------------------------------------------------------------------
    // Array diffing (non-assignment arrays)
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_ArrayChanged_DetectsWholeArrayDiff()
    {
        var live = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","tags":["a","b","c"]}""");
        var backup = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","tags":["a","b"]}""");

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("tags", field.FieldPath);
    }

    [Fact]
    public void Compare_ArrayUnchanged_NoChange()
    {
        var json = """{"id":"1","displayName":"Test","tags":["a","b"]}""";
        var live = new[] { MakeItem("1", "Test", json) };
        var backup = MakeBackup(MakeItem("1", "Test", json));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        Assert.Empty(changes);
    }

    // -----------------------------------------------------------------------
    // Assignment diffing
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_AssignmentAdded_DetectedAsFriendly()
    {
        var liveJson = """{"id":"1","displayName":"Test","assignments":[{"target":{"@odata.type":"#microsoft.graph.groupAssignmentTarget","groupId":"g1","groupDisplayName":"Sales"}}]}""";
        var backupJson = """{"id":"1","displayName":"Test","assignments":[]}""";

        var live = MakeItem("1", "Test", liveJson);
        var backup = MakeItem("1", "Test", backupJson);

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("assignments.added", field.FieldPath);
        Assert.Null(field.OldValue);
        Assert.Contains("Sales", field.NewValue);
    }

    [Fact]
    public void Compare_AssignmentRemoved_DetectedAsFriendly()
    {
        var liveJson = """{"id":"1","displayName":"Test","assignments":[]}""";
        var backupJson = """{"id":"1","displayName":"Test","assignments":[{"target":{"@odata.type":"#microsoft.graph.groupAssignmentTarget","groupId":"g1","groupDisplayName":"Marketing"}}]}""";

        var live = MakeItem("1", "Test", liveJson);
        var backup = MakeItem("1", "Test", backupJson);

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("assignments.removed", field.FieldPath);
        Assert.Contains("Marketing", field.OldValue);
        Assert.Null(field.NewValue);
    }

    [Fact]
    public void Compare_AssignmentModified_DetectedAsFriendly()
    {
        var liveJson = """{"id":"1","displayName":"Test","assignments":[{"target":{"@odata.type":"#microsoft.graph.groupAssignmentTarget","groupId":"g1","groupDisplayName":"Sales"},"intent":"required"}]}""";
        var backupJson = """{"id":"1","displayName":"Test","assignments":[{"target":{"@odata.type":"#microsoft.graph.groupAssignmentTarget","groupId":"g1","groupDisplayName":"Sales"},"intent":"available"}]}""";

        var live = MakeItem("1", "Test", liveJson);
        var backup = MakeItem("1", "Test", backupJson);

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("assignments.modified", field.FieldPath);
        Assert.NotNull(field.OldValue);
        Assert.NotNull(field.NewValue);
    }

    [Fact]
    public void Compare_Assignment_AllUsersVirtualGroup()
    {
        var liveJson = """{"id":"1","displayName":"Test","assignments":[{"target":{"@odata.type":"#microsoft.graph.allLicensedUsersAssignmentTarget"}}]}""";
        var backupJson = """{"id":"1","displayName":"Test","assignments":[]}""";

        var live = MakeItem("1", "Test", liveJson);
        var backup = MakeItem("1", "Test", backupJson);

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Contains("All Users", field.NewValue);
    }

    [Fact]
    public void Compare_Assignment_AllDevicesVirtualGroup()
    {
        var liveJson = """{"id":"1","displayName":"Test","assignments":[{"target":{"@odata.type":"#microsoft.graph.allDevicesAssignmentTarget"}}]}""";
        var backupJson = """{"id":"1","displayName":"Test","assignments":[]}""";

        var live = MakeItem("1", "Test", liveJson);
        var backup = MakeItem("1", "Test", backupJson);

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Contains("All Devices", field.NewValue);
    }

    [Fact]
    public void Compare_Assignment_ExclusionGroup()
    {
        var liveJson = """{"id":"1","displayName":"Test","assignments":[{"target":{"@odata.type":"#microsoft.graph.exclusionGroupAssignmentTarget","groupId":"g1","groupDisplayName":"VIPs"}}]}""";
        var backupJson = """{"id":"1","displayName":"Test","assignments":[]}""";

        var live = MakeItem("1", "Test", liveJson);
        var backup = MakeItem("1", "Test", backupJson);

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Contains("Exclude", field.NewValue);
        Assert.Contains("VIPs", field.NewValue);
    }

    [Fact]
    public void Compare_Assignment_FallsBackToId()
    {
        var liveJson = """{"id":"1","displayName":"Test","assignments":[{"id":"assign-1","target":{"@odata.type":"#microsoft.graph.groupAssignmentTarget","groupId":"g1"}}]}""";
        var backupJson = """{"id":"1","displayName":"Test","assignments":[]}""";

        var live = MakeItem("1", "Test", liveJson);
        var backup = MakeItem("1", "Test", backupJson);

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        // groupId is used as key, and shown in the description when no displayName
        Assert.Contains("g1", field.NewValue);
    }

    [Fact]
    public void Compare_Assignment_NoTarget_ShowsUnknown()
    {
        var liveJson = """{"id":"1","displayName":"Test","assignments":[{"id":"assign-1"}]}""";
        var backupJson = """{"id":"1","displayName":"Test","assignments":[]}""";

        var live = MakeItem("1", "Test", liveJson);
        var backup = MakeItem("1", "Test", backupJson);

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Contains("unknown", field.NewValue);
    }

    // -----------------------------------------------------------------------
    // Null / empty PolicyData edge cases
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_NullLivePolicyData_NullBackupData_NoFieldChanges()
    {
        // Both have null PolicyData — same content, no modification detected
        var live = new[] { MakeItem("1", "Test") };
        var backup = MakeBackup(MakeItem("1", "Test"));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_LiveHasData_BackupNull_DetectsModification()
    {
        var live = new[] { MakeItem("1", "Test", """{"id":"1","displayName":"Test","enabled":true}""") };
        var backup = MakeBackup(MakeItem("1", "Test"));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        var modified = Assert.Single(changes);
        Assert.Equal(ChangeType.Modified, modified.ChangeType);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("(root)", field.FieldPath);
        Assert.Null(field.OldValue);
    }

    [Fact]
    public void Compare_LiveNull_BackupHasData_DetectsModification()
    {
        var live = new[] { MakeItem("1", "Test") };
        var backup = MakeBackup(MakeItem("1", "Test", """{"id":"1","displayName":"Test","enabled":true}"""));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        var modified = Assert.Single(changes);
        Assert.Equal(ChangeType.Modified, modified.ChangeType);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("(root)", field.FieldPath);
        Assert.Null(field.NewValue);
    }

    // -----------------------------------------------------------------------
    // Case-insensitive ID matching
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_CaseInsensitiveIds_MatchesCorrectly()
    {
        var json = """{"id":"ABC-123","displayName":"Test","enabled":true}""";
        var live = new[] { MakeItem("ABC-123", "Test", json) };
        var backup = MakeBackup(MakeItem("abc-123", "Test", json));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        Assert.Empty(changes);
    }

    // -----------------------------------------------------------------------
    // Boolean value changes
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_BooleanChange_DetectedCorrectly()
    {
        var live = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","enabled":true}""");
        var backup = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","enabled":false}""");

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("enabled", field.FieldPath);
        Assert.Equal("false", field.OldValue);
        Assert.Equal("true", field.NewValue);
    }

    // -----------------------------------------------------------------------
    // Null value handling
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_NullToValue_DetectedAsChange()
    {
        var live = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","description":"hello"}""");
        var backup = MakeItem("1", "Test",
            """{"id":"1","displayName":"Test","description":null}""");

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog,
            new[] { live }, MakeBackup(backup));

        var modified = Assert.Single(changes);
        var field = Assert.Single(modified.FieldChanges);
        Assert.Equal("description", field.FieldPath);
    }
}
