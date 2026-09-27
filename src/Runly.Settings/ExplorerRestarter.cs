using System.ComponentModel;
using System.Diagnostics;

namespace Runly.Settings;

/// <summary>Restarts this session's Explorer so it re-reads the blocked shell-extension list.</summary>
internal static class ExplorerRestarter
{
    public static void Restart()
    {
        var session = Process.GetCurrentProcess().SessionId;

        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            using (process)
            {
                if (process.SessionId != session)
                {
                    continue;
                }

                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                }
            }
        }

        for (var attempt = 0; attempt < 12; attempt++)
        {
            Thread.Sleep(250);
            if (IsRunning(session))
            {
                return;
            }
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true })?.Dispose();
    }

    private static bool IsRunning(int session)
    {
        var running = false;
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            using (process)
            {
                running |= process.SessionId == session;
            }
        }

        return running;
    }
}
