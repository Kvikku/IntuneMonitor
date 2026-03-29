using System.Text.Json;

namespace IntuneMonitor.Graph;

/// <summary>
/// Extension-style helpers for extracting typed values from <see cref="JsonElement"/>
/// without throwing on missing properties.
/// </summary>
internal static class JsonElementHelpers
{
    /// <summary>
    /// Returns the string value of a property, or <c>null</c> if the property is missing
    /// or not a string.
    /// </summary>
    public static string? GetStringOrNull(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    /// <summary>
    /// Returns the string value of a property, or <see cref="string.Empty"/> if the property
    /// is missing or not a string.
    /// </summary>
    public static string GetStringOrEmpty(JsonElement element, string propertyName) =>
        GetStringOrNull(element, propertyName) ?? string.Empty;

    /// <summary>
    /// Parses a property value as a <see cref="DateTime"/>, returning <c>null</c> on failure.
    /// </summary>
    public static DateTime? TryParseDateTime(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop)
        && prop.ValueKind == JsonValueKind.String
        && DateTime.TryParse(prop.GetString(), out var dt)
            ? dt
            : null;

    /// <summary>
    /// Returns the display name from a Graph entity element, trying "displayName", then "name",
    /// then "id".
    /// </summary>
    public static string? GetDisplayName(JsonElement element)
    {
        foreach (var prop in new[] { "displayName", "name", "id" })
        {
            if (element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
        }
        return null;
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> object to a mutable dictionary.
    /// </summary>
    public static Dictionary<string, object?> ToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
            dict[prop.Name] = prop.Value;
        return dict;
    }
}
