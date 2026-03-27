using IntuneMonitor.Models;

namespace IntuneMonitor.Storage;

/// <summary>
/// Shared file-naming and folder-naming helpers used by both
/// <see cref="LocalFileStorage"/> and <see cref="GitStorage"/>.
/// </summary>
internal static class BackupFileHelpers
{
    /// <summary>
    /// Returns the folder name for a content type, falling back to the raw type string.
    /// </summary>
    public static string GetFolderName(string contentType) =>
        IntuneContentTypes.FolderNames.TryGetValue(contentType, out var folder)
            ? folder
            : contentType;

    /// <summary>
    /// Builds a deterministic file name for a backup item:
    /// "{sanitised-name}_{shortId}.json".
    /// </summary>
    public static string BuildFileName(IntuneItem item)
    {
        var name = SanitizeFileName(item.Name ?? item.Id ?? "unknown");
        var shortId = (item.Id ?? "noid").Split('-')[0];
        return $"{name}_{shortId}.json";
    }

    /// <summary>
    /// Replaces invalid file-name characters with underscores and truncates to 200 chars.
    /// </summary>
    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}
