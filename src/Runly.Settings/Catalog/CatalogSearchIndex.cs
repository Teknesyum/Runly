namespace Runly.Settings.Catalog;

internal sealed record CatalogSearchRow(
    CatalogEntry Entry,
    string NormalizedExtension,
    string ExtensionLower,
    string DisplayNameEnLower,
    string[] SuggestedAppsLower);

internal static class CatalogSearchIndex
{
    private static IReadOnlyList<CatalogEntry>? _source;
    private static CatalogSearchRow[] _rows = [];
    private static Dictionary<string, CatalogEntry> _byExtension = new(StringComparer.OrdinalIgnoreCase);

    public static CatalogSearchRow[] Rows
    {
        get
        {
            EnsureBuilt();
            return _rows;
        }
    }

    public static CatalogEntry? Find(string extension)
    {
        EnsureBuilt();
        return _byExtension.GetValueOrDefault(extension);
    }

    private static void EnsureBuilt()
    {
        var entries = ExtensionCatalog.Entries;
        if (ReferenceEquals(_source, entries))
        {
            return;
        }

        var rows = new CatalogSearchRow[entries.Count];
        var byExtension = new Dictionary<string, CatalogEntry>(entries.Count, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            rows[i] = new CatalogSearchRow(
                entry,
                Core.Models.RunlyConfig.NormalizeExtension(entry.Extension),
                entry.Extension.ToLowerInvariant(),
                entry.DisplayName.En.ToLowerInvariant(),
                Array.ConvertAll(entry.SuggestedApps, app => app.ToLowerInvariant()));
            byExtension.TryAdd(entry.Extension, entry);
        }

        _rows = rows;
        _byExtension = byExtension;
        _source = entries;
    }
}
