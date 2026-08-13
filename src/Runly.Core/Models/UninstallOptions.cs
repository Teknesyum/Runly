namespace Runly.Core.Models;

/// <summary>Choices offered when Runly is removed from the shell (SPEC 9, "Kaldırma").</summary>
public sealed record UninstallOptions
{
    /// <summary>The default: remove Runly's keys and leave the previous associations empty.</summary>
    public static UninstallOptions Default { get; } = new();

    /// <summary>
    /// Whether to replay a registry backup after removal, restoring the pre-install associations.
    /// Off by default because the previous <c>.js</c> default was <c>WScript.exe</c>.
    /// </summary>
    public bool RestoreBackup { get; init; }

    /// <summary>Backup file to replay; when <see langword="null"/> the most recent backup is used.</summary>
    public string? BackupPath { get; init; }
}
