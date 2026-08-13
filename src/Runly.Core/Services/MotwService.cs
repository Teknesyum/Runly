using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Runly.Core.Abstractions;

namespace Runly.Core.Services;

/// <summary>Reads and strips the <c>:Zone.Identifier</c> alternate data stream that marks downloaded files (SPEC 6).</summary>
[SupportedOSPlatform("windows")]
public sealed class MotwService : IMotwService
{
    private const int TrustedZoneThreshold = 3;

    private readonly ILogger? _logger;

    /// <summary>Creates the service, optionally logging read/strip failures on file systems without ADS support.</summary>
    public MotwService(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool HasMotw(string path) => GetZoneId(path) is >= TrustedZoneThreshold;

    /// <inheritdoc />
    public int? GetZoneId(string path)
    {
        var adsPath = GetZoneIdentifierPath(path);
        try
        {
            if (!File.Exists(adsPath))
            {
                return null;
            }

            foreach (var line in File.ReadLines(adsPath))
            {
                if (line.StartsWith("ZoneId=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(line.AsSpan("ZoneId=".Length), out var zoneId))
                {
                    return zoneId;
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // FAT32 volumes and some network shares do not support alternate data streams at all;
            // SPEC 6 requires this to read as "no MOTW" rather than fail the launch.
            _logger?.Info($"MOTW okunamadı (ADS desteklenmiyor olabilir): {path} — {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public void Strip(string path)
    {
        var adsPath = GetZoneIdentifierPath(path);
        if (!File.Exists(adsPath))
        {
            return;
        }

        if (!NativeMethods.DeleteFile(adsPath))
        {
            var errorCode = Marshal.GetLastWin32Error();
            _logger?.Warn($"MOTW kaldırılamadı: {path} (Win32 hata kodu {errorCode})");
        }
    }

    private static string GetZoneIdentifierPath(string path) => path + ":Zone.Identifier";

    [SupportedOSPlatform("windows")]
    private static class NativeMethods
    {
        // DllImport (not LibraryImport) here: the bool-return/string-parameter signature marshals natively
        // under NativeAOT without needing <AllowUnsafeBlocks> on the whole assembly (T2.md AOT requirement).
        [DllImport("kernel32.dll", EntryPoint = "DeleteFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string fileName);
    }
}
