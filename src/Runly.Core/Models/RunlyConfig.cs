using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Runly.Core.Models;

/// <summary>The contents of <c>%APPDATA%\Runly\config.json</c> (SPEC 5.1).</summary>
public sealed record RunlyConfig
{
    /// <summary>Schema version this build writes and understands.</summary>
    public const int CurrentVersion = 2;

    /// <summary>Schema version of the loaded file; used by the migration hook in the config store.</summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = CurrentVersion;

    /// <summary>User-interface language; supported values are <c>tr</c> and <c>en</c>.</summary>
    [JsonPropertyName("language")]
    public string Language { get; init; } = "tr";

    /// <summary>How often the security gate prompts the user.</summary>
    [JsonPropertyName("securityMode")]
    public SecurityMode SecurityMode { get; init; } = SecurityMode.TrustOnFirstUse;

    /// <summary>Whether the console window stays open after the script finishes.</summary>
    [JsonPropertyName("keepWindowOpen")]
    public KeepWindowMode KeepWindowOpen { get; init; } = KeepWindowMode.OnError;

    /// <summary>Command used by the "edit" verb; when empty the launcher falls back to Notepad.</summary>
    [JsonPropertyName("editorCommand")]
    public string EditorCommand { get; init; } = string.Empty;

    /// <summary>Whether file logging is enabled.</summary>
    [JsonPropertyName("logEnabled")]
    public bool LogEnabled { get; init; } = true;

    /// <summary>Extension mappings keyed by lower-case extension including the leading dot (for example <c>.js</c>).</summary>
    [JsonPropertyName("extensions")]
    public Dictionary<string, ExtensionMapping> Extensions { get; init; } = CreateExtensionDictionary();

    /// <summary>Creates an empty extension dictionary with the comparer Runly expects (case-insensitive).</summary>
    public static Dictionary<string, ExtensionMapping> CreateExtensionDictionary() =>
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Normalises an extension to the canonical key form: lower-case, with a single leading dot.</summary>
    public static string NormalizeExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        var trimmed = extension.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (trimmed[0] != '.')
        {
            trimmed = "." + trimmed;
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>Looks up the mapping for an extension, tolerating case and a missing leading dot.</summary>
    public bool TryGetMapping(string extension, [NotNullWhen(true)] out ExtensionMapping? mapping)
    {
        mapping = null;
        var key = NormalizeExtension(extension);
        return key.Length != 0 && Extensions.TryGetValue(key, out mapping);
    }
}
