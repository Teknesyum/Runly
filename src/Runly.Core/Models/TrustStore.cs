using System.Text.Json.Serialization;

namespace Runly.Core.Models;

/// <summary>The contents of <c>%APPDATA%\Runly\trust.json</c> (SPEC 5.2).</summary>
public sealed record TrustStore
{
    /// <summary>Schema version this build writes and understands.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Schema version of the loaded file.</summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Full paths of folders whose scripts run without prompting, sub-folders included.</summary>
    [JsonPropertyName("trustedFolders")]
    public List<string> TrustedFolders { get; init; } = [];

    /// <summary>Individually trusted scripts keyed by full path.</summary>
    [JsonPropertyName("trustedFiles")]
    public Dictionary<string, TrustedFileEntry> TrustedFiles { get; init; } = CreateFileDictionary();

    /// <summary>Creates an empty trusted-file dictionary with the comparer Runly expects (case-insensitive paths).</summary>
    public static Dictionary<string, TrustedFileEntry> CreateFileDictionary() =>
        new(StringComparer.OrdinalIgnoreCase);
}
