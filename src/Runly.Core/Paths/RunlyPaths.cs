namespace Runly.Core.Paths;

/// <summary>Fixed locations of Runly's runtime data (SPEC 5, SPEC 8).</summary>
public static class RunlyPaths
{
    /// <summary>Folder name used under both roaming and local application data.</summary>
    public const string FolderName = "Runly";

    /// <summary><c>%APPDATA%\Runly</c> — roaming data: configuration, trust store, log, backups.</summary>
    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

    /// <summary><c>%LOCALAPPDATA%\Runly</c> — machine-local data that must not roam, such as the interpreter cache.</summary>
    public static string LocalAppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);

    /// <summary><c>%APPDATA%\Runly\config.json</c>.</summary>
    public static string ConfigPath { get; } = Path.Combine(AppDataDir, "config.json");

    /// <summary><c>%APPDATA%\Runly\trust.json</c>.</summary>
    public static string TrustPath { get; } = Path.Combine(AppDataDir, "trust.json");

    /// <summary><c>%APPDATA%\Runly\runly.log</c>.</summary>
    public static string LogPath { get; } = Path.Combine(AppDataDir, "runly.log");

    /// <summary><c>%APPDATA%\Runly\backups</c> — registry backups written before any association change.</summary>
    public static string BackupDir { get; } = Path.Combine(AppDataDir, "backups");

    /// <summary><c>%LOCALAPPDATA%\Runly\ipcache.json</c> — interpreter lookup cache (SPEC 8).</summary>
    public static string CachePath { get; } = Path.Combine(LocalAppDataDir, "ipcache.json");

    /// <summary>Creates every directory Runly writes into; safe to call repeatedly.</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(BackupDir);
        Directory.CreateDirectory(LocalAppDataDir);
    }
}
