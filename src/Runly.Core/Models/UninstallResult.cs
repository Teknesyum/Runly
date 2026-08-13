namespace Runly.Core.Models;

/// <summary>Outcome of removing Runly from the shell (SPEC 9, "Kaldırma").</summary>
public sealed record UninstallResult
{
    /// <summary>Whether every intended registry removal succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Registry key paths that were deleted.</summary>
    public IReadOnlyList<string> RemovedKeys { get; init; } = [];

    /// <summary>Backup file that was replayed, or <see langword="null"/> when no restore was requested.</summary>
    public string? RestoredBackupPath { get; init; }

    /// <summary>Turkish, user-visible log of what was done, one line per action.</summary>
    public IReadOnlyList<string> Actions { get; init; } = [];

    /// <summary>
    /// Extensions whose Windows <c>UserChoice</c> key pointed at a Runly ProgID at removal time (decision K20).
    /// Entries with <see cref="OrphanedUserChoice.Removed"/> false are still dangling and must be shown.
    /// </summary>
    public IReadOnlyList<OrphanedUserChoice> AffectedUserChoices { get; init; } = [];

    /// <summary>Whether any extension is left pointing at a ProgID that no longer exists.</summary>
    public bool HasOrphanedUserChoices => AffectedUserChoices.Any(o => !o.Removed);

    /// <summary>Turkish error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }
}
