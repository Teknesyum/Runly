using System.Runtime.InteropServices;
using System.Text;

namespace Runly.Settings.Discovery;

internal readonly record struct ShortcutTarget(string Path, string Arguments);

internal static class ShortcutTargetReader
{
    public static ShortcutTarget? TryRead(string shortcutPath)
    {
        try
        {
            var link = (IShellLinkW)Activator.CreateInstance(typeof(ShellLink))!;
            ((IPersistFile)link).Load(shortcutPath, 0);
            var path = new StringBuilder(32768);
            link.GetPath(path, path.Capacity, IntPtr.Zero, 0);
            if (path.Length == 0)
            {
                return null;
            }

            var arguments = new StringBuilder(32768);
            link.GetArguments(arguments, arguments.Capacity);
            return new ShortcutTarget(path.ToString(), arguments.ToString());
        }
        catch (COMException) { return null; }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        // GetArguments is the eighth entry of the vtable, so every slot before it has to be declared even
        // though it is never called. Removing one silently shifts the call onto the wrong function.
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maximum, IntPtr findData, uint flags);
        void GetIdList(out IntPtr idList);
        void SetIdList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maximum);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maximum);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maximum);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
    }
}
