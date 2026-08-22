using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Runly.Core.Services;

/// <summary>
/// What an <c>IO_REPARSE_TAG_APPEXECLINK</c> reparse point points at, i.e. what a zero-byte
/// <c>%LOCALAPPDATA%\Microsoft\WindowsApps</c> entry really is (SPEC 8, decision K9).
/// </summary>
internal sealed record AppExecutionAlias
{
    /// <summary>The version field the reparse payload starts with; Windows 10 and 11 write 3.</summary>
    public required uint Version { get; init; }

    /// <summary>Owning package family, e.g. <c>PythonSoftwareFoundation.PythonManager_3847v3x7pw1km</c>.</summary>
    public required string PackageFamilyName { get; init; }

    /// <summary>The alias' application user model id, i.e. <c>&lt;family&gt;!&lt;application&gt;</c>.</summary>
    public required string ApplicationUserModelId { get; init; }

    /// <summary>The executable the alias actually launches.</summary>
    public required string TargetPath { get; init; }

    /// <summary>The trailing application-type field; every alias measured on Windows 11 carries <c>0</c>.</summary>
    public required string ApplicationType { get; init; }

    /// <summary>
    /// Whether the alias is a dead Store install stub instead of an installed application. Such a stub targets
    /// one of App Installer's <c>*Redirector.exe</c> shims — <c>python.exe</c> gets
    /// <c>AppInstallerPythonRedirector.exe</c> — which only opens a Store page and never runs a script.
    /// The owning package is deliberately not part of the test: <c>winget.exe</c> is a working alias and it
    /// belongs to that very same App Installer package.
    /// </summary>
    public bool IsStoreRedirector =>
        Path.GetFileName(TargetPath.AsSpan()).EndsWith("Redirector.exe", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Reads the <c>APPEXECLINK</c> reparse point behind an app-execution alias. File size cannot separate a
/// working alias from a dead Store stub — both are zero-byte reparse points — so the reparse tag and the
/// resolved target are the only reliable signal (SPEC 8, decision K9; the same route <c>uv</c> and CPython's
/// <c>PC/launcher2.c</c> take).
/// </summary>
internal static class AppExecutionAliasReader
{
    private const uint IoReparseTagAppExecLink = 0x8000001B;

    private const uint FILE_READ_ATTRIBUTES = 0x0080;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint FILE_SHARE_DELETE = 0x00000004;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FSCTL_GET_REPARSE_POINT = 0x000900A8;
    private const int MAXIMUM_REPARSE_DATA_BUFFER_SIZE = 16 * 1024;

    private const int ReparseTagLength = 4;
    private const int ReparseHeaderLength = 8;
    private const int VersionLength = 4;
    private const int PackageFamilyNameIndex = 0;
    private const int ApplicationUserModelIdIndex = 1;
    private const int TargetPathIndex = 2;
    private const int ApplicationTypeIndex = 3;
    private const int ExpectedStringCount = 4;

    /// <summary>
    /// Reads and parses the alias at <paramref name="path"/>. False means "not a readable app-execution
    /// alias", which is the caller's cue to fall back to the old size-only rule rather than to guess.
    /// </summary>
    public static bool TryRead(string path, out AppExecutionAlias? alias)
    {
        alias = null;
        return TryReadReparseData(path, out var reparseData)
            && reparseData is not null
            && TryParse(reparseData, out alias);
    }

    /// <summary>
    /// Copies the raw <c>REPARSE_DATA_BUFFER</c> of <paramref name="path"/>, tag header included. False when
    /// the path is not a reparse point, cannot be opened, or sits on a file system without reparse support.
    /// </summary>
    public static bool TryReadReparseData(string path, out byte[]? reparseData)
    {
        reparseData = null;

        // FILE_FLAG_OPEN_REPARSE_POINT keeps the handle on the alias itself; without it Windows follows the
        // link and the tag is never visible. FILE_FLAG_BACKUP_SEMANTICS is what allows the control code when
        // the candidate turns out to be a directory rather than a file.
        using var handle = CreateFileW(
            path,
            FILE_READ_ATTRIBUTES,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return false;
        }

        var buffer = new byte[MAXIMUM_REPARSE_DATA_BUFFER_SIZE];
        var read = DeviceIoControl(
            handle,
            FSCTL_GET_REPARSE_POINT,
            IntPtr.Zero,
            0,
            ref buffer[0],
            buffer.Length,
            out var bytesReturned,
            IntPtr.Zero);

        if (!read || bytesReturned <= 0 || bytesReturned > buffer.Length)
        {
            return false;
        }

        reparseData = buffer[..bytesReturned];
        return true;
    }

    /// <summary>
    /// Parses a raw <c>REPARSE_DATA_BUFFER</c> without touching the file system, so the layout stays testable
    /// on machines that have no Store alias to point at.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> reparseData, out AppExecutionAlias? alias)
    {
        alias = null;

        if (reparseData.Length < ReparseHeaderLength + VersionLength)
        {
            return false;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(reparseData) != IoReparseTagAppExecLink)
        {
            return false;
        }

        var declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(reparseData[ReparseTagLength..]);
        var payload = reparseData[ReparseHeaderLength..];
        if (declaredLength > payload.Length)
        {
            return false;
        }

        payload = payload[..declaredLength];
        if (payload.Length < VersionLength)
        {
            return false;
        }

        // Only the strings up to and including the target are required: the trailing application-type field
        // is what a future version would most plausibly drop, and it carries nothing the search needs.
        var strings = SplitNulTerminatedStrings(payload[VersionLength..]);
        if (strings.Count <= TargetPathIndex || strings[TargetPathIndex].Length == 0)
        {
            return false;
        }

        alias = new AppExecutionAlias
        {
            Version = BinaryPrimitives.ReadUInt32LittleEndian(payload),
            PackageFamilyName = strings[PackageFamilyNameIndex],
            ApplicationUserModelId = strings[ApplicationUserModelIdIndex],
            TargetPath = strings[TargetPathIndex],
            ApplicationType = strings.Count > ApplicationTypeIndex ? strings[ApplicationTypeIndex] : string.Empty,
        };

        return true;
    }

    private static List<string> SplitNulTerminatedStrings(ReadOnlySpan<byte> payload)
    {
        var characters = MemoryMarshal.Cast<byte, char>(payload[..(payload.Length - (payload.Length % 2))]);
        var strings = new List<string>(ExpectedStringCount);
        var start = 0;

        for (var index = 0; index < characters.Length; index++)
        {
            if (characters[index] != '\0')
            {
                continue;
            }

            strings.Add(new string(characters[start..index]));
            start = index + 1;
        }

        if (start < characters.Length)
        {
            strings.Add(new string(characters[start..]));
        }

        return strings;
    }

    // DllImport rather than LibraryImport: the generated stubs need <AllowUnsafeBlocks>, which Runly.Core
    // does not turn on. These signatures are blittable plus a UTF-16 string and a SafeHandle, all of which
    // NativeAOT marshals ahead of time, so the launcher's AOT publish is unaffected.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        ref byte lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);
}
