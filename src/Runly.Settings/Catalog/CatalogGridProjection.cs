using Runly.Core.Models;

namespace Runly.Settings.Catalog;

internal static class CatalogGridProjection
{
    public static IReadOnlyList<string> GetExtensions(
        IReadOnlyList<CatalogEntry> catalog, RunlyConfig config, string? category, string query)
    {
        var result = new List<string>();
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in catalog)
        {
            if (query.Length == 0 && !string.Equals(entry.Category, category, StringComparison.Ordinal)) continue;
            if (query.Length > 0 && !entry.Extension.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !entry.DisplayName.Tr.Contains(query, StringComparison.CurrentCultureIgnoreCase) &&
                !entry.DisplayName.En.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !entry.SuggestedApps.Any(app => app.Contains(query, StringComparison.OrdinalIgnoreCase))) continue;
            var extension = RunlyConfig.NormalizeExtension(entry.Extension);
            yielded.Add(extension);
            result.Add(extension);
        }
        foreach (var pair in config.Extensions)
        {
            if (yielded.Contains(pair.Key) || (query.Length == 0 && !string.Equals(pair.Value.Category, category, StringComparison.Ordinal)) ||
                (query.Length > 0 && !pair.Key.Contains(query, StringComparison.OrdinalIgnoreCase))) continue;
            result.Add(pair.Key);
        }
        return result;
    }
}
