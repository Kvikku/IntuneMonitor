namespace IntuneMonitor.Models;

/// <summary>
/// Resolves the effective set of content types to process, applying CLI overrides,
/// config defaults, and falling back to all known types.
/// </summary>
public static class ContentTypeResolver
{
    /// <summary>
    /// Returns the content types to process: explicit list → config list → all.
    /// </summary>
    public static List<string> Resolve(IEnumerable<string>? specified, IReadOnlyList<string>? configuredTypes)
    {
        if (specified != null)
        {
            var list = specified.ToList();
            if (list.Count > 0) return list;
        }

        if (configuredTypes is { Count: > 0 })
            return configuredTypes.ToList();

        return IntuneContentTypes.All.ToList();
    }
}
