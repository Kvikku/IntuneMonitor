using IntuneMonitor.Models;
using IntuneMonitor.Reporting;

namespace IntuneMonitor.Tests;

public class MarkdownReportGeneratorTests
{
    private static ChangeReport MakeChangeReport(bool withChanges = true) => new()
    {
        GeneratedAt = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc),
        TenantId = "test-tenant-id",
        TenantName = "Test Tenant",
        Changes = withChanges
            ? new List<PolicyChange>
            {
                new()
                {
                    ContentType = "SettingsCatalog",
                    PolicyId = "abc-123",
                    PolicyName = "Firewall Policy",
                    ChangeType = ChangeType.Modified,
                    Severity = ChangeSeverity.Warning,
                    Details = "Policy 'Firewall Policy' was modified (1 field(s) changed).",
                    FieldChanges = new List<FieldChange>
                    {
                        new() { FieldPath = "enabled", OldValue = "true", NewValue = "false" }
                    }
                },
                new()
                {
                    ContentType = "SettingsCatalog",
                    PolicyId = "def-456",
                    PolicyName = "New Policy",
                    ChangeType = ChangeType.Added,
                    Severity = ChangeSeverity.Info
                },
                new()
                {
                    ContentType = "DeviceCompliancePolicy",
                    PolicyId = "ghi-789",
                    PolicyName = "Deleted Policy",
                    ChangeType = ChangeType.Removed,
                    Severity = ChangeSeverity.Critical
                }
            }
            : new List<PolicyChange>()
    };

    [Fact]
    public void Generate_ContainsMarkdownHeader()
    {
        var md = MarkdownReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("# Intune Monitor", md);
        Assert.Contains("Test Tenant", md);
    }

    [Fact]
    public void Generate_ContainsSummaryTable()
    {
        var md = MarkdownReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("Total Changes", md);
        Assert.Contains("Added", md);
        Assert.Contains("Modified", md);
        Assert.Contains("Removed", md);
    }

    [Fact]
    public void Generate_ContainsContentTypeHeadings()
    {
        var md = MarkdownReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("## SettingsCatalog", md);
        Assert.Contains("## DeviceCompliancePolicy", md);
    }

    [Fact]
    public void Generate_ContainsChangeIcons()
    {
        var md = MarkdownReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("🟢 Added", md);
        Assert.Contains("🟡 Modified", md);
        Assert.Contains("🔴 Removed", md);
    }

    [Fact]
    public void Generate_ContainsFieldChangeDetails()
    {
        var md = MarkdownReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("enabled", md);
        Assert.Contains("1 field change(s)", md);
    }

    [Fact]
    public void Generate_NoChanges_ShowsNoChangesMessage()
    {
        var md = MarkdownReportGenerator.Generate(MakeChangeReport(withChanges: false));

        Assert.Contains("No changes detected", md);
    }

    [Fact]
    public void Generate_EscapesPipeInPolicyName()
    {
        var report = new ChangeReport
        {
            Changes = new List<PolicyChange>
            {
                new()
                {
                    ContentType = "SettingsCatalog",
                    PolicyId = "1",
                    PolicyName = "Policy | Special",
                    ChangeType = ChangeType.Added,
                    Severity = ChangeSeverity.Info
                }
            }
        };

        var md = MarkdownReportGenerator.Generate(report);

        Assert.Contains("Policy \\| Special", md);
    }
}
