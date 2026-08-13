using System.Globalization;
using Runly.Core.Abstractions;
using Runly.Core.Paths;

namespace Runly.Core.Services;

/// <summary>Appends to <c>runly.log</c> with single-file rotation past 1 MB; logging never throws (SPEC 11).</summary>
public sealed class FileLogger : ILogger
{
    private const long MaxSizeBytes = 1024 * 1024;
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    private readonly object _syncRoot = new();
    private readonly bool _enabled;
    private readonly string _logPath;

    /// <summary>Creates a logger at the default <c>%APPDATA%\Runly\runly.log</c> location, or a custom path for tests.</summary>
    public FileLogger(bool enabled = true, string? logPath = null)
    {
        _enabled = enabled;
        _logPath = logPath ?? RunlyPaths.LogPath;
    }

    /// <inheritdoc />
    public void Info(string message) => Write("INFO", message);

    /// <inheritdoc />
    public void Warn(string message) => Write("WARN", message);

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} — {exception}");

    private void Write(string level, string message)
    {
        if (!_enabled)
        {
            return;
        }

        try
        {
            lock (_syncRoot)
            {
                WriteLocked(level, message);
            }
        }
        catch (Exception ex) when (IsRecoverableIoFailure(ex))
        {
            // SPEC 11: a logging failure must never crash the application, so it is swallowed here by design.
        }
    }

    private void WriteLocked(string level, string message)
    {
        var directory = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        RotateIfNeeded();

        var timestamp = DateTime.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        File.AppendAllText(_logPath, $"{timestamp} [{level}] {message}{Environment.NewLine}");
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_logPath) || new FileInfo(_logPath).Length < MaxSizeBytes)
        {
            return;
        }

        File.Move(_logPath, _logPath + ".1", overwrite: true);
    }

    private static bool IsRecoverableIoFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException;
}
