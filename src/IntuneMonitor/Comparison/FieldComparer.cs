using System.Text.Json;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;

namespace IntuneMonitor.Comparison;

/// <summary>
/// Compares two JSON policy snapshots and produces a list of field-level differences.
/// Extracted from <see cref="PolicyComparer"/> to isolate the deep JSON diff engine.
/// </summary>
internal static class FieldComparer
{
    private static readonly HashSet<string> IgnoredFields = new(StringComparer.OrdinalIgnoreCase)
    {
        // Fields that change automatically and are not meaningful config changes
        "lastModifiedDateTime",
        "version",
        "settingsCount",
        "isAssigned",
        "@odata.context"
    };

    /// <summary>
    /// Computes field-level differences between two JSON policy snapshots.
    /// </summary>
    public static List<FieldChange> ComputeFieldChanges(
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
        var fieldName = path.Split('.').LastOrDefault() ?? "";

        // Special handling for assignments array — produce human-readable diffs
        if (fieldName.Equals("assignments", StringComparison.OrdinalIgnoreCase))
        {
            AssignmentComparer.DiffAssignments(live, backup, path, changes);
            return;
        }

        // Order-insensitive comparison: normalize both arrays by sorting elements
        // by their serialized form, so reordering alone doesn't produce false-positive diffs.
        var liveNormalized = NormalizeArray(live);
        var backupNormalized = NormalizeArray(backup);

        if (!string.Equals(liveNormalized, backupNormalized, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange
            {
                FieldPath = path.TrimStart('.'),
                OldValue = Serialize(backup),
                NewValue = Serialize(live)
            });
        }
    }

    /// <summary>
    /// Normalizes an array for comparison by sorting its elements by their serialized form.
    /// This ensures that two arrays with the same elements in different order compare as equal.
    /// </summary>
    private static string NormalizeArray(JsonElement array)
    {
        var elements = array.EnumerateArray()
            .Select(e => JsonSerializer.Serialize(e, JsonDefaults.Compact))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        return "[" + string.Join(",", elements) + "]";
    }

    private static string? Serialize(JsonElement? element)
    {
        if (element == null) return null;
        return element.Value.ValueKind == JsonValueKind.String
            ? element.Value.GetString()
            : JsonSerializer.Serialize(element.Value, JsonDefaults.Compact);
    }
}
