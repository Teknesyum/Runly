using System.Text.Json;
using System.Text.RegularExpressions;

namespace Runly.Core.Tests;

/// <summary>Keeps the two shipped UI dictionaries interchangeable: same keys, same placeholders.</summary>
public sealed partial class LocaleCatalogTests
{
    [GeneratedRegex(@"\{[a-zA-Z]+\}")]
    private static partial Regex Placeholder();

    private static Dictionary<string, string> Load(string language)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "Runly.Settings", "locale", language + ".json"));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
    }

    [Fact]
    public void BothLanguages_ShareTheSameKeys()
    {
        var tr = Load("tr");
        var en = Load("en");

        Assert.Empty(tr.Keys.Except(en.Keys, StringComparer.Ordinal));
        Assert.Empty(en.Keys.Except(tr.Keys, StringComparer.Ordinal));
    }

    [Fact]
    public void BothLanguages_ShareTheSamePlaceholders()
    {
        var tr = Load("tr");
        var en = Load("en");

        foreach (var pair in tr)
        {
            var source = Placeholder().Matches(pair.Value).Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal);
            var target = Placeholder().Matches(en[pair.Key]).Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal);
            Assert.Equal(source, target);
        }
    }

    [Fact]
    public void ApplicationPickerKeys_ArePresentInBothLanguages()
    {
        string[] required =
        [
            "catalog.searchLabel", "catalog.searchClear", "catalog.searchResults", "catalog.searchNoResults",
            "catalog.chooseApp", "chooseApp.title", "chooseApp.prompt", "chooseApp.promptRun",
            "chooseApp.searchPlaceholder", "chooseApp.suggested", "chooseApp.browse", "chooseApp.select",
            "chooseApp.empty", "chooseApp.filter", "chooseApp.pickOne", "chooseApp.noRow", "chooseApp.assigned",
            "handler.choosePrompt", "handler.notSelectedDetail", "bind.openFileTypePage",
            "bind.needsInstall", "bind.notRegistered", "install.launcherMissing",
        ];

        var tr = Load("tr");
        var en = Load("en");

        foreach (var key in required)
        {
            Assert.False(string.IsNullOrWhiteSpace(tr.GetValueOrDefault(key)), $"tr.json is missing {key}");
            Assert.False(string.IsNullOrWhiteSpace(en.GetValueOrDefault(key)), $"en.json is missing {key}");
        }
    }
}
