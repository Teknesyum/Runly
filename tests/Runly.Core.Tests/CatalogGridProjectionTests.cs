using Runly.Core.Models;
using Runly.Settings.Catalog;

namespace Runly.Core.Tests;

public sealed class CatalogGridProjectionTests
{
    [Fact]
    public void SpecialCategory_IncludesDisabledCustomExtensionWithoutThrowing()
    {
        var config = new RunlyConfig
        {
            Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                [".foo"] = new() { Category = "special", Enabled = false },
            },
        };

        var result = CatalogGridProjection.GetExtensions(ExtensionCatalog.Entries, config, "special", string.Empty);

        Assert.Contains(".foo", result);
    }

    [Fact]
    public void Search_FindsExtensionOutsideTheSelectedCategory()
    {
        var config = new RunlyConfig();

        var result = CatalogGridProjection.GetExtensions(ExtensionCatalog.Entries, config, "scripts", "md");

        Assert.Contains(".md", result);
    }

    [Fact]
    public void Search_MatchesLocalisedTypeNameAndSuggestedApplication()
    {
        var config = new RunlyConfig();

        Assert.Contains(".md", CatalogGridProjection.GetExtensions(ExtensionCatalog.Entries, config, "images", "markdown"));
        Assert.Contains(".md", CatalogGridProjection.GetExtensions(ExtensionCatalog.Entries, config, "images", "notepad.exe"));
    }
}
