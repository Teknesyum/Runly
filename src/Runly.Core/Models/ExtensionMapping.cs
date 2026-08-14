using System.Text.Json.Serialization;

namespace Runly.Core.Models;

/// <summary>How one file extension is turned into an interpreter invocation (SPEC 5.1).</summary>
public sealed record ExtensionMapping
{
    /// <summary>Whether the file is executed or opened in an application.</summary>
    [JsonPropertyName("kind")]
    public HandlerKind Kind { get; init; } = HandlerKind.Run;

    /// <summary>Catalog category used by the settings application.</summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = "Betikler";

    /// <summary>Interpreter name to look up on PATH, or a full path to an executable.</summary>
    [JsonPropertyName("interpreter")]
    public string Interpreter { get; init; } = string.Empty;

    /// <summary>Absolute executable path used by an <see cref="HandlerKind.Open"/> mapping.</summary>
    [JsonPropertyName("openWith")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OpenWith { get; init; }

    /// <summary>Argument template; supports the <c>{script}</c>, <c>{args}</c> and <c>{dir}</c> placeholders.</summary>
    [JsonPropertyName("args")]
    public string Args { get; init; } = string.Empty;

    /// <summary>Whether this extension takes part in installation and launching.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    /// <summary>Icon file name under the install folder's <c>assets</c> directory, or <see langword="null"/> for none.</summary>
    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; init; }
}
