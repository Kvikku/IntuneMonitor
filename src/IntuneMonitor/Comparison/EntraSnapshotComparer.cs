using System.Text.Json;
using IntuneMonitor.Models;

namespace IntuneMonitor.Comparison;

/// <summary>
/// Compares two Entra snapshots and produces a list of changes (added/removed apps,
/// owner changes, permission changes) with detailed before/after diffs.
/// </summary>
public static class EntraSnapshotComparer
{
    /// <summary>
    /// Compares the current snapshot against a previous snapshot and returns detected changes.
    /// </summary>
    public static List<EntraChange> Compare(EntraSnapshot current, EntraSnapshot? previous)
    {
        var changes = new List<EntraChange>();

        if (previous == null)
            return changes; // No baseline to compare against

        CompareCategory("AppRegistration", current.AppRegistrations, previous.AppRegistrations, changes);
        CompareCategory("EnterpriseApplication", current.EnterpriseApplications, previous.EnterpriseApplications, changes);

        return changes;
    }

    private static void CompareCategory(
        string category,
        List<EntraAppItem> currentItems,
        List<EntraAppItem> previousItems,
        List<EntraChange> changes)
    {
        var currentById = currentItems.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var previousById = previousItems.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        // Added
        foreach (var item in currentItems.Where(i => !previousById.ContainsKey(i.Id)))
        {
            changes.Add(new EntraChange
            {
                Category = category,
                AppId = item.Id,
                AppName = item.DisplayName,
                ChangeType = "Added",
                Details = $"New {category}: {item.DisplayName}"
            });
        }

        // Removed
        foreach (var item in previousItems.Where(i => !currentById.ContainsKey(i.Id)))
        {
            changes.Add(new EntraChange
            {
                Category = category,
                AppId = item.Id,
                AppName = item.DisplayName,
                ChangeType = "Removed",
                Details = $"Removed {category}: {item.DisplayName}"
            });
        }

        // Modified — compare items present in both
        foreach (var current in currentItems)
        {
            if (!previousById.TryGetValue(current.Id, out var previous))
                continue;

            CompareOwners(category, current, previous, changes);
            CompareRequiredResourceAccess(category, current, previous, changes);
            CompareAppRoleAssignments(category, current, previous, changes);
            CompareOAuth2PermissionGrants(category, current, previous, changes);
        }
    }

    private static void CompareOwners(
        string category,
        EntraAppItem current,
        EntraAppItem previous,
        List<EntraChange> changes)
    {
        var currentOwners = GetOwnerEntries(current.Data);
        var previousOwners = GetOwnerEntries(previous.Data);

        var added = currentOwners.Where(c => !previousOwners.Any(p => p.Id == c.Id)).ToList();
        var removed = previousOwners.Where(p => !currentOwners.Any(c => c.Id == p.Id)).ToList();

        foreach (var owner in added)
        {
            changes.Add(new EntraChange
            {
                Category = category,
                AppId = current.Id,
                AppName = current.DisplayName,
                ChangeType = "OwnerAdded",
                Details = $"Owner added: {owner.DisplayLabel}"
            });
        }

        foreach (var owner in removed)
        {
            changes.Add(new EntraChange
            {
                Category = category,
                AppId = current.Id,
                AppName = current.DisplayName,
                ChangeType = "OwnerRemoved",
                Details = $"Owner removed: {owner.DisplayLabel}"
            });
        }
    }

    private static void CompareRequiredResourceAccess(
        string category,
        EntraAppItem current,
        EntraAppItem previous,
        List<EntraChange> changes)
    {
        var currentAccess = GetResourceAccessEntries(current.Data);
        var previousAccess = GetResourceAccessEntries(previous.Data);

        var added = currentAccess.Except(previousAccess).ToList();
        var removed = previousAccess.Except(currentAccess).ToList();

        if (added.Count == 0 && removed.Count == 0)
            return;

        var parts = new List<string>();
        if (added.Count > 0)
            parts.Add($"Added: {string.Join(", ", added.Select(a => $"{a.PermissionId} ({a.Type})"))}");
        if (removed.Count > 0)
            parts.Add($"Removed: {string.Join(", ", removed.Select(r => $"{r.PermissionId} ({r.Type})"))}");

        changes.Add(new EntraChange
        {
            Category = category,
            AppId = current.Id,
            AppName = current.DisplayName,
            ChangeType = "PermissionChanged",
            Details = $"Required resource access: {string.Join("; ", parts)}"
        });
    }

    private static void CompareAppRoleAssignments(
        string category,
        EntraAppItem current,
        EntraAppItem previous,
        List<EntraChange> changes)
    {
        var currentRoles = GetAppRoleAssignmentEntries(current.Data);
        var previousRoles = GetAppRoleAssignmentEntries(previous.Data);

        var added = currentRoles.Where(c => !previousRoles.Any(p => p.Id == c.Id)).ToList();
        var removed = previousRoles.Where(p => !currentRoles.Any(c => c.Id == p.Id)).ToList();

        if (added.Count == 0 && removed.Count == 0)
            return;

        var parts = new List<string>();
        if (added.Count > 0)
            parts.Add($"Added: {string.Join(", ", added.Select(a => a.DisplayLabel))}");
        if (removed.Count > 0)
            parts.Add($"Removed: {string.Join(", ", removed.Select(r => r.DisplayLabel))}");

        changes.Add(new EntraChange
        {
            Category = category,
            AppId = current.Id,
            AppName = current.DisplayName,
            ChangeType = "PermissionChanged",
            Details = $"App role assignments: {string.Join("; ", parts)}"
        });
    }

    private static void CompareOAuth2PermissionGrants(
        string category,
        EntraAppItem current,
        EntraAppItem previous,
        List<EntraChange> changes)
    {
        var currentGrants = GetOAuth2GrantEntries(current.Data);
        var previousGrants = GetOAuth2GrantEntries(previous.Data);

        // Compare by grant ID — check for added/removed grants and scope changes
        var currentById = currentGrants.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);
        var previousById = previousGrants.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);

        var parts = new List<string>();

        foreach (var grant in currentGrants.Where(g => !previousById.ContainsKey(g.Id)))
            parts.Add($"New grant: {grant.Scope}");

        foreach (var grant in previousGrants.Where(g => !currentById.ContainsKey(g.Id)))
            parts.Add($"Removed grant: {grant.Scope}");

        foreach (var grant in currentGrants)
        {
            if (!previousById.TryGetValue(grant.Id, out var prevGrant))
                continue;

            var currentScopes = SplitScopes(grant.Scope);
            var previousScopes = SplitScopes(prevGrant.Scope);

            var addedScopes = currentScopes.Except(previousScopes).ToList();
            var removedScopes = previousScopes.Except(currentScopes).ToList();

            if (addedScopes.Count > 0)
                parts.Add($"Scopes added: {string.Join(", ", addedScopes)}");
            if (removedScopes.Count > 0)
                parts.Add($"Scopes removed: {string.Join(", ", removedScopes)}");
        }

        if (parts.Count == 0)
            return;

        changes.Add(new EntraChange
        {
            Category = category,
            AppId = current.Id,
            AppName = current.DisplayName,
            ChangeType = "PermissionChanged",
            Details = $"Delegated permission grants: {string.Join("; ", parts)}"
        });
    }

    // ── Helper types and extractors ──────────────────────────────────────

    private record OwnerEntry(string Id, string DisplayLabel);

    private record ResourceAccessEntry(string ResourceAppId, string PermissionId, string Type)
    {
        public virtual bool Equals(ResourceAccessEntry? other) =>
            other != null &&
            string.Equals(ResourceAppId, other.ResourceAppId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(PermissionId, other.PermissionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Type, other.Type, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() =>
            HashCode.Combine(
                ResourceAppId.ToLowerInvariant(),
                PermissionId.ToLowerInvariant(),
                Type.ToLowerInvariant());
    }

    private record AppRoleEntry(string Id, string DisplayLabel);

    private record OAuth2GrantEntry(string Id, string Scope);

    private static List<OwnerEntry> GetOwnerEntries(JsonElement? data)
    {
        var entries = new List<OwnerEntry>();
        if (data == null) return entries;

        if (data.Value.TryGetProperty("owners", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var id = GetString(item, "id") ?? "";
                var upn = GetString(item, "userPrincipalName");
                var displayName = GetString(item, "displayName");
                var label = upn ?? displayName ?? id;
                entries.Add(new OwnerEntry(id, label));
            }
        }
        return entries;
    }

    private static List<ResourceAccessEntry> GetResourceAccessEntries(JsonElement? data)
    {
        var entries = new List<ResourceAccessEntry>();
        if (data == null) return entries;

        if (data.Value.TryGetProperty("requiredResourceAccess", out var rra) && rra.ValueKind == JsonValueKind.Array)
        {
            foreach (var resource in rra.EnumerateArray())
            {
                var resourceAppId = GetString(resource, "resourceAppId") ?? "";
                if (resource.TryGetProperty("resourceAccess", out var ra) && ra.ValueKind == JsonValueKind.Array)
                {
                    foreach (var access in ra.EnumerateArray())
                    {
                        var permId = GetString(access, "id") ?? "";
                        var type = GetString(access, "type") ?? "";
                        entries.Add(new ResourceAccessEntry(resourceAppId, permId, type));
                    }
                }
            }
        }
        return entries;
    }

    private static List<AppRoleEntry> GetAppRoleAssignmentEntries(JsonElement? data)
    {
        var entries = new List<AppRoleEntry>();
        if (data == null) return entries;

        if (data.Value.TryGetProperty("appRoleAssignments", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var id = GetString(item, "id") ?? "";
                var roleId = GetString(item, "appRoleId") ?? "";
                var principalName = GetString(item, "principalDisplayName");
                var resourceName = GetString(item, "resourceDisplayName");
                var label = principalName != null && resourceName != null
                    ? $"{principalName} → {resourceName} (role: {roleId})"
                    : $"role {roleId}";
                entries.Add(new AppRoleEntry(id, label));
            }
        }
        return entries;
    }

    private static List<OAuth2GrantEntry> GetOAuth2GrantEntries(JsonElement? data)
    {
        var entries = new List<OAuth2GrantEntry>();
        if (data == null) return entries;

        if (data.Value.TryGetProperty("oauth2PermissionGrants", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var id = GetString(item, "id") ?? "";
                var scope = GetString(item, "scope") ?? "";
                entries.Add(new OAuth2GrantEntry(id, scope));
            }
        }
        return entries;
    }

    private static HashSet<string> SplitScopes(string scope) =>
        new(scope.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
