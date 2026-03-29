using System.Text.Json;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;

namespace IntuneMonitor.Storage;

/// <summary>
/// Validates backup files before import to detect corruption or invalid data.
/// </summary>
public class BackupValidator
{
    private readonly ILogger<BackupValidator> _logger;

    public BackupValidator(ILogger<BackupValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a backup document for import readiness.
    /// </summary>
    /// <returns>A validation result with any errors found.</returns>
    public BackupValidationResult Validate(BackupDocument document)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(document.ContentType))
            errors.Add("Missing ContentType");

        if (string.IsNullOrWhiteSpace(document.ExportedAt))
            warnings.Add("Missing ExportedAt timestamp");

        if (document.Items.Count == 0)
            warnings.Add("Backup contains no items");

        for (int i = 0; i < document.Items.Count; i++)
        {
            var item = document.Items[i];
            var prefix = $"Item[{i}]";

            if (string.IsNullOrWhiteSpace(item.Id))
                errors.Add($"{prefix}: Missing Id");

            if (string.IsNullOrWhiteSpace(item.Name) && string.IsNullOrWhiteSpace(item.Id))
                errors.Add($"{prefix}: Missing both Name and Id");

            if (item.PolicyData == null)
                errors.Add($"{prefix} ({item.Name ?? item.Id ?? "unknown"}): Missing PolicyData");
            else if (item.PolicyData.Value.ValueKind != JsonValueKind.Object)
                errors.Add($"{prefix} ({item.Name ?? item.Id ?? "unknown"}): PolicyData is not a JSON object");

            if (!string.IsNullOrWhiteSpace(item.ContentType) && !string.IsNullOrWhiteSpace(document.ContentType)
                && !string.Equals(item.ContentType, document.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{prefix} ({item.Name ?? item.Id ?? "unknown"}): ContentType '{item.ContentType}' doesn't match document ContentType '{document.ContentType}'");
            }
        }

        var idGroups = document.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .GroupBy(i => i.Id!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var group in idGroups)
            errors.Add($"Duplicate policy ID: '{group.Key}' appears {group.Count()} times");

        var result = new BackupValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };

        if (!result.IsValid)
            _logger.LogWarning("Backup validation failed with {ErrorCount} error(s) for {ContentType}",
                errors.Count, document.ContentType);
        else if (warnings.Count > 0)
            _logger.LogInformation("Backup validation passed with {WarningCount} warning(s) for {ContentType}",
                warnings.Count, document.ContentType);

        return result;
    }

    /// <summary>
    /// Validates all stored content types in a storage backend.
    /// </summary>
    public async Task<Dictionary<string, BackupValidationResult>> ValidateStorageAsync(
        IBackupStorage storage,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, BackupValidationResult>(StringComparer.OrdinalIgnoreCase);
        var storedTypes = await storage.ListStoredContentTypesAsync(cancellationToken);

        foreach (var contentType in storedTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var backup = await storage.LoadBackupAsync(contentType, cancellationToken);
            if (backup == null)
            {
                results[contentType] = new BackupValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Failed to load backup" }
                };
                continue;
            }

            results[contentType] = Validate(backup);
        }

        return results;
    }
}

/// <summary>
/// Result of a backup validation check.
/// </summary>
public record BackupValidationResult
{
    /// <summary>Whether the backup is valid for import.</summary>
    public bool IsValid { get; init; }

    /// <summary>Validation errors (prevent import).</summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>Validation warnings (informational, import still allowed).</summary>
    public List<string> Warnings { get; init; } = new();
}
