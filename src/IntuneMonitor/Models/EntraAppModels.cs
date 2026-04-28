using System.Text.Json;

namespace IntuneMonitor.Models;

/// <summary>
/// Represents a single Entra app registration or enterprise application with its full data.
/// </summary>
public record EntraAppItem
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AppId { get; init; }
    public DateTime? CreatedDateTime { get; init; }

    /// <summary>Full raw JSON data from Microsoft Graph, including merged sub-resources.</summary>
    public JsonElement? Data { get; init; }
}

/// <summary>
/// A point-in-time snapshot of all app registrations and enterprise applications.
/// </summary>
public record EntraSnapshot
{
    public string? SnapshotPath { get; init; }
    public List<EntraAppItem> AppRegistrations { get; init; } = new();
    public List<EntraAppItem> EnterpriseApplications { get; init; } = new();
}

/// <summary>
/// Represents a directory audit event from the Microsoft Graph auditLogs/directoryAudits endpoint.
/// Used for tracking Entra ID changes such as app registration and enterprise application modifications.
/// </summary>
public record DirectoryAuditEvent
{
    public string Id { get; init; } = string.Empty;
    public string ActivityDisplayName { get; init; } = string.Empty;
    public string ActivityDateTime { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DirectoryAuditActor? InitiatedBy { get; init; }
    public List<DirectoryAuditTarget> TargetResources { get; init; } = new();
}

/// <summary>
/// The actor that initiated a directory audit event (user or app).
/// </summary>
public record DirectoryAuditActor
{
    public string? UserPrincipalName { get; init; }
    public string? UserDisplayName { get; init; }
    public string? UserId { get; init; }
    public string? AppDisplayName { get; init; }
    public string? AppId { get; init; }
    public string? IpAddress { get; init; }
}

/// <summary>
/// A resource affected by a directory audit event.
/// </summary>
public record DirectoryAuditTarget
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public List<DirectoryAuditModifiedProperty> ModifiedProperties { get; init; } = new();
}

/// <summary>
/// A property that was modified on a target resource during a directory audit event.
/// </summary>
public record DirectoryAuditModifiedProperty
{
    public string DisplayName { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

/// <summary>
/// Report summarizing directory audit events for Entra app registrations and enterprise applications.
/// </summary>
public record EntraAuditReport
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public string TenantId { get; init; } = string.Empty;
    public int DaysReviewed { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public int TotalEvents { get; init; }
    public List<DirectoryAuditEvent> Events { get; init; } = new();

    /// <summary>Event counts grouped by activity display name.</summary>
    public Dictionary<string, int> EventsByActivity { get; init; } = new();

    /// <summary>Event counts grouped by initiating actor.</summary>
    public Dictionary<string, int> EventsByActor { get; init; } = new();

    /// <summary>Event counts grouped by target application display name.</summary>
    public Dictionary<string, int> EventsByTargetApp { get; init; } = new();

    public bool HasEvents => TotalEvents > 0;
}

/// <summary>
/// Describes a single change detected between two Entra snapshots.
/// </summary>
public record EntraChange
{
    public required string Category { get; init; }
    public required string AppId { get; init; }
    public required string AppName { get; init; }
    public required string ChangeType { get; init; }
    public string? Details { get; init; }
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Combined nightly report: snapshot diff + directory audit events.
/// </summary>
public record EntraMonitorReport
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Changes detected by comparing the current snapshot to the previous one.</summary>
    public List<EntraChange> SnapshotChanges { get; init; } = new();

    /// <summary>Directory audit events from the review period.</summary>
    public EntraAuditReport AuditReport { get; init; } = new();

    public bool HasChanges => SnapshotChanges.Count > 0 || AuditReport.HasEvents;
    public int TotalSnapshotChanges => SnapshotChanges.Count;

    /// <summary>
    /// Converts snapshot changes to a <see cref="ChangeReport"/> for use with the
    /// existing notification senders (Teams, Slack, Email).
    /// </summary>
    public ChangeReport ToChangeReport()
    {
        var changes = SnapshotChanges.Select(c => new PolicyChange
        {
            ContentType = c.Category,
            PolicyId = c.AppId,
            PolicyName = c.AppName,
            ChangeType = c.ChangeType switch
            {
                "Added" => Models.ChangeType.Added,
                "Removed" => Models.ChangeType.Removed,
                _ => Models.ChangeType.Modified
            },
            Severity = c.ChangeType is "OwnerAdded" or "OwnerRemoved" or "PermissionChanged"
                ? ChangeSeverity.Critical
                : ChangeSeverity.Warning,
            Details = c.Details,
            DetectedAt = c.DetectedAt
        }).ToList();

        return new ChangeReport
        {
            GeneratedAt = GeneratedAt,
            TenantId = TenantId,
            TenantName = $"Entra Monitor ({TenantId})",
            Changes = changes
        };
    }
}
