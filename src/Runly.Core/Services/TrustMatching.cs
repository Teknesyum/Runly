using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Runly.Core.Models;

namespace Runly.Core.Services;

/// <summary>
/// Folder/file trust matching shared by <see cref="TrustStoreService"/> and <see cref="SecurityGate"/> so both
/// agree on the folder-prefix rule: a trusted <c>C:\A</c> covers <c>C:\A\B\x.js</c> but never <c>C:\AB\x.js</c>
/// (SPEC 5.2, SPEC 6). Folder matches are additionally verified against the reparse-point-resolved real path
/// (SPEC 11.1 K21) so a junction inside a trusted folder cannot smuggle in an outside target.
/// </summary>
internal static class TrustMatching
{
    private const uint FILE_READ_ATTRIBUTES = 0x0080;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint FILE_SHARE_DELETE = 0x00000004;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_NAME_NORMALIZED = 0x0;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    /// <summary>Resolves a path to its full, absolute form for comparison.</summary>
    public static string NormalizeFullPath(string path) => Path.GetFullPath(path);

    /// <summary>Resolves a folder path and strips any trailing directory separator.</summary>
    public static string NormalizeFolderPath(string folderPath) =>
        NormalizeFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Resolves reparse points (symlinks, junctions, mount points) along the whole path to the real,
    /// final target. Returns false if the path does not exist or cannot be opened.
    /// </summary>
    private static bool TryResolveRealPath(string normalizedPath, out string? resolvedPath)
    {
        using var handle = CreateFileW(
            normalizedPath,
            FILE_READ_ATTRIBUTES,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            resolvedPath = null;
            return false;
        }

        var buffer = new StringBuilder(4096);
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, FILE_NAME_NORMALIZED);
        if (length == 0 || length >= buffer.Capacity)
        {
            resolvedPath = null;
            return false;
        }

        var result = buffer.ToString(0, (int)length);
        if (result.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            result = result[4..];
        }

        resolvedPath = result;
        return true;
    }

    /// <summary>Whether the script path is exactly a trusted folder or lies under one, sub-folders included.</summary>
    public static bool IsWithinAnyTrustedFolder(string scriptPath, IEnumerable<string> trustedFolders)
    {
        var normalizedScript = NormalizeFullPath(scriptPath);
        var scriptResolveAttempted = false;
        string? resolvedScript = null;

        foreach (var folder in trustedFolders)
        {
            var normalizedFolder = NormalizeFolderPath(folder);
            var isExactMatch = normalizedScript.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase);
            var isPrefixMatch = normalizedScript.StartsWith(
                normalizedFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            if (!isExactMatch && !isPrefixMatch)
            {
                continue;
            }

            if (!scriptResolveAttempted)
            {
                scriptResolveAttempted = true;
                TryResolveRealPath(normalizedScript, out resolvedScript);
            }

            if (resolvedScript is null)
            {
                return false;
            }

            if (!TryResolveRealPath(normalizedFolder, out var resolvedFolder) || resolvedFolder is null)
            {
                continue;
            }

            var resolvedExactMatch = resolvedScript.Equals(resolvedFolder, StringComparison.OrdinalIgnoreCase);
            var resolvedPrefixMatch = resolvedScript.StartsWith(
                resolvedFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            if (resolvedExactMatch || resolvedPrefixMatch)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Looks up a trusted-file entry by full path, tolerating case and relative-path differences.</summary>
    public static bool TryGetTrustedFile(
        string scriptPath,
        IReadOnlyDictionary<string, TrustedFileEntry> trustedFiles,
        out TrustedFileEntry entry)
    {
        var normalizedScript = NormalizeFullPath(scriptPath);
        foreach (var pair in trustedFiles)
        {
            if (NormalizeFullPath(pair.Key).Equals(normalizedScript, StringComparison.OrdinalIgnoreCase))
            {
                entry = pair.Value;
                return true;
            }
        }

        entry = null!;
        return false;
    }
}
