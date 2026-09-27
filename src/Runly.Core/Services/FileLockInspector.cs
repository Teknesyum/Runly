using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Runly.Core.Services;

/// <summary>One process that is holding a file open.</summary>
public sealed record FileLockHolder
{
    /// <summary>Process id reported by the Restart Manager.</summary>
    public required int ProcessId { get; init; }

    /// <summary>Executable or window name, as the user would recognise it.</summary>
    public required string Name { get; init; }

    /// <summary>Full path of the process image, when it could be read.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>When the process started; the Restart Manager uses it to make the id unambiguous.</summary>
    public DateTime? StartedAt { get; init; }
}

/// <summary>
/// Finds the processes that hold a file open, through the Restart Manager API — the same source File
/// Locksmith reads. Runly answers the question itself (K33) instead of sending the user to another tool,
/// so a locked script can be reported at the moment it fails rather than through a separate menu verb.
/// </summary>
[SupportedOSPlatform("windows")]
public static class FileLockInspector
{
    private const int RmRebootReasonNone = 0;
    private const int CchRmMaxAppName = 255;
    private const int CchRmMaxSvcName = 63;
    private const int ErrorSuccess = 0;
    private const int ErrorMoreData = 234;

    /// <summary>Lists the processes holding <paramref name="path"/>; empty when nothing holds it or the API fails.</summary>
    public static IReadOnlyList<FileLockHolder> Find(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var key = new StringBuilder(256);
        if (RmStartSession(out var session, 0, key) != ErrorSuccess)
        {
            return [];
        }

        try
        {
            if (RmRegisterResources(session, 1, [path], 0, null, 0, null) != ErrorSuccess)
            {
                return [];
            }

            uint needed = 0;
            uint count = 4;
            var info = new RmProcessInfo[count];
            uint reason = 0;

            var status = RmGetList(session, out needed, ref count, info, ref reason);
            if (status == ErrorMoreData)
            {
                count = needed;
                info = new RmProcessInfo[count == 0 ? 1 : count];
                status = RmGetList(session, out needed, ref count, info, ref reason);
            }

            if (status != ErrorSuccess)
            {
                return [];
            }

            var holders = new List<FileLockHolder>();
            for (var i = 0; i < count; i++)
            {
                holders.Add(Describe(info[i]));
            }

            return holders;
        }
        finally
        {
            RmEndSession(session);
        }
    }

    private static FileLockHolder Describe(RmProcessInfo info)
    {
        var id = (int)info.Process.ProcessId;
        string? executablePath = null;
        var name = info.AppName;

        try
        {
            using var process = Process.GetProcessById(id);
            executablePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = process.ProcessName;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // A process that died between RmGetList and here is simply reported by name alone.
        }

        return new FileLockHolder
        {
            ProcessId = id,
            Name = string.IsNullOrWhiteSpace(name) ? "(bilinmeyen süreç)" : name,
            ExecutablePath = executablePath,
            StartedAt = ToDateTime(info.Process.ProcessStartTime),
        };
    }

    private static DateTime? ToDateTime(FileTime time)
    {
        try
        {
            var ticks = ((long)time.High << 32) | (uint)time.Low;
            return ticks == 0 ? null : DateTime.FromFileTime(ticks);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Ends a process by id; returns whether it is gone afterwards.</summary>
    public static bool TryEnd(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill();
            process.WaitForExit(5000);
            return process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                      or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public int High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public uint ProcessId;
        public FileTime ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
        public string AppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
        public string ServiceShortName;

        public uint ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint sessionHandle, int flags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint sessionHandle,
        uint fileCount,
        string[] files,
        uint applicationCount,
        RmUniqueProcess[]? applications,
        uint serviceCount,
        string[]? serviceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint sessionHandle,
        out uint needed,
        ref uint count,
        [In, Out] RmProcessInfo[] processInfo,
        ref uint rebootReasons);
}
