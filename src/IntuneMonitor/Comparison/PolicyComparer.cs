using System.Text.Json;
using IntuneMonitor.Models;

namespace IntuneMonitor.Comparison;

/// <summary>
/// Compares live Intune policy data against a stored backup and generates a
/// <see cref="ChangeReport"/> describing all detected changes.
/// </summary>
public class PolicyComparer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Compares the current (live) items against the backup for a single content type.
    /// </summary>
    /// <param name="contentType">The content type being compared.</param>
    /// <param name="liveItems">Current items fetched from Microsoft Graph.</param>
    /// <param name="backupDocument">The stored backup (may be null if no backup exists).</param>
    /// <returns>A list of <see cref="PolicyChange"/> describing the differences.</returns>
    public IReadOnlyList<PolicyChange> Compare(
        string contentType,
        IReadOnlyList<IntuneItem> liveItems,
        BackupDocument? backupDocument)
    {
        var changes = new List<PolicyChange>();

        var backupItems = backupDocument?.Items ?? new List<IntuneItem>();

        var liveById = liveItems
            .Where(i => i.Id != null)
            .ToDictionary(i => i.Id!, StringComparer.OrdinalIgnoreCase);

        var backupById = backupItems
            .Where(i => i.Id != null)
            .ToDictionary(i => i.Id!, StringComparer.OrdinalIgnoreCase);

        // Added: in live but not in backup
        foreach (var (id, live) in liveById)
        {
            if (!backupById.ContainsKey(id))
            {
                changes.Add(new PolicyChange
                {
                    ContentType = contentType,
                    PolicyId = id,
                    PolicyName = live.Name ?? id,
                    ChangeType = ChangeType.Added,
                    Severity = ChangeSeverity.Info,
                    Details = $"Policy '{live.Name}' was created.",
                    DetectedAt = DateTime.UtcNow
                });
            }
        }

        // Removed: in backup but not in live
        foreach (var (id, backup) in backupById)
        {
            if (!liveById.ContainsKey(id))
            {
                changes.Add(new PolicyChange
                {
                    ContentType = contentType,
                    PolicyId = id,
                    PolicyName = backup.Name ?? id,
                    ChangeType = ChangeType.Removed,
                    Severity = ChangeSeverity.Critical,
                    Details = $"Policy '{backup.Name}' was deleted.",
                    DetectedAt = DateTime.UtcNow
                });
            }
        }

        // Modified: in both, but data differs
        foreach (var (id, live) in liveById)
        {
            if (!backupById.TryGetValue(id, out var backup))
                continue;

            var fieldChanges = ComputeFieldChanges(live.PolicyData, backup.PolicyData);

            if (fieldChanges.Count > 0)
            {
                changes.Add(new PolicyChange
                {
                    ContentType = contentType,
                    PolicyId = id,
                    PolicyName = live.Name ?? id,
                    ChangeType = ChangeType.Modified,
                    Severity = ChangeSeverity.Warning,
                    FieldChanges = fieldChanges,
                    Details = $"Policy '{live.Name}' was modified ({fieldChanges.Count} field(s) changed).",
                    DetectedAt = DateTime.UtcNow
                });
            }
        }

        return changes;
    }

    /// <summary>
    /// Computes field-level differences between two JSON policy snapshots.
    /// </summary>
    private static List<FieldChange> ComputeFieldChanges(
        JsonElement? live,
        JsonElement? backup)
    {
        var changes = new List<FieldChange>();

        if (live == null && backup == null) return changes;

        if (live == null)
        {
            changes.Add(new FieldChange { FieldPath = "(root)", OldValue = Serialize(backup), NewValue = null });
            return changes;
        }

        if (backup == null)
        {
            changes.Add(new FieldChange { FieldPath = "(root)", OldValue = null, NewValue = Serialize(live) });
            return changes;
        }

        DiffJsonElements(live.Value, backup.Value, string.Empty, changes);
        return changes;
    }

    private static readonly HashSet<string> IgnoredFields = new(StringComparer.OrdinalIgnoreCase)
    {
        // Fields that change automatically and are not meaningful config changes
        "lastModifiedDateTime",
        "version",
        "settingsCount",
        "isAssigned",
        "@odata.context"
    };

    private static void DiffJsonElements(
        JsonElement live,
        JsonElement backup,
        string path,
        List<FieldChange> changes)
    {
        if (live.ValueKind != backup.ValueKind)
        {
            // Type changed
            changes.Add(new FieldChange
            {
                FieldPath = path.TrimStart('.'),
                OldValue = Serialize(backup),
                NewValue = Serialize(live)
            });
            return;
        }

        switch (live.ValueKind)
        {
            case JsonValueKind.Object:
                DiffObjects(live, backup, path, changes);
                break;

            case JsonValueKind.Array:
                DiffArrays(live, backup, path, changes);
                break;

            default:
                // Scalar: compare directly
                var liveStr = Serialize(live);
                var backupStr = Serialize(backup);
                if (!string.Equals(liveStr, backupStr, StringComparison.Ordinal))
                {
                    changes.Add(new FieldChange
                    {
                        FieldPath = path.TrimStart('.'),
                        OldValue = backupStr,
                        NewValue = liveStr
                    });
                }
                break;
        }
    }

    private static void DiffObjects(
        JsonElement live,
        JsonElement backup,
        string path,
        List<FieldChange> changes)
    {
        var liveProps = live.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var backupProps = backup.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        var allKeys = liveProps.Keys.Union(backupProps.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var key in allKeys)
        {
            if (IgnoredFields.Contains(key)) continue;

            var fieldPath = $"{path}.{key}";

            if (!backupProps.TryGetValue(key, out var backupVal))
            {
                changes.Add(new FieldChange
                {
                    FieldPath = fieldPath.TrimStart('.'),
                    OldValue = null,
                    NewValue = Serialize(liveProps[key])
                });
            }
            else if (!liveProps.TryGetValue(key, out var liveVal))
            {
                changes.Add(new FieldChange
                {
                    FieldPath = fieldPath.TrimStart('.'),
                    OldValue = Serialize(backupVal),
                    NewValue = null
                });
            }
            else
            {
                DiffJsonElements(liveVal, backupVal, fieldPath, changes);
            }
        }
    }

    private static void DiffArrays(
        JsonElement live,
        JsonElement backup,
        string path,
        List<FieldChange> changes)
    {
        var liveItems = live.EnumerateArray().ToList();
        var backupItems = backup.EnumerateArray().ToList();

        // Simple approach: compare the serialized form of the whole array
        var liveJson = Serialize(live);
        var backupJson = Serialize(backup);

        if (!string.Equals(liveJson, backupJson, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange
            {
                FieldPath = path.TrimStart('.'),
                OldValue = backupJson,
                NewValue = liveJson
            });
        }
    }

    private static string? Serialize(JsonElement? element)
    {
        if (element == null) return null;
        return element.Value.ValueKind == JsonValueKind.String
            ? element.Value.GetString()
            : JsonSerializer.Serialize(element.Value, JsonOptions);
    }
}
