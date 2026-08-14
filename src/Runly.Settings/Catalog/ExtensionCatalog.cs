using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Runly.Settings.Catalog;

internal static class ExtensionCatalog
{
    private const string ResourceName = "Runly.Settings.catalog.json";
    private static readonly Lazy<IReadOnlyList<CatalogEntry>> EntriesSource = new(LoadCore);
    public static IReadOnlyList<CatalogEntry> Entries => EntriesSource.Value;

    private static IReadOnlyList<CatalogEntry> LoadCore()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded catalog resource '{ResourceName}' was not found.");
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        return JsonSerializer.Deserialize<CatalogEntry[]>(stream, options)
            ?? throw new InvalidDataException("The embedded extension catalog is empty.");
    }
}
