using IntuneMonitor.Models;
using IntuneMonitor.Reporting;

namespace IntuneMonitor.Tests;

public class HtmlAuditReportGeneratorTests
{
    private static AuditLogReport MakeAuditReport(bool withEvents = true) => new()
    {
        GeneratedAt = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc),
        TenantId = "test-tenant-id",
        DaysReviewed = 7,
        PeriodStart = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
        PeriodEnd = new DateTime(2026, 4, 9, 0, 0, 0, DateTimeKind.Utc),
        TotalEvents = withEvents ? 2 : 0,
        EventsByActivityType = withEvents
            ? new Dictionary<string, int> { ["Update"] = 1, ["Create"] = 1 }
            : new Dictionary<string, int>(),
        EventsByComponent = withEvents
            ? new Dictionary<string, int> { ["DeviceConfiguration"] = 2 }
            : new Dictionary<string, int>(),
        EventsByActor = withEvents
            ? new Dictionary<string, int> { ["admin@test.com"] = 2 }
            : new Dictionary<string, int>(),
        Events = withEvents
            ? new List<AuditEvent>
            {
                new()
                {
                    Id = "evt-1",
                    DisplayName = "Update Config",
                    ComponentName = "DeviceConfiguration",
                    Activity = "Update configuration",
                    ActivityType = "Update",
                    ActivityResult = "Success",
                    ActivityDateTime = new DateTime(2026, 4, 8, 10, 30, 0, DateTimeKind.Utc),
                    Actor = new AuditActor { UserPrincipalName = "admin@test.com" },
                    Resources = new List<AuditResource>
                    {
                        new() { DisplayName = "Test Policy", ResourceType = "Configuration" }
                    }
                },
                new()
                {
                    Id = "evt-2",
                    DisplayName = "Create Policy",
                    ComponentName = "DeviceConfiguration",
                    Activity = "Create policy",
                    ActivityType = "Create",
                    ActivityResult = "Failure",
                    ActivityDateTime = new DateTime(2026, 4, 8, 11, 0, 0, DateTimeKind.Utc),
                    Actor = new AuditActor { ApplicationDisplayName = "IntuneApp" },
                    Resources = new List<AuditResource>()
                }
            }
            : new List<AuditEvent>()
    };

    [Fact]
    public void Generate_ContainsHtmlStructure()
    {
        var html = HtmlAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
        Assert.Contains("Audit Log Report", html);
    }

    [Fact]
    public void Generate_ContainsSummaryCards()
    {
        var html = HtmlAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("Total Events", html);
        Assert.Contains("Activity Types", html);
        Assert.Contains("Components", html);
        Assert.Contains("Actors", html);
    }

    [Fact]
    public void Generate_ContainsPeriodInfo()
    {
        var html = HtmlAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("2026-04-02", html);
        Assert.Contains("2026-04-09", html);
        Assert.Contains("7 day(s)", html);
    }

    [Fact]
    public void Generate_ContainsBreakdownSections()
    {
        var html = HtmlAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("By Activity Type", html);
        Assert.Contains("By Component", html);
        Assert.Contains("By Actor", html);
    }

    [Fact]
    public void Generate_ContainsEventDetails()
    {
        var html = HtmlAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("Update configuration", html);
        Assert.Contains("admin@test.com", html);
        Assert.Contains("DeviceConfiguration", html);
        Assert.Contains("Test Policy", html);
    }

    [Fact]
    public void Generate_ContainsResultBadges()
    {
        var html = HtmlAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("Success", html);
        Assert.Contains("Failure", html);
    }

    [Fact]
    public void Generate_FallsBackToAppDisplayName()
    {
        var html = HtmlAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("IntuneApp", html);
    }

    [Fact]
    public void Generate_NoEvents_ShowsNoEventsMessage()
    {
        var html = HtmlAuditReportGenerator.Generate(MakeAuditReport(withEvents: false));

        Assert.Contains("No audit events found", html);
    }
}
