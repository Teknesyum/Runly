using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Runly.Core.Abstractions;

namespace Runly.Launcher.Ui;

/// <summary>Opens a script in the configured editor for the <c>edit</c> verb, without running it (SPEC 7).</summary>
[SupportedOSPlatform("windows")]
internal static class EditorLauncher
{
    /// <summary>Decision K7: an empty or unusable <c>editorCommand</c> falls back to Notepad, and this constant stays out of Core.</summary>
    internal const string FallbackEditor = "notepad.exe";

    /// <summary>Opens the script in the editor; returns <see langword="false"/> when no editor could be started.</summary>
    internal static bool Open(string editorCommand, string scriptPath, IPathSearcher pathSearcher, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(pathSearcher);
        ArgumentNullException.ThrowIfNull(logger);

        foreach (var candidate in GetCandidates(editorCommand))
        {
            var executable = TryFind(pathSearcher, candidate, logger);
            if (executable is null)
            {
                logger.Warn($"Editör bulunamadı: {candidate}");
                continue;
            }

            if (TryStart(executable, scriptPath, logger))
            {
                logger.Info($"Editörde açıldı: {executable} — {scriptPath}");
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetCandidates(string editorCommand)
    {
        if (!string.IsNullOrWhiteSpace(editorCommand))
        {
            yield return editorCommand.Trim();
        }

        yield return FallbackEditor;
    }

    private static string? TryFind(IPathSearcher pathSearcher, string candidate, ILogger logger)
    {
        try
        {
            return pathSearcher.Find(candidate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.Warn($"Editör aranırken hata: {candidate} — {ex.Message}");
            return null;
        }
    }

    private static bool TryStart(string executable, string scriptPath, ILogger logger)
    {
        // UseShellExecute is on because editor launchers are often batch shims (VS Code ships code.cmd),
        // and CreateProcess cannot start a .cmd directly.
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory,
        };

        try
        {
            // A null process is normal here: an already running editor may just take over the file.
            using var process = Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            logger.Warn($"Editör başlatılamadı: {executable} — {ex.Message}");
            return false;
        }
    }
}
