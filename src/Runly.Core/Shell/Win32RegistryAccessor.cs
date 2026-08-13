using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Runly.Core.Shell;

/// <summary>
/// The real registry, reached through <c>advapi32</c> P/Invoke. SPEC 3 forbids extra NuGet packages, so the
/// Win32 calls are written by hand; this also keeps <c>Runly.Core</c> on the windows agnostic <c>net8.0</c> TFM.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32RegistryAccessor : IRegistryAccessor
{
    private static readonly IntPtr HkeyCurrentUser = new(unchecked((int)0x80000001));
    private static readonly IntPtr HkeyClassesRoot = new(unchecked((int)0x80000000));

    private const int ErrorSuccess = 0;
    private const int ErrorFileNotFound = 2;
    private const int ErrorNoMoreItems = 259;
    private const int ErrorMoreData = 234;

    private const int KeyRead = 0x20019;
    private const int KeyWrite = 0x20006;

    /// <inheritdoc />
    public bool KeyExists(RegistryRoot root, string subKey)
    {
        var handle = Open(root, subKey, KeyRead);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        _ = RegCloseKey(handle);
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string subKey)
    {
        var handle = Open(root, subKey, KeyRead);
        if (handle == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var names = new List<string>();
            var buffer = new char[256];

            for (var index = 0; ; index++)
            {
                var length = buffer.Length;
                var status = RegEnumKeyExW(handle, index, buffer, ref length,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                if (status == ErrorNoMoreItems)
                {
                    break;
                }

                if (status == ErrorMoreData)
                {
                    buffer = new char[buffer.Length * 2];
                    index--;
                    continue;
                }

                if (status != ErrorSuccess)
                {
                    throw new Win32Exception(status);
                }

                names.Add(new string(buffer, 0, length));
            }

            return names;
        }
        finally
        {
            _ = RegCloseKey(handle);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RegistryValueEntry> GetValues(RegistryRoot root, string subKey)
    {
        var handle = Open(root, subKey, KeyRead);
        if (handle == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var names = new List<string>();
            var buffer = new char[16384];

            for (var index = 0; ; index++)
            {
                var length = buffer.Length;
                var status = RegEnumValueW(handle, index, buffer, ref length,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                if (status == ErrorNoMoreItems)
                {
                    break;
                }

                if (status == ErrorMoreData)
                {
                    buffer = new char[buffer.Length * 2];
                    index--;
                    continue;
                }

                if (status != ErrorSuccess)
                {
                    throw new Win32Exception(status);
                }

                names.Add(new string(buffer, 0, length));
            }

            var values = new List<RegistryValueEntry>(names.Count);
            foreach (var name in names)
            {
                var entry = ReadValue(handle, name);
                if (entry is not null)
                {
                    values.Add(entry);
                }
            }

            return values;
        }
        finally
        {
            _ = RegCloseKey(handle);
        }
    }

    /// <inheritdoc />
    public RegistryValueEntry? GetValue(RegistryRoot root, string subKey, string valueName)
    {
        ArgumentNullException.ThrowIfNull(valueName);

        var handle = Open(root, subKey, KeyRead);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return ReadValue(handle, valueName);
        }
        finally
        {
            _ = RegCloseKey(handle);
        }
    }

    /// <inheritdoc />
    public void CreateKey(RegistryRoot root, string subKey)
    {
        RequireWritable(root);
        var handle = Create(subKey);
        _ = RegCloseKey(handle);
    }

    /// <inheritdoc />
    public void SetValue(RegistryRoot root, string subKey, RegistryValueEntry value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireWritable(root);

        var handle = Create(subKey);
        try
        {
            var name = value.Name.Length == 0 ? null : value.Name;
            var status = RegSetValueExW(handle, name, 0, (int)value.Kind, value.Data, value.Data.Length);
            if (status != ErrorSuccess)
            {
                throw new Win32Exception(status);
            }
        }
        finally
        {
            _ = RegCloseKey(handle);
        }
    }

    /// <inheritdoc />
    public void DeleteValue(RegistryRoot root, string subKey, string valueName)
    {
        ArgumentNullException.ThrowIfNull(valueName);
        RequireWritable(root);

        var handle = Open(root, subKey, KeyWrite);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var status = RegDeleteValueW(handle, valueName.Length == 0 ? null : valueName);
            if (status is not (ErrorSuccess or ErrorFileNotFound))
            {
                throw new Win32Exception(status);
            }
        }
        finally
        {
            _ = RegCloseKey(handle);
        }
    }

    /// <inheritdoc />
    public void DeleteKeyTree(RegistryRoot root, string subKey)
    {
        RequireWritable(root);

        var path = RegFileWriter.NormalizeSubKey(subKey);
        if (path.Length == 0)
        {
            throw new ArgumentException("Kök anahtarın tamamı silinemez.", nameof(subKey));
        }

        var status = RegDeleteTreeW(HkeyCurrentUser, path);
        if (status is not (ErrorSuccess or ErrorFileNotFound))
        {
            throw new Win32Exception(status);
        }

        // RegDeleteTree empties the key; on some Windows builds the key itself survives, so remove it too.
        status = RegDeleteKeyExW(HkeyCurrentUser, path, 0, 0);
        if (status is not (ErrorSuccess or ErrorFileNotFound))
        {
            throw new Win32Exception(status);
        }
    }

    private static void RequireWritable(RegistryRoot root)
    {
        if (root != RegistryRoot.CurrentUser)
        {
            throw new InvalidOperationException(
                "Runly yalnızca HKEY_CURRENT_USER altına yazar (SPEC 9).");
        }
    }

    private static IntPtr RootHandle(RegistryRoot root) =>
        root == RegistryRoot.CurrentUser ? HkeyCurrentUser : HkeyClassesRoot;

    private static IntPtr Open(RegistryRoot root, string subKey, int access)
    {
        ArgumentNullException.ThrowIfNull(subKey);

        var path = RegFileWriter.NormalizeSubKey(subKey);
        var status = RegOpenKeyExW(RootHandle(root), path.Length == 0 ? null : path, 0, access, out var handle);

        if (status == ErrorSuccess)
        {
            return handle;
        }

        if (status is ErrorFileNotFound or 3 /* ERROR_PATH_NOT_FOUND */)
        {
            return IntPtr.Zero;
        }

        throw new Win32Exception(status);
    }

    private static IntPtr Create(string subKey)
    {
        var path = RegFileWriter.NormalizeSubKey(subKey);
        if (path.Length == 0)
        {
            throw new ArgumentException("Anahtar yolu boş olamaz.", nameof(subKey));
        }

        var status = RegCreateKeyExW(HkeyCurrentUser, path, 0, null, 0, KeyRead | KeyWrite,
            IntPtr.Zero, out var handle, out _);

        if (status != ErrorSuccess)
        {
            throw new Win32Exception(status);
        }

        return handle;
    }

    private static RegistryValueEntry? ReadValue(IntPtr handle, string valueName)
    {
        var name = valueName.Length == 0 ? null : valueName;
        var size = 0;

        var status = RegQueryValueExW(handle, name, IntPtr.Zero, out var type, null, ref size);
        if (status == ErrorFileNotFound)
        {
            return null;
        }

        if (status is not (ErrorSuccess or ErrorMoreData))
        {
            throw new Win32Exception(status);
        }

        var data = new byte[size];
        if (size > 0)
        {
            status = RegQueryValueExW(handle, name, IntPtr.Zero, out type, data, ref size);
            if (status != ErrorSuccess)
            {
                throw new Win32Exception(status);
            }
        }

        return new RegistryValueEntry
        {
            Name = valueName,
            Kind = (RegistryValueKind)type,
            Data = size == data.Length ? data : data[..size],
        };
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegOpenKeyExW(IntPtr hKey, string? subKey, int options, int samDesired, out IntPtr result);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegCreateKeyExW(IntPtr hKey, string subKey, int reserved, string? className,
        int options, int samDesired, IntPtr securityAttributes, out IntPtr result, out int disposition);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern int RegCloseKey(IntPtr hKey);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegQueryValueExW(IntPtr hKey, string? valueName, IntPtr reserved,
        out int type, byte[]? data, ref int dataSize);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegSetValueExW(IntPtr hKey, string? valueName, int reserved,
        int type, byte[] data, int dataSize);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegDeleteValueW(IntPtr hKey, string? valueName);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegDeleteKeyExW(IntPtr hKey, string subKey, int samDesired, int reserved);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegDeleteTreeW(IntPtr hKey, string? subKey);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegEnumKeyExW(IntPtr hKey, int index, char[] name, ref int nameLength,
        IntPtr reserved, IntPtr className, IntPtr classLength, IntPtr lastWriteTime);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegEnumValueW(IntPtr hKey, int index, char[] name, ref int nameLength,
        IntPtr reserved, IntPtr type, IntPtr data, IntPtr dataLength);
}
