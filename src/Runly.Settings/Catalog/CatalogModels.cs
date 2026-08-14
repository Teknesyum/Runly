using System.Text.Json.Serialization;
using Runly.Core.Models;

namespace Runly.Settings.Catalog;

internal sealed record LocalizedName(
    [property: JsonPropertyName("tr")] string Tr,
    [property: JsonPropertyName("en")] string En);

internal sealed record CatalogEntry
{
    [JsonPropertyName("extension")]
    public string Extension { get; init; } = string.Empty;
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;
    [JsonPropertyName("displayName")]
    public LocalizedName DisplayName { get; init; } = new(string.Empty, string.Empty);
    [JsonPropertyName("defaultKind")]
    public HandlerKind DefaultKind { get; init; }
    [JsonPropertyName("suggestedApps")]
    public string[] SuggestedApps { get; init; } = [];
    [JsonPropertyName("blocked")]
    public bool Blocked { get; init; }
    [JsonPropertyName("riskNote")]
    public LocalizedName? RiskNote { get; init; }
}
