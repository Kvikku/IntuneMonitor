using System.Text.Json;
using IntuneMonitor.Comparison;
using IntuneMonitor.Models;
using Xunit;

namespace IntuneMonitor.Tests;

public class PolicyComparerTests
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

    [Fact]
    public void Compare_NoChanges_ReturnsEmpty()
    {
        var json = """{"id":"1","displayName":"Test","enabled":true}""";
        var live = new List<IntuneItem> { MakeItem("1", "Test", json) };
        var backup = MakeBackup(MakeItem("1", "Test", json));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_AddedPolicy_DetectsAddition()
    {
        var json = """{"id":"1","displayName":"Test"}""";
        var live = new List<IntuneItem> { MakeItem("1", "Test", json) };
        var backup = MakeBackup(); // empty backup

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        var added = Assert.Single(changes, c => c.ChangeType == ChangeType.Added);
        Assert.Equal("1", added.PolicyId);
        Assert.Equal("Test", added.PolicyName);
        Assert.Equal(ChangeSeverity.Info, added.Severity);
    }

    [Fact]
    public void Compare_RemovedPolicy_DetectsRemoval()
    {
        var json = """{"id":"1","displayName":"OldPolicy"}""";
        var live = new List<IntuneItem>(); // policy gone from live
        var backup = MakeBackup(MakeItem("1", "OldPolicy", json));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        var removed = Assert.Single(changes, c => c.ChangeType == ChangeType.Removed);
        Assert.Equal("1", removed.PolicyId);
        Assert.Equal("OldPolicy", removed.PolicyName);
        Assert.Equal(ChangeSeverity.Critical, removed.Severity);
    }

    [Fact]
    public void Compare_ModifiedPolicy_DetectsChange()
    {
        var livePolicyJson = """{"id":"1","displayName":"Test","enabled":false}""";
        var backupPolicyJson = """{"id":"1","displayName":"Test","enabled":true}""";

        var live = new List<IntuneItem> { MakeItem("1", "Test", livePolicyJson) };
        var backup = MakeBackup(MakeItem("1", "Test", backupPolicyJson));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        var modified = Assert.Single(changes, c => c.ChangeType == ChangeType.Modified);
        Assert.Equal("1", modified.PolicyId);
        Assert.NotEmpty(modified.FieldChanges);

        var fieldChange = Assert.Single(modified.FieldChanges, f => f.FieldPath == "enabled");
        Assert.Equal("true", fieldChange.OldValue);
        Assert.Equal("false", fieldChange.NewValue);
    }

    [Fact]
    public void Compare_IgnoresLastModifiedDateTime()
    {
        // lastModifiedDateTime should not trigger a "modified" change
        var livePolicyJson = """{"id":"1","displayName":"Test","lastModifiedDateTime":"2024-01-02T00:00:00Z"}""";
        var backupPolicyJson = """{"id":"1","displayName":"Test","lastModifiedDateTime":"2024-01-01T00:00:00Z"}""";

        var live = new List<IntuneItem> { MakeItem("1", "Test", livePolicyJson) };
        var backup = MakeBackup(MakeItem("1", "Test", backupPolicyJson));

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, backup);

        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_NullBackup_TreatsAllAsAdded()
    {
        var json = """{"id":"1","displayName":"Test"}""";
        var live = new List<IntuneItem> { MakeItem("1", "Test", json) };

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, live, null);

        Assert.Single(changes, c => c.ChangeType == ChangeType.Added);
    }

    [Fact]
    public void Compare_MultipleChanges_AllDetected()
    {
        var liveItems = new List<IntuneItem>
        {
            MakeItem("1", "Existing", """{"id":"1","displayName":"Existing","setting":"new"}"""),
            MakeItem("3", "NewPolicy", """{"id":"3","displayName":"NewPolicy"}"""),
        };
        var backup = MakeBackup(
            MakeItem("1", "Existing", """{"id":"1","displayName":"Existing","setting":"old"}"""),
            MakeItem("2", "DeletedPolicy", """{"id":"2","displayName":"DeletedPolicy"}""")
        );

        var changes = _comparer.Compare(IntuneContentTypes.SettingsCatalog, liveItems, backup);

        Assert.Contains(changes, c => c.ChangeType == ChangeType.Added && c.PolicyId == "3");
        Assert.Contains(changes, c => c.ChangeType == ChangeType.Removed && c.PolicyId == "2");
        Assert.Contains(changes, c => c.ChangeType == ChangeType.Modified && c.PolicyId == "1");
    }

    [Fact]
    public void ChangeReport_CountProperties_AreCorrect()
    {
        var report = new ChangeReport
        {
            Changes = new List<PolicyChange>
            {
                new() { ContentType = "X", PolicyId = "1", PolicyName = "A", ChangeType = ChangeType.Added },
                new() { ContentType = "X", PolicyId = "2", PolicyName = "B", ChangeType = ChangeType.Removed },
                new() { ContentType = "X", PolicyId = "3", PolicyName = "C", ChangeType = ChangeType.Modified },
                new() { ContentType = "X", PolicyId = "4", PolicyName = "D", ChangeType = ChangeType.Modified },
            }
        };

        Assert.Equal(4, report.TotalCount);
        Assert.Equal(1, report.AddedCount);
        Assert.Equal(1, report.RemovedCount);
        Assert.Equal(2, report.ModifiedCount);
        Assert.True(report.HasChanges);
    }
}
