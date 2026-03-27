using IntuneMonitor.Commands;
using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

public class AuditLogCommandTests
{
    [Fact]
    public void BuildReport_EmptyEvents_ReturnsEmptyReport()
    {
        var events = new List<AuditEvent>();
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        var report = AuditLogCommand.BuildReport(events, 7, start, end);

        Assert.Equal(0, report.TotalEvents);
        Assert.Equal(7, report.DaysReviewed);
        Assert.Empty(report.EventsByActivityType);
        Assert.Empty(report.EventsByComponent);
        Assert.Empty(report.EventsByActor);
        Assert.Empty(report.Events);
    }

    [Fact]
    public void BuildReport_SingleEvent_CorrectAggregation()
    {
        var events = new List<AuditEvent>
        {
            MakeEvent("1", "Create", "DeviceConfiguration", "user@contoso.com", "Test Policy")
        };

        var report = AuditLogCommand.BuildReport(events, 1, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        Assert.Equal(1, report.TotalEvents);
        Assert.Single(report.EventsByActivityType);
        Assert.Equal(1, report.EventsByActivityType["Create"]);
        Assert.Single(report.EventsByComponent);
        Assert.Equal(1, report.EventsByComponent["DeviceConfiguration"]);
        Assert.Single(report.EventsByActor);
        Assert.Equal(1, report.EventsByActor["user@contoso.com"]);
    }

    [Fact]
    public void BuildReport_MultipleEvents_GroupsCorrectly()
    {
        var events = new List<AuditEvent>
        {
            MakeEvent("1", "Create", "DeviceConfiguration", "user1@contoso.com", "Policy A"),
            MakeEvent("2", "Update", "DeviceConfiguration", "user1@contoso.com", "Policy A"),
            MakeEvent("3", "Delete", "CompliancePolicy", "user2@contoso.com", "Policy B"),
            MakeEvent("4", "Create", "CompliancePolicy", "user2@contoso.com", "Policy C"),
            MakeEvent("5", "Create", "DeviceConfiguration", "user1@contoso.com", "Policy D"),
        };

        var report = AuditLogCommand.BuildReport(events, 30, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        Assert.Equal(5, report.TotalEvents);

        // Activity types: Create=3, Update=1, Delete=1
        Assert.Equal(3, report.EventsByActivityType.Count);
        Assert.Equal(3, report.EventsByActivityType["Create"]);
        Assert.Equal(1, report.EventsByActivityType["Update"]);
        Assert.Equal(1, report.EventsByActivityType["Delete"]);

        // Components: DeviceConfiguration=3, CompliancePolicy=2
        Assert.Equal(2, report.EventsByComponent.Count);
        Assert.Equal(3, report.EventsByComponent["DeviceConfiguration"]);
        Assert.Equal(2, report.EventsByComponent["CompliancePolicy"]);

        // Actors: user1=3, user2=2
        Assert.Equal(2, report.EventsByActor.Count);
        Assert.Equal(3, report.EventsByActor["user1@contoso.com"]);
        Assert.Equal(2, report.EventsByActor["user2@contoso.com"]);
    }

    [Fact]
    public void BuildReport_EventsWithNullActor_ExcludedFromActorCounts()
    {
        var events = new List<AuditEvent>
        {
            MakeEvent("1", "Create", "DeviceConfiguration", null, "Policy A"),
            MakeEvent("2", "Update", "DeviceConfiguration", "admin@contoso.com", "Policy B"),
        };

        var report = AuditLogCommand.BuildReport(events, 7, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        Assert.Equal(2, report.TotalEvents);
        Assert.Single(report.EventsByActor);
        Assert.Equal(1, report.EventsByActor["admin@contoso.com"]);
    }

    [Fact]
    public void BuildReport_EventWithAppActor_UsesAppDisplayName()
    {
        var events = new List<AuditEvent>
        {
            new()
            {
                Id = "1",
                ActivityType = "Create",
                ComponentName = "DeviceConfiguration",
                Activity = "Create Policy",
                ActivityResult = "Success",
                ActivityDateTime = DateTime.UtcNow,
                Actor = new AuditActor
                {
                    ApplicationDisplayName = "IntuneMonitor App",
                    UserPrincipalName = null
                },
                Resources = new List<AuditResource>()
            }
        };

        var report = AuditLogCommand.BuildReport(events, 7, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        Assert.Single(report.EventsByActor);
        Assert.True(report.EventsByActor.ContainsKey("IntuneMonitor App"));
    }

    [Fact]
    public void BuildReport_EmptyActivityTypeAndComponent_ExcludedFromGrouping()
    {
        var events = new List<AuditEvent>
        {
            new()
            {
                Id = "1",
                ActivityType = "",
                ComponentName = "",
                Activity = "Unknown",
                ActivityResult = "Success",
                ActivityDateTime = DateTime.UtcNow,
                Actor = null,
                Resources = new List<AuditResource>()
            }
        };

        var report = AuditLogCommand.BuildReport(events, 7, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        Assert.Equal(1, report.TotalEvents);
        Assert.Empty(report.EventsByActivityType);
        Assert.Empty(report.EventsByComponent);
        Assert.Empty(report.EventsByActor);
    }

    [Fact]
    public void BuildReport_PreservesAllEvents()
    {
        var events = new List<AuditEvent>
        {
            MakeEvent("1", "Create", "Comp1", "user@contoso.com", "Res1"),
            MakeEvent("2", "Update", "Comp2", "user@contoso.com", "Res2"),
            MakeEvent("3", "Delete", "Comp3", "admin@contoso.com", "Res3"),
        };

        var report = AuditLogCommand.BuildReport(events, 14, DateTime.UtcNow.AddDays(-14), DateTime.UtcNow);

        Assert.Equal(3, report.Events.Count);
        Assert.Equal("1", report.Events[0].Id);
        Assert.Equal("2", report.Events[1].Id);
        Assert.Equal("3", report.Events[2].Id);
    }

    private static AuditEvent MakeEvent(string id, string activityType, string component, string? actor, string resourceName) =>
        new()
        {
            Id = id,
            DisplayName = $"Event {id}",
            ComponentName = component,
            Activity = $"{activityType} {resourceName}",
            ActivityType = activityType,
            ActivityResult = "Success",
            ActivityDateTime = DateTime.UtcNow.AddHours(-int.Parse(id)),
            Actor = actor != null ? new AuditActor { UserPrincipalName = actor } : null,
            Resources = new List<AuditResource>
            {
                new() { DisplayName = resourceName, ResourceType = component }
            }
        };
}
