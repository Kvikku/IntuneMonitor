using System.Text.Json;

namespace IntuneMonitor.Graph;

/// <summary>
/// Centralized <see cref="JsonSerializerOptions"/> instances shared across the project.
/// Avoids creating duplicate options objects in every class that serializes/deserializes JSON.
/// </summary>
internal static class JsonDefaults
{
    /// <summary>
    /// Options for writing indented, camelCase JSON (reports, backups).
    /// </summary>
    public static readonly JsonSerializerOptions IndentedCamelCase = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Options for reading JSON in a case-insensitive manner (backup loading).
    /// </summary>
    public static readonly JsonSerializerOptions CaseInsensitiveRead = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Compact (non-indented) options used for comparison and hashing.
    /// </summary>
    public static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false
    };
}
