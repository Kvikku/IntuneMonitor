using IntuneMonitor.Models;
using IntuneMonitor.Reporting;

namespace IntuneMonitor.Tests;

public class HtmlReportGeneratorTests
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
                    Severity = ChangeSeverity.Info,
                    Details = "Policy 'New Policy' was created."
                },
                new()
                {
                    ContentType = "DeviceCompliancePolicy",
                    PolicyId = "ghi-789",
                    PolicyName = "Deleted Policy",
                    ChangeType = ChangeType.Removed,
                    Severity = ChangeSeverity.Critical,
                    Details = "Policy 'Deleted Policy' was deleted."
                }
            }
            : new List<PolicyChange>()
    };

    [Fact]
    public void Generate_ContainsHtmlStructure()
    {
        var html = HtmlReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
        Assert.Contains("Intune Monitor", html);
    }

    [Fact]
    public void Generate_ContainsSummaryCards()
    {
        var html = HtmlReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("Total Changes", html);
        Assert.Contains("Added", html);
        Assert.Contains("Modified", html);
        Assert.Contains("Removed", html);
    }

    [Fact]
    public void Generate_ContainsTenantName()
    {
        var html = HtmlReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("Test Tenant", html);
    }

    [Fact]
    public void Generate_ContainsContentTypeGrouping()
    {
        var html = HtmlReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("SettingsCatalog", html);
        Assert.Contains("DeviceCompliancePolicy", html);
    }

    [Fact]
    public void Generate_ContainsPolicyDetails()
    {
        var html = HtmlReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("Firewall Policy", html);
        Assert.Contains("abc-123", html);
        Assert.Contains("New Policy", html);
        Assert.Contains("Deleted Policy", html);
    }

    [Fact]
    public void Generate_ContainsFieldChanges()
    {
        var html = HtmlReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("enabled", html);
        Assert.Contains("1 field change(s)", html);
    }

    [Fact]
    public void Generate_ContainsSeverityBadges()
    {
        var html = HtmlReportGenerator.Generate(MakeChangeReport());

        Assert.Contains("warning", html.ToLowerInvariant());
        Assert.Contains("critical", html.ToLowerInvariant());
        Assert.Contains("info", html.ToLowerInvariant());
    }

    [Fact]
    public void Generate_NoChanges_ShowsNoChangesMessage()
    {
        var html = HtmlReportGenerator.Generate(MakeChangeReport(withChanges: false));

        Assert.Contains("No changes detected", html);
    }

    [Fact]
    public void Generate_EscapesHtmlInPolicyName()
    {
        var report = new ChangeReport
        {
            Changes = new List<PolicyChange>
            {
                new()
                {
                    ContentType = "SettingsCatalog",
                    PolicyId = "1",
                    PolicyName = "<script>alert('xss')</script>",
                    ChangeType = ChangeType.Added,
                    Severity = ChangeSeverity.Info
                }
            }
        };

        var html = HtmlReportGenerator.Generate(report);

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
