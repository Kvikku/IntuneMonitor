using System.Text.Json;
using IntuneMonitor.Config;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;
using IntuneMonitor.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Commands;

/// <summary>
/// Analyzes policy relationships and dependencies from backup data.
/// Shows which groups are targeted by which policies, assignment filter usage, and cross-policy relationships.
/// </summary>
public class DependencyCommand
{
    private readonly AppConfiguration _config;
    private readonly ILogger<DependencyCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public DependencyCommand(AppConfiguration config, ILoggerFactory? loggerFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<DependencyCommand>();
    }

    /// <summary>
    /// Analyzes policy dependencies and prints a summary.
    /// </summary>
    /// <param name="contentTypes">Optional content type filter.</param>
    /// <param name="jsonReportPath">Optional path to write a JSON dependency report.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dependency analysis report.</returns>
    public async Task<DependencyReport> RunAsync(
        IEnumerable<string>? contentTypes = null,
        string? jsonReportPath = null,
        CancellationToken cancellationToken = default)
    {
        ConsoleUI.WriteHeader("Policy Dependency Analysis");
        _logger.LogInformation("=== Policy Dependency Analysis ===");

        IBackupStorage storage;
        try
        {
            storage = BackupStorageFactory.Create(_config.Backup, _loggerFactory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage error");
            return new DependencyReport();
        }

        var types = ResolveContentTypes(contentTypes);

        var groupToPolicies = new Dictionary<string, List<PolicyReference>>(StringComparer.OrdinalIgnoreCase);
        var filterToPolicies = new Dictionary<string, List<PolicyReference>>(StringComparer.OrdinalIgnoreCase);
        int totalPolicies = 0;

        await ConsoleUI.StatusAsync("Analyzing policy dependencies...", async () =>
        {
            foreach (var contentType in types)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var backup = await storage.LoadBackupAsync(contentType, cancellationToken);
                if (backup == null) continue;

                foreach (var item in backup.Items)
                {
                    totalPolicies++;
                    var policyRef = new PolicyReference
                    {
                        ContentType = contentType,
                        PolicyId = item.Id ?? "",
                        PolicyName = item.Name ?? item.Id ?? "(unknown)"
                    };

                    if (item.PolicyData == null) continue;

                    if (item.PolicyData.Value.TryGetProperty("assignments", out var assignments)
                        && assignments.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var assignment in assignments.EnumerateArray())
                        {
                            if (!assignment.TryGetProperty("target", out var target))
                                continue;

                            var groupName = GetStringProp(target, "groupDisplayName");
                            var groupId = GetStringProp(target, "groupId");
                            var odataType = GetStringProp(target, "@odata.type");

                            var groupKey = groupName ?? groupId ?? odataType ?? "(unknown)";
                            if (!groupToPolicies.ContainsKey(groupKey))
                                groupToPolicies[groupKey] = new List<PolicyReference>();
                            groupToPolicies[groupKey].Add(policyRef);

                            var filterId = GetStringProp(target, "deviceAndAppManagementAssignmentFilterId");
                            if (!string.IsNullOrWhiteSpace(filterId))
                            {
                                if (!filterToPolicies.ContainsKey(filterId))
                                    filterToPolicies[filterId] = new List<PolicyReference>();
                                filterToPolicies[filterId].Add(policyRef);
                            }
                        }
                    }
                }
            }
        });

        var report = new DependencyReport
        {
            GeneratedAt = DateTime.UtcNow,
            TotalPolicies = totalPolicies,
            GroupAssignments = groupToPolicies
                .OrderByDescending(g => g.Value.Count)
                .ToDictionary(g => g.Key, g => g.Value),
            FilterUsage = filterToPolicies
                .OrderByDescending(f => f.Value.Count)
                .ToDictionary(f => f.Key, f => f.Value)
        };

        PrintSummary(report);

        if (!string.IsNullOrWhiteSpace(jsonReportPath))
            await WriteJsonReportAsync(report, jsonReportPath, cancellationToken);

        return report;
    }

    private void PrintSummary(DependencyReport report)
    {
        _logger.LogInformation("Analyzed {TotalPolicies} policies", report.TotalPolicies);
        _logger.LogInformation("{GroupCount} unique group targets found", report.GroupAssignments.Count);
        _logger.LogInformation("{FilterCount} assignment filters in use", report.FilterUsage.Count);

        if (report.GroupAssignments.Count > 0)
        {
            _logger.LogInformation("--- Top Group Targets ---");
            foreach (var (group, policies) in report.GroupAssignments.Take(15))
            {
                _logger.LogInformation("  {GroupName}: {PolicyCount} policies", group, policies.Count);
                foreach (var p in policies.Take(5))
                    _logger.LogInformation("    → [{ContentType}] {PolicyName}", p.ContentType, p.PolicyName);
                if (policies.Count > 5)
                    _logger.LogInformation("    ... and {More} more", policies.Count - 5);
            }
        }

        if (report.FilterUsage.Count > 0)
        {
            _logger.LogInformation("--- Assignment Filter Usage ---");
            foreach (var (filterId, policies) in report.FilterUsage.Take(10))
                _logger.LogInformation("  Filter {FilterId}: {PolicyCount} policies", filterId, policies.Count);
        }
    }

    private async Task WriteJsonReportAsync(DependencyReport report, string outputPath, CancellationToken cancellationToken)
    {
        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(report, options);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);
            _logger.LogInformation("Dependency report written to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write dependency report to '{OutputPath}'", outputPath);
        }
    }

    private List<string> ResolveContentTypes(IEnumerable<string>? specified)
    {
        if (specified != null)
        {
            var list = specified.ToList();
            if (list.Count > 0) return list;
        }

        if (_config.ContentTypes?.Count > 0)
            return _config.ContentTypes;

        return IntuneContentTypes.All.ToList();
    }

    private static string? GetStringProp(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}

/// <summary>A reference to a specific policy within a content type.</summary>
public record PolicyReference
{
    /// <summary>The content type of the policy.</summary>
    public required string ContentType { get; init; }

    /// <summary>The policy ID.</summary>
    public required string PolicyId { get; init; }

    /// <summary>The display name of the policy.</summary>
    public required string PolicyName { get; init; }
}

/// <summary>Result of a policy dependency analysis.</summary>
public record DependencyReport
{
    /// <summary>When the analysis was generated.</summary>
    public DateTime GeneratedAt { get; init; }

    /// <summary>Total number of policies analyzed.</summary>
    public int TotalPolicies { get; init; }

    /// <summary>Map of group name/ID to policies targeting that group.</summary>
    public Dictionary<string, List<PolicyReference>> GroupAssignments { get; init; } = new();

    /// <summary>Map of assignment filter ID to policies using that filter.</summary>
    public Dictionary<string, List<PolicyReference>> FilterUsage { get; init; } = new();
}
