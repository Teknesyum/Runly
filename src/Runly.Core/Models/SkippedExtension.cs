namespace Runly.Core.Models;

/// <summary>An extension that installation deliberately left alone, with the reason shown to the user.</summary>
public sealed record SkippedExtension
{
    /// <summary>Extension including the leading dot.</summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>Turkish, user-visible explanation of why the extension was skipped.</summary>
    public string Reason { get; init; } = string.Empty;
}
