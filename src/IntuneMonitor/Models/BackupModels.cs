using System.Text.Json;

namespace IntuneMonitor.Models;

/// <summary>
/// Represents a single Intune policy item as stored in a backup.
/// </summary>
public record IntuneItem
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Platform { get; init; }
    public string? ContentType { get; init; }
    public DateTime? LastModifiedDateTime { get; init; }
    public DateTime? CreatedDateTime { get; init; }

    /// <summary>Full raw policy data from Microsoft Graph.</summary>
    public JsonElement? PolicyData { get; init; }
}

/// <summary>
/// Represents a full backup document for one content type.
/// </summary>
public record BackupDocument
{
    public string ExportedAt { get; init; } = DateTime.UtcNow.ToString("o");
    public string TenantId { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public List<IntuneItem> Items { get; init; } = new();
}

/// <summary>
/// Severity level of a detected change.
/// </summary>
public enum ChangeSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Describes a single detected change between the backup and the current live state.
/// </summary>
public record PolicyChange
{
    public required string ContentType { get; init; }
    public required string PolicyId { get; init; }
    public required string PolicyName { get; init; }
    public required ChangeType ChangeType { get; init; }
    public ChangeSeverity Severity { get; init; } = ChangeSeverity.Warning;
    public List<FieldChange> FieldChanges { get; init; } = new();
    public string? Details { get; init; }
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Type of change detected.
/// </summary>
public enum ChangeType
{
    Added,
    Removed,
    Modified
}

/// <summary>
/// Describes a single field-level change within a policy.
/// </summary>
public record FieldChange
{
    public required string FieldPath { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

/// <summary>
/// Summary report of all detected changes across content types.
/// </summary>
public record ChangeReport
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public string TenantId { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public List<PolicyChange> Changes { get; init; } = new();

    public int AddedCount => Changes.Count(c => c.ChangeType == ChangeType.Added);
    public int RemovedCount => Changes.Count(c => c.ChangeType == ChangeType.Removed);
    public int ModifiedCount => Changes.Count(c => c.ChangeType == ChangeType.Modified);
    public int TotalCount => Changes.Count;

    public bool HasChanges => TotalCount > 0;
}
