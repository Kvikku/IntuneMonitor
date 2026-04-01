using IntuneMonitor.Models;

namespace IntuneMonitor.Comparison;

/// <summary>
/// Compares live Intune policy data against a stored backup and generates a
/// <see cref="ChangeReport"/> describing all detected changes.
/// Delegates JSON diffing to <see cref="FieldComparer"/> and change construction
/// to <see cref="ChangeBuilder"/>.
/// </summary>
public class PolicyComparer
{
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
                changes.Add(ChangeBuilder.BuildAdded(contentType, id, live.Name));
        }

        // Removed: in backup but not in live
        foreach (var (id, backup) in backupById)
        {
            if (!liveById.ContainsKey(id))
                changes.Add(ChangeBuilder.BuildRemoved(contentType, id, backup.Name));
        }

        // Modified: in both, but data differs
        foreach (var (id, live) in liveById)
        {
            if (!backupById.TryGetValue(id, out var backup))
                continue;

            var fieldChanges = FieldComparer.ComputeFieldChanges(live.PolicyData, backup.PolicyData);

            if (fieldChanges.Count > 0)
                changes.Add(ChangeBuilder.BuildModified(contentType, id, live.Name, fieldChanges));
        }

        return changes;
    }
}
