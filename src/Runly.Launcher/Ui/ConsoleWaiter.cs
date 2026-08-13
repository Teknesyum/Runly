using System.Globalization;
using Runly.Core.Models;

namespace Runly.Launcher.Ui;

/// <summary>Implements the <c>keepWindowOpen</c> rule after the child process exits (SPEC 7).</summary>
internal static class ConsoleWaiter
{
    /// <summary>Prints the exit line and waits for a key press when the mode calls for it.</summary>
    internal static void WaitIfNeeded(int exitCode, TimeSpan elapsed, KeepWindowMode mode)
    {
        if (!ShouldWait(exitCode, mode))
        {
            return;
        }

        // A redirected console has no window to keep open and no key to wait for.
        if (Console.IsOutputRedirected)
        {
            return;
        }

        var line = string.Format(
            CultureInfo.InvariantCulture,
            "--- Çıkış kodu: {0} ({1:F1} sn) — kapatmak için bir tuşa basın ---",
            exitCode,
            elapsed.TotalSeconds);

        // Teknesyum neon: console is limited to the 16 ConHost colours, so success/error map to
        // Cyan/Magenta (the closest matches to neon-blue/neon-pink) instead of the old green/red.
        WriteColoured(line, exitCode == ExitCode.Success ? ConsoleColor.Cyan : ConsoleColor.Magenta);

        if (Console.IsInputRedirected)
        {
            return;
        }

        try
        {
            Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
            // No console input handle (launched without one); nothing to wait for.
        }
    }

    private static bool ShouldWait(int exitCode, KeepWindowMode mode) => mode switch
    {
        KeepWindowMode.Always => true,
        KeepWindowMode.OnError => exitCode != ExitCode.Success,
        _ => false,
    };

    private static void WriteColoured(string line, ConsoleColor colour)
    {
        try
        {
            Console.ForegroundColor = colour;
            Console.WriteLine(line);
        }
        finally
        {
            Console.ResetColor();
        }
    }
}
