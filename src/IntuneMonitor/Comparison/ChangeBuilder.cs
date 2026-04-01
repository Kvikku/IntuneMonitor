using IntuneMonitor.Models;

namespace IntuneMonitor.Comparison;

/// <summary>
/// Constructs <see cref="PolicyChange"/> instances with consistent field population.
/// Extracted from <see cref="PolicyComparer"/> to isolate change construction logic.
/// </summary>
internal static class ChangeBuilder
{
    /// <summary>Creates a <see cref="PolicyChange"/> for a newly added policy.</summary>
    public static PolicyChange BuildAdded(string contentType, string id, string? name) =>
        new()
        {
            ContentType = contentType,
            PolicyId = id,
            PolicyName = name ?? id,
            ChangeType = ChangeType.Added,
            Severity = ChangeSeverity.Info,
            Details = $"Policy '{name ?? id}' was created.",
            DetectedAt = DateTime.UtcNow
        };

    /// <summary>Creates a <see cref="PolicyChange"/> for a deleted policy.</summary>
    public static PolicyChange BuildRemoved(string contentType, string id, string? name) =>
        new()
        {
            ContentType = contentType,
            PolicyId = id,
            PolicyName = name ?? id,
            ChangeType = ChangeType.Removed,
            Severity = ChangeSeverity.Critical,
            Details = $"Policy '{name ?? id}' was deleted.",
            DetectedAt = DateTime.UtcNow
        };

    /// <summary>Creates a <see cref="PolicyChange"/> for a modified policy with field-level changes.</summary>
    public static PolicyChange BuildModified(string contentType, string id, string? name, IReadOnlyList<FieldChange> fieldChanges) =>
        new()
        {
            ContentType = contentType,
            PolicyId = id,
            PolicyName = name ?? id,
            ChangeType = ChangeType.Modified,
            Severity = ChangeSeverity.Warning,
            FieldChanges = fieldChanges.ToList(),
            Details = $"Policy '{name ?? id}' was modified ({fieldChanges.Count} field(s) changed).",
            DetectedAt = DateTime.UtcNow
        };
}
