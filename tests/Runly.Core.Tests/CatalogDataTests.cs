using System.Text.Json;

namespace Runly.Core.Tests;

/// <summary>Validates the Settings extension catalog as product data.</summary>
public sealed class CatalogDataTests
{
    private static readonly HashSet<string> Categories =
    ["scripts", "code", "text", "data", "web", "images", "audio", "video", "archive", "office", "design", "fonts", "locked", "special"];

    /// <summary>
    /// Script types that run with the full privileges of the signed-in user once bound to Run.
    /// They stay unblocked on purpose, so the risk note is the only thing that warns the user.
    /// </summary>
    private static readonly string[] RiskyScriptExtensions =
    [".hta", ".vbs", ".wsf", ".jar", ".js", ".ps1"];

    private static JsonDocument LoadCatalog()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "Runly.Settings", "Catalog", "catalog.json"));
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    [Fact]
    public void Catalog_HasUniqueExtensionsValidCategoriesAndCompleteMetadata()
    {
        using var document = LoadCatalog();
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
            }
        }
    }

    [Fact]
    public void Catalog_KeepsRiskNoteOnUnblockedScriptTypesThatRunWithFullPrivileges()
    {
        using var document = LoadCatalog();
        var entries = document.RootElement.EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("extension").GetString()!, StringComparer.OrdinalIgnoreCase);

        foreach (var extension in RiskyScriptExtensions)
        {
            Assert.True(entries.TryGetValue(extension, out var entry), $"Missing catalog entry: {extension}");
            Assert.True(entry.TryGetProperty("riskNote", out var riskNote), $"Missing riskNote: {extension}");
            Assert.False(string.IsNullOrWhiteSpace(riskNote.GetProperty("tr").GetString()), $"Empty Turkish riskNote: {extension}");
            Assert.False(string.IsNullOrWhiteSpace(riskNote.GetProperty("en").GetString()), $"Empty English riskNote: {extension}");
        }
    }
}
