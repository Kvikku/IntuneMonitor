using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for computed properties on BackupModels records.
/// </summary>
public class BackupModelsTests
{
    // -----------------------------------------------------------------------
    // ChangeReport
    // -----------------------------------------------------------------------

    [Fact]
    public void ChangeReport_EmptyChanges_HasCorrectCounts()
    {
        var report = new ChangeReport();

        Assert.Equal(0, report.TotalCount);
        Assert.Equal(0, report.AddedCount);
        Assert.Equal(0, report.RemovedCount);
        Assert.Equal(0, report.ModifiedCount);
        Assert.False(report.HasChanges);
    }

    [Fact]
    public void ChangeReport_Defaults()
    {
        var report = new ChangeReport();

        Assert.Equal(string.Empty, report.TenantId);
        Assert.Equal(string.Empty, report.TenantName);
        Assert.NotNull(report.Changes);
        Assert.Empty(report.Changes);
    }

    // -----------------------------------------------------------------------
    // ExportReport
    // -----------------------------------------------------------------------

    [Fact]
    public void ExportReport_TotalItems_SumsContentSummaries()
    {
        var report = new ExportReport
        {
            ContentSummaries = new List<ExportContentSummary>
            {
                new() { ContentType = "A", ItemCount = 5 },
                new() { ContentType = "B", ItemCount = 10 },
                new() { ContentType = "C", ItemCount = 3 },
            }
        };

        Assert.Equal(18, report.TotalItems);
    }

    [Fact]
    public void ExportReport_ContentTypeCount_ReturnsCount()
    {
        var report = new ExportReport
        {
            ContentSummaries = new List<ExportContentSummary>
            {
                new() { ContentType = "A", ItemCount = 5 },
                new() { ContentType = "B", ItemCount = 10 },
            }
        };

        Assert.Equal(2, report.ContentTypeCount);
    }

    [Fact]
    public void ExportReport_EmptySummaries_ZeroCounts()
    {
        var report = new ExportReport();

        Assert.Equal(0, report.TotalItems);
        Assert.Equal(0, report.ContentTypeCount);
    }

    [Fact]
    public void ExportReport_Defaults()
    {
        var report = new ExportReport();

        Assert.Equal(string.Empty, report.TenantId);
        Assert.Equal(string.Empty, report.TenantName);
        Assert.Equal(string.Empty, report.StorageType);
        Assert.Equal(string.Empty, report.BackupPath);
        Assert.NotNull(report.ContentSummaries);
        Assert.Empty(report.ContentSummaries);
    }

    // -----------------------------------------------------------------------
    // ExportContentSummary
    // -----------------------------------------------------------------------

    [Fact]
    public void ExportContentSummary_Defaults()
    {
        var summary = new ExportContentSummary { ContentType = "Test" };

        Assert.Equal(0, summary.ItemCount);
        Assert.NotNull(summary.ItemNames);
        Assert.Empty(summary.ItemNames);
    }

    // -----------------------------------------------------------------------
    // IntuneItem
    // -----------------------------------------------------------------------

    [Fact]
    public void IntuneItem_Defaults()
    {
        var item = new IntuneItem();

        Assert.Null(item.Id);
        Assert.Null(item.Name);
        Assert.Null(item.Description);
        Assert.Null(item.Platform);
        Assert.Null(item.ContentType);
        Assert.Null(item.LastModifiedDateTime);
        Assert.Null(item.CreatedDateTime);
        Assert.Null(item.PolicyData);
    }

    // -----------------------------------------------------------------------
    // BackupDocument
    // -----------------------------------------------------------------------

    [Fact]
    public void BackupDocument_Defaults()
    {
        var doc = new BackupDocument();

        Assert.NotNull(doc.ExportedAt);
        Assert.Equal(string.Empty, doc.TenantId);
        Assert.Equal(string.Empty, doc.TenantName);
        Assert.Equal(string.Empty, doc.ContentType);
        Assert.NotNull(doc.Items);
        Assert.Empty(doc.Items);
    }

    // -----------------------------------------------------------------------
    // PolicyChange
    // -----------------------------------------------------------------------

    [Fact]
    public void PolicyChange_Defaults()
    {
        var change = new PolicyChange
        {
            ContentType = "Test",
            PolicyId = "1",
            PolicyName = "TestPolicy",
            ChangeType = ChangeType.Added
        };

        Assert.Equal(ChangeSeverity.Warning, change.Severity);
        Assert.NotNull(change.FieldChanges);
        Assert.Empty(change.FieldChanges);
        Assert.Null(change.Details);
    }
}
