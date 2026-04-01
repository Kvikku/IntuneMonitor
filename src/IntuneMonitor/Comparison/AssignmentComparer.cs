using System.Text.Json;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;

namespace IntuneMonitor.Comparison;

/// <summary>
/// Produces human-readable diffs for policy assignment arrays by comparing
/// individual assignments and showing friendly target names.
/// Extracted from <see cref="PolicyComparer"/> to isolate assignment-specific logic.
/// </summary>
internal static class AssignmentComparer
{
    /// <summary>
    /// Diffs assignment arrays and produces human-readable field changes showing
    /// added, removed, and modified assignments.
    /// </summary>
    public static void DiffAssignments(
        JsonElement live,
        JsonElement backup,
        string path,
        List<FieldChange> changes)
    {
        var liveList = live.EnumerateArray().ToList();
        var backupList = backup.EnumerateArray().ToList();

        var liveById = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var backupById = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in liveList)
        {
            var id = GetAssignmentKey(item);
            if (id != null) liveById[id] = item;
        }

        foreach (var item in backupList)
        {
            var id = GetAssignmentKey(item);
            if (id != null) backupById[id] = item;
        }

        // Added assignments
        foreach (var (key, item) in liveById)
        {
            if (!backupById.ContainsKey(key))
            {
                changes.Add(new FieldChange
                {
                    FieldPath = $"{path.TrimStart('.')}.added",
                    OldValue = null,
                    NewValue = DescribeAssignment(item)
                });
            }
        }

        // Removed assignments
        foreach (var (key, item) in backupById)
        {
            if (!liveById.ContainsKey(key))
            {
                changes.Add(new FieldChange
                {
                    FieldPath = $"{path.TrimStart('.')}.removed",
                    OldValue = DescribeAssignment(item),
                    NewValue = null
                });
            }
        }

        // Modified assignments (same key but different content)
        foreach (var (key, liveItem) in liveById)
        {
            if (backupById.TryGetValue(key, out var backupItem))
            {
                var liveJson = JsonSerializer.Serialize(liveItem, JsonDefaults.Compact);
                var backupJson = JsonSerializer.Serialize(backupItem, JsonDefaults.Compact);
                if (!string.Equals(liveJson, backupJson, StringComparison.Ordinal))
                {
                    changes.Add(new FieldChange
                    {
                        FieldPath = $"{path.TrimStart('.')}.modified",
                        OldValue = DescribeAssignment(backupItem),
                        NewValue = DescribeAssignment(liveItem)
                    });
                }
            }
        }
    }

    /// <summary>
    /// Builds a stable key for an assignment element, using the target group ID
    /// or OData type for virtual groups.
    /// </summary>
    private static string? GetAssignmentKey(JsonElement assignment)
    {
        if (assignment.TryGetProperty("target", out var target))
        {
            var groupId = target.TryGetProperty("groupId", out var gid) && gid.ValueKind == JsonValueKind.String
                ? gid.GetString()
                : null;
            if (groupId != null) return groupId;

            var odataType = target.TryGetProperty("@odata.type", out var odt) && odt.ValueKind == JsonValueKind.String
                ? odt.GetString()
                : null;
            if (odataType != null) return odataType;
        }

        // Fall back to the assignment id
        if (assignment.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            return id.GetString();

        return null;
    }

    /// <summary>
    /// Returns a human-readable description of an assignment target.
    /// </summary>
    private static string DescribeAssignment(JsonElement assignment)
    {
        if (!assignment.TryGetProperty("target", out var target))
            return "(unknown target)";

        // Prefer the friendly groupDisplayName we injected during export
        var displayName = target.TryGetProperty("groupDisplayName", out var dn) && dn.ValueKind == JsonValueKind.String
            ? dn.GetString()
            : null;

        var odataType = target.TryGetProperty("@odata.type", out var odt) && odt.ValueKind == JsonValueKind.String
            ? odt.GetString()
            : null;

        var groupId = target.TryGetProperty("groupId", out var gid) && gid.ValueKind == JsonValueKind.String
            ? gid.GetString()
            : null;

        var intent = assignment.TryGetProperty("intent", out var intentProp) && intentProp.ValueKind == JsonValueKind.String
            ? intentProp.GetString()
            : null;

        // Determine the assignment type from @odata.type
        var targetType = odataType switch
        {
            string t when t.Contains("exclusionGroup", StringComparison.OrdinalIgnoreCase) => "Exclude",
            string t when t.Contains("allLicensedUsers", StringComparison.OrdinalIgnoreCase) => "Include",
            string t when t.Contains("allDevices", StringComparison.OrdinalIgnoreCase) => "Include",
            string t when t.Contains("group", StringComparison.OrdinalIgnoreCase) => "Include",
            _ => intent ?? "Assign"
        };

        var name = displayName ?? groupId ?? FriendlyTypeName(odataType) ?? "(unknown)";
        return $"{targetType}: {name}";
    }

    private static string? FriendlyTypeName(string? odataType) =>
        odataType switch
        {
            "#microsoft.graph.allLicensedUsersAssignmentTarget" => "All Users",
            "#microsoft.graph.allDevicesAssignmentTarget" => "All Devices",
            _ => null
        };
}
