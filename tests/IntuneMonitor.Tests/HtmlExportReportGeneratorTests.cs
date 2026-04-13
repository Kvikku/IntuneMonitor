using IntuneMonitor.Models;
using IntuneMonitor.Reporting;

namespace IntuneMonitor.Tests;

public class HtmlExportReportGeneratorTests
{
    private static ExportReport MakeExportReport(bool withItems = true) => new()
    {
        GeneratedAt = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc),
        TenantId = "test-tenant-id",
        TenantName = "Test Tenant",
        StorageType = "LocalFile",
        BackupPath = "/backups/2026-04-09",
        ContentSummaries = withItems
            ? new List<ExportContentSummary>
            {
                new()
                {
                    ContentType = "SettingsCatalog",
                    ItemCount = 2,
                    ItemNames = new List<string> { "Policy A", "Policy B" }
                },
                new()
                {
                    ContentType = "DeviceCompliancePolicy",
                    ItemCount = 1,
                    ItemNames = new List<string> { "Compliance Rule" }
                }
            }
            : new List<ExportContentSummary>()
    };

    [Fact]
    public void Generate_ContainsHtmlStructure()
    {
        var html = HtmlExportReportGenerator.Generate(MakeExportReport());

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
        Assert.Contains("Export Summary", html);
    }

    [Fact]
    public void Generate_ContainsSummaryCards()
    {
        var html = HtmlExportReportGenerator.Generate(MakeExportReport());

        Assert.Contains("Total Items", html);
        Assert.Contains("Content Types", html);
    }

    [Fact]
    public void Generate_ContainsStorageInfo()
    {
        var html = HtmlExportReportGenerator.Generate(MakeExportReport());

        Assert.Contains("LocalFile", html);
        Assert.Contains("/backups/2026-04-09", html);
    }

    [Fact]
    public void Generate_ContainsContentTypeSections()
    {
        var html = HtmlExportReportGenerator.Generate(MakeExportReport());

        Assert.Contains("SettingsCatalog", html);
        Assert.Contains("DeviceCompliancePolicy", html);
    }

    [Fact]
    public void Generate_ContainsItemNames()
    {
        var html = HtmlExportReportGenerator.Generate(MakeExportReport());

        Assert.Contains("Policy A", html);
        Assert.Contains("Policy B", html);
        Assert.Contains("Compliance Rule", html);
    }

    [Fact]
    public void Generate_NoItems_ShowsNoItemsMessage()
    {
        var html = HtmlExportReportGenerator.Generate(MakeExportReport(withItems: false));

        Assert.Contains("No items exported", html);
    }
}
