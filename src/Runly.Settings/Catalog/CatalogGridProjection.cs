using Runly.Core.Models;

namespace Runly.Settings.Catalog;

internal static class CatalogGridProjection
{
    public static IReadOnlyList<string> GetExtensions(
        IReadOnlyList<CatalogEntry> catalog, RunlyConfig config, string? category, string query)
        => GetExtensions(catalog, config, category, query, CancellationToken.None);

    public static IReadOnlyList<string> GetExtensions(
        IReadOnlyList<CatalogEntry> catalog, RunlyConfig config, string? category, string query,
        CancellationToken cancellationToken)
    {
        var rows = ReferenceEquals(catalog, ExtensionCatalog.Entries) ? CatalogSearchIndex.Rows : Build(catalog);
        var queryLower = query.ToLowerInvariant();
        var result = new List<string>();
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = row.Entry;
            if (query.Length == 0 && !string.Equals(entry.Category, category, StringComparison.Ordinal)) continue;
            // The Turkish name stays on a culture-sensitive compare: lowering it into the index would
            // change which rows match on a Turkish locale, and the search results must not move.
            if (query.Length > 0 && !row.ExtensionLower.Contains(queryLower, StringComparison.Ordinal) &&
                !entry.DisplayName.Tr.Contains(query, StringComparison.CurrentCultureIgnoreCase) &&
                !row.DisplayNameEnLower.Contains(queryLower, StringComparison.Ordinal) &&
                !Array.Exists(row.SuggestedAppsLower, app => app.Contains(queryLower, StringComparison.Ordinal))) continue;
            yielded.Add(row.NormalizedExtension);
            result.Add(row.NormalizedExtension);
        }
        foreach (var pair in config.Extensions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (yielded.Contains(pair.Key) || (query.Length == 0 && !string.Equals(pair.Value.Category, category, StringComparison.Ordinal)) ||
                (query.Length > 0 && !pair.Key.Contains(query, StringComparison.OrdinalIgnoreCase))) continue;
            result.Add(pair.Key);
        }
        return result;
    }

    private static CatalogSearchRow[] Build(IReadOnlyList<CatalogEntry> catalog)
    {
        var rows = new CatalogSearchRow[catalog.Count];
        for (var i = 0; i < catalog.Count; i++)
        {
            var entry = catalog[i];
            rows[i] = new CatalogSearchRow(
                entry,
                RunlyConfig.NormalizeExtension(entry.Extension),
                entry.Extension.ToLowerInvariant(),
                entry.DisplayName.En.ToLowerInvariant(),
                Array.ConvertAll(entry.SuggestedApps, app => app.ToLowerInvariant()));
        }
        return rows;
    }
}
