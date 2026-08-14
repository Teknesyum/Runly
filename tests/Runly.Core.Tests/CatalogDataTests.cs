using System.Text.Json;

namespace Runly.Core.Tests;

/// <summary>Validates the Settings extension catalog as product data.</summary>
public sealed class CatalogDataTests
{
    private static readonly HashSet<string> Categories =
    ["scripts", "code", "text", "data", "web", "images", "audio", "video", "archive", "office", "design", "fonts", "locked", "special"];

    [Fact]
    public void Catalog_HasUniqueExtensionsValidCategoriesAndCompleteMetadata()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "Runly.Settings", "Catalog", "catalog.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var entries = document.RootElement.EnumerateArray().ToArray();
        Assert.InRange(entries.Length, 390, 420);

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var extension = entry.GetProperty("extension").GetString();
            var category = entry.GetProperty("category").GetString();
            var blocked = entry.GetProperty("blocked").GetBoolean();
            Assert.NotNull(extension);
            Assert.True(extensions.Add(extension), $"Duplicate catalog extension: {extension}");
            Assert.Contains(category!, Categories);
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("displayName").GetProperty("tr").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("displayName").GetProperty("en").GetString()));
            if (blocked)
            {
                Assert.Equal("locked", category);
                Assert.True(entry.TryGetProperty("riskNote", out _));
            }
            else
            {
                Assert.NotEmpty(entry.GetProperty("suggestedApps").EnumerateArray());
                Assert.False(entry.TryGetProperty("riskNote", out _));
            }
        }
    }
}
