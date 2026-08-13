namespace Runly.Core.Models;

/// <summary>Outcome of a shell installation run, detailed enough to drive the result dialog (SPEC 9, SPEC 10).</summary>
public sealed record InstallResult
{
    /// <summary>Whether every intended registry write succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Full path of the registry backup written before any change, or <see langword="null"/> if none was taken.</summary>
    public string? BackupPath { get; init; }

    /// <summary>Per-extension binding state after installation.</summary>
    public IReadOnlyList<ExtensionStatus> Extensions { get; init; } = [];

    /// <summary>Extensions that were skipped, with reasons.</summary>
    public IReadOnlyList<SkippedExtension> Skipped { get; init; } = [];

    /// <summary>Turkish, user-visible log of what was done, one line per action.</summary>
    public IReadOnlyList<string> Actions { get; init; } = [];

    /// <summary>Turkish error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }
}
