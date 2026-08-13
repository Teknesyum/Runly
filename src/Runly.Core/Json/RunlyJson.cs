using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Runly.Core.Models;

namespace Runly.Core.Json;

/// <summary>The single AOT-safe entry point for reading and writing Runly's JSON files.</summary>
public static class RunlyJson
{
    private static readonly RunlyJsonContext Context = new(CreateOptions());

    /// <summary>Serializer options: schema property names verbatim, indented output, comments and trailing commas tolerated.</summary>
    public static JsonSerializerOptions Options => Context.Options;

    /// <summary>Type metadata for <see cref="RunlyConfig"/>.</summary>
    public static JsonTypeInfo<RunlyConfig> Config => Context.RunlyConfig;

    /// <summary>Type metadata for <see cref="Models.TrustStore"/>.</summary>
    public static JsonTypeInfo<TrustStore> TrustStore => Context.TrustStore;

    private static JsonSerializerOptions CreateOptions() => new()
    {
        // Property names come from [JsonPropertyName] and must match SPEC 5.1 / 5.2 character for character,
        // so no naming policy is applied.
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
