using System.Diagnostics;
using Runly.Core.Abstractions;
using Runly.Core.Models;

namespace Runly.Launcher;

/// <summary>Runs the resolved interpreter and hands the child's exit code back untouched (SPEC 7).</summary>
internal static class ScriptRunner
{
    /// <summary>
    /// Starts the interpreter, then runs <paramref name="wait"/> — the "keep the window open" step — and
    /// only afterwards returns. The child's code is captured before waiting and returned verbatim, and a
    /// failing wait cannot replace it: gsudo#421 is exactly this, a wrapper that lost the child's exit
    /// code while holding its own console open.
    /// </summary>
    internal static int RunAndWait(
        IProcessLauncher launcher,
        ResolvedInterpreter interpreter,
        string workingDirectory,
        bool elevated,
        Action<int, TimeSpan> wait,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentNullException.ThrowIfNull(logger);

        var stopwatch = Stopwatch.StartNew();
        var childExitCode = launcher.Launch(interpreter, workingDirectory, elevated);
        stopwatch.Stop();

        logger.Info($"Bitti: çıkış kodu {childExitCode}, süre {stopwatch.Elapsed.TotalSeconds:F1} sn");

        try
        {
            wait(childExitCode, stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or PlatformNotSupportedException)
        {
            logger.Warn($"Pencere açık tutulamadı: {ex.Message}");
        }

        return childExitCode;
    }
}
