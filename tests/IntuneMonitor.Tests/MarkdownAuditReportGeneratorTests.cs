using IntuneMonitor.Models;
using IntuneMonitor.Reporting;

namespace IntuneMonitor.Tests;

public class MarkdownAuditReportGeneratorTests
{
    private static AuditLogReport MakeAuditReport(bool withEvents = true) => new()
    {
        GeneratedAt = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc),
        TenantId = "test-tenant-id",
        DaysReviewed = 7,
        PeriodStart = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
        PeriodEnd = new DateTime(2026, 4, 9, 0, 0, 0, DateTimeKind.Utc),
        TotalEvents = withEvents ? 1 : 0,
        EventsByActivityType = withEvents
            ? new Dictionary<string, int> { ["Update"] = 1 }
            : new Dictionary<string, int>(),
        EventsByComponent = withEvents
            ? new Dictionary<string, int> { ["DeviceConfiguration"] = 1 }
            : new Dictionary<string, int>(),
        EventsByActor = withEvents
            ? new Dictionary<string, int> { ["admin@test.com"] = 1 }
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
                }
            }
            : new List<AuditEvent>()
    };

    [Fact]
    public void Generate_ContainsMarkdownHeader()
    {
        var md = MarkdownAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("# Intune Audit Log Report", md);
    }

    [Fact]
    public void Generate_ContainsPeriodInfo()
    {
        var md = MarkdownAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("2026-04-02", md);
        Assert.Contains("2026-04-09", md);
        Assert.Contains("7 day(s)", md);
    }

    [Fact]
    public void Generate_ContainsSummaryTable()
    {
        var md = MarkdownAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("Total Events", md);
        Assert.Contains("Activity Types", md);
        Assert.Contains("Components", md);
        Assert.Contains("Actors", md);
    }

    [Fact]
    public void Generate_ContainsBreakdownSections()
    {
        var md = MarkdownAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("## By Activity Type", md);
        Assert.Contains("## By Component", md);
        Assert.Contains("## By Actor", md);
    }

    [Fact]
    public void Generate_ContainsEventTable()
    {
        var md = MarkdownAuditReportGenerator.Generate(MakeAuditReport());

        Assert.Contains("## Recent Events", md);
        Assert.Contains("Update configuration", md);
        Assert.Contains("admin@test.com", md);
        Assert.Contains("DeviceConfiguration", md);
    }

    [Fact]
    public void Generate_NoEvents_ShowsNoEventsMessage()
    {
        var md = MarkdownAuditReportGenerator.Generate(MakeAuditReport(withEvents: false));

        Assert.Contains("No audit events found", md);
    }
}
