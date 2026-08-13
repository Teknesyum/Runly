using System.Text.Json.Serialization;

namespace Runly.Core.Models;

/// <summary>The remembered fingerprint of one individually trusted script (SPEC 5.2).</summary>
public sealed record TrustedFileEntry
{
    /// <summary>Lower-case hexadecimal SHA-256 of the file contents at the time it was trusted.</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>When the user granted the trust, in UTC.</summary>
    [JsonPropertyName("addedUtc")]
    public DateTimeOffset AddedUtc { get; init; }
}
