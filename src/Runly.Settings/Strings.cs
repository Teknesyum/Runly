using System.Reflection;
using System.Text.Json;

namespace Runly.Settings;

/// <summary>UI dictionary loaded from the embedded <c>locale/*.json</c> files. Translators edit the
/// JSON; nothing here changes. Embedded rather than satellite assemblies so the shipped layout stays
/// a single folder, and parsed at runtime because this project is never trimmed or AOT-compiled.</summary>
internal static class Strings
{
    private const string SourceLanguage = "tr";

    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = LoadCatalogs();

    /// <summary>Source text to key. Built once so a language switch is a lookup, not a scan.</summary>
    private static readonly Dictionary<string, string> KeyByText = BuildReverseIndex();

    public static string Language { get; set; } = SourceLanguage;

    public static string Get(string key) =>
        Catalogs.TryGetValue(Language, out var catalog) && catalog.TryGetValue(key, out var value)
            ? value
            : Catalogs[SourceLanguage].TryGetValue(key, out var source) ? source : key;

    public static string Translate(string text) =>
        string.IsNullOrEmpty(text) ? text : KeyByText.TryGetValue(text, out var key) ? Get(key) : text;

    public static void Apply(Control root)
    {
        // A RichTextBox holds rendered runs (bold, mono, neon-pink code spans), not a translatable
        // caption. Assigning .Text here would flatten every run back to plain body text.
        if (root is RichTextBox)
        {
            return;
        }

        root.Text = Translate(root.Text);
        if (root is NeonGroupPanel group) group.Title = Translate(group.Title);
        if (root is DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = Translate(column.HeaderText);
        }
        foreach (Control child in root.Controls) Apply(child);
        root.Invalidate();
    }

    private static Dictionary<string, Dictionary<string, string>> LoadCatalogs()
    {
        var catalogs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith("Runly.Settings.locale.", StringComparison.Ordinal) ||
                !name.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            var code = name["Runly.Settings.locale.".Length..^".json".Length];
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            if (parsed is not null) catalogs[code] = new Dictionary<string, string>(parsed, StringComparer.Ordinal);
        }

        if (!catalogs.ContainsKey(SourceLanguage))
        {
            throw new InvalidOperationException($"locale/{SourceLanguage}.json gömülü kaynaklarda bulunamadı.");
        }

        return catalogs;
    }

    private static Dictionary<string, string> BuildReverseIndex()
    {
        // Every language maps back to its key so switching works in both directions. A repeated value
        // resolves to the first key that declared it; the ones that repeat today are identical across
        // languages, so the winner does not matter.
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var catalog in Catalogs.Values)
        {
            foreach (var pair in catalog) index.TryAdd(pair.Value, pair.Key);
        }
        return index;
    }
}
