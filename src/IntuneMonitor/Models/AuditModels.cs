namespace IntuneMonitor.Models;

/// <summary>
/// Represents a single Intune audit event from the Microsoft Graph API.
/// </summary>
public record AuditEvent
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ComponentName { get; init; } = string.Empty;
    public string Activity { get; init; } = string.Empty;
    public string ActivityType { get; init; } = string.Empty;
    public string ActivityResult { get; init; } = string.Empty;
    public DateTime ActivityDateTime { get; init; }
    public AuditActor? Actor { get; init; }
    public List<AuditResource> Resources { get; init; } = new();
}

/// <summary>
/// Actor that performed an audit event (user, application, etc.).
/// </summary>
public record AuditActor
{
    public string? ApplicationDisplayName { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? IpAddress { get; init; }
}

/// <summary>
/// Resource affected by an audit event.
/// </summary>
public record AuditResource
{
    public string DisplayName { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
}

/// <summary>
/// Summary of audit log activity for a given time period.
/// </summary>
public record AuditLogReport
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public string TenantId { get; init; } = string.Empty;
    public int DaysReviewed { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public int TotalEvents { get; init; }
    public List<AuditEvent> Events { get; init; } = new();

    /// <summary>Event counts grouped by activity type (Create, Update, Delete, etc.).</summary>
    public Dictionary<string, int> EventsByActivityType { get; init; } = new();

    /// <summary>Event counts grouped by component name.</summary>
    public Dictionary<string, int> EventsByComponent { get; init; } = new();

    /// <summary>Event counts grouped by actor (user principal name or app display name).</summary>
    public Dictionary<string, int> EventsByActor { get; init; } = new();
}
