using System.Runtime.InteropServices;
using System.Text;

namespace Runly.Settings.Discovery;

internal static class ShortcutTargetReader
{
    public static string? TryRead(string shortcutPath)
    {
        try
        {
            var link = (IShellLinkW)Activator.CreateInstance(typeof(ShellLink))!;
            ((IPersistFile)link).Load(shortcutPath, 0);
            var path = new StringBuilder(32768);
            link.GetPath(path, path.Capacity, IntPtr.Zero, 0);
            return path.Length == 0 ? null : path.ToString();
        }
        catch (COMException) { return null; }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maximum, IntPtr findData, uint flags);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
    }
}
