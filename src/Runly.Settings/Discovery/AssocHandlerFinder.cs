using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Runly.Settings.Discovery;

/// <summary>One entry of the list Windows itself shows under "Open with".</summary>
/// <param name="DisplayName">What the shell calls the application, from <c>GetUIName</c>.</param>
/// <param name="Path">Resolved executable. Entries without one are dropped by the finder.</param>
/// <param name="IconLocation">Raw icon source from <c>GetIconLocation</c>: a path, a <c>"file,index"</c>
/// pair, or an <c>"@file,-id"</c> indirect string.</param>
/// <param name="IsRecommended">The shell's own recommendation flag for this extension.</param>
internal sealed record AssocHandlerApplication(
    string DisplayName,
    string Path,
    string IconLocation,
    bool IsRecommended);

/// <summary>Enumerates the handlers the shell associates with one extension. This runs beside
/// <see cref="ApplicationFinder"/>, not instead of it: the registry scan finds installed programs that
/// have never been associated with the extension, and this finds the ones the user actually sees in the
/// system dialog, including per-user choices the registry scan has no way to rank.</summary>
internal static class AssocHandlerFinder
{
    private const int SOk = 0;
    private const int AssocFilterNone = 0;

    /// <summary>Handlers for <paramref name="extension"/> (with the leading dot). Returns an empty list
    /// rather than throwing, so a COM failure leaves the caller on its existing source.</summary>
    public static IReadOnlyList<AssocHandlerApplication> Find(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension) || extension[0] != '.')
        {
            return [];
        }

        NativeMethods.IEnumAssocHandlers? enumerator = null;
        try
        {
            NativeMethods.SHAssocEnumHandlers(extension, AssocFilterNone, out enumerator);
            if (enumerator is null)
            {
                return [];
            }

            return Drain(enumerator);
        }
        catch (COMException)
        {
            return [];
        }
        catch (ArgumentException)
        {
            return [];
        }
        finally
        {
            if (enumerator is not null)
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
    }

    private static List<AssocHandlerApplication> Drain(NativeMethods.IEnumAssocHandlers enumerator)
    {
        var results = new List<AssocHandlerApplication>();
        var batch = new NativeMethods.IAssocHandler[1];

        while (enumerator.Next(1, batch, out var fetched) == SOk && fetched == 1)
        {
            var handler = batch[0];
            batch[0] = null!;
            try
            {
                var entry = Describe(handler);
                if (entry is not null)
                {
                    results.Add(entry);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(handler);
            }
        }

        return results;
    }

    private static AssocHandlerApplication? Describe(NativeMethods.IAssocHandler handler)
    {
        var path = ResolveExecutable(TakeString(handler.GetName));
        if (path is null || IsMarkedNoOpenWith(path))
        {
            return null;
        }

        var uiName = TakeString(handler.GetUIName);
        var displayName = string.IsNullOrWhiteSpace(uiName)
            ? Path.GetFileNameWithoutExtension(path)
            : uiName;

        var iconLocation = ReadIconLocation(handler) ?? path;
        return new AssocHandlerApplication(displayName, path, iconLocation, handler.IsRecommended() == SOk);
    }

    /// <summary><c>GetName</c> also returns package monikers for packaged applications, which are not
    /// something <c>ProcessLauncher</c> can start. Only a real executable on disk is kept.</summary>
    private static string? ResolveExecutable(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var candidate = Environment.ExpandEnvironmentVariables(name.Trim().Trim('"'));
        if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathFullyQualified(candidate))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(candidate);
            return File.Exists(full) ? full : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Whether the application asked to be kept out of "Open with".
    ///
    /// The flag lives under <c>Applications\&lt;file name&gt;</c>, and a file name is not an identity: the
    /// packaged Notepad ships as <c>Notepad.exe</c> under <c>WindowsApps</c> while the <c>NoOpenWith</c>
    /// flag on that key belongs to the <c>system32</c> one. So the registration only counts when it has no
    /// command of its own, or when its command points at the very executable being tested.</summary>
    private static bool IsMarkedNoOpenWith(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.Length == 0)
        {
            return false;
        }

        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(@"Applications\" + fileName, writable: false);
            if (key?.GetValue("NoOpenWith") is null)
            {
                return false;
            }

            using var command = key.OpenSubKey(@"shell\open\command", writable: false);
            var registered = ResolveExecutable(ExtractExecutable(command?.GetValue(null) as string));
            return registered is null || string.Equals(registered, path, StringComparison.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
    }

    private static string? ExtractExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        command = command.Trim();
        if (command[0] == '"')
        {
            var end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }

        var exeEnd = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeEnd >= 0 ? command[..(exeEnd + 4)] : null;
    }

    private static string? ReadIconLocation(NativeMethods.IAssocHandler handler)
    {
        var buffer = IntPtr.Zero;
        try
        {
            if (handler.GetIconLocation(out buffer, out var index) != SOk || buffer == IntPtr.Zero)
            {
                return null;
            }

            var path = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrWhiteSpace(path)
                ? null
                : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{path},{index}");
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(buffer);
            }
        }
    }

    private delegate int StringGetter(out IntPtr value);

    private static string? TakeString(StringGetter getter)
    {
        var buffer = IntPtr.Zero;
        try
        {
            if (getter(out buffer) != SOk || buffer == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(buffer);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(buffer);
            }
        }
    }

    private static class NativeMethods
    {
        [ComImport]
        [Guid("F04061AC-1659-4a3f-A954-775AA57FC083")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAssocHandler
        {
            [PreserveSig]
            int GetName(out IntPtr ppsz);

            [PreserveSig]
            int GetUIName(out IntPtr ppsz);

            [PreserveSig]
            int GetIconLocation(out IntPtr ppszPath, out int pIndex);

            [PreserveSig]
            int IsRecommended();

            [PreserveSig]
            int MakeDefault([MarshalAs(UnmanagedType.LPWStr)] string pszDescription);

            [PreserveSig]
            int Invoke(IntPtr pdo);

            [PreserveSig]
            int CreateInvoker(IntPtr pdo, out IntPtr ppInvoker);
        }

        [ComImport]
        [Guid("973810ae-9599-4b88-9e4d-6ee98c9552da")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IEnumAssocHandlers
        {
            [PreserveSig]
            int Next(
                int celt,
                [Out][MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)]
                IAssocHandler[] rgelt,
                out int pceltFetched);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void SHAssocEnumHandlers(
            [MarshalAs(UnmanagedType.LPWStr)] string pszExtra,
            int afFilter,
            [MarshalAs(UnmanagedType.Interface)] out IEnumAssocHandlers ppEnumHandler);
    }
}
