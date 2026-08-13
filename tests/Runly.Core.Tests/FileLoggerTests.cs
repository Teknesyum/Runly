using System.Text.RegularExpressions;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>Covers the log line format, the disabled no-op path and single-file rotation past 1 MB.</summary>
public sealed partial class FileLoggerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _logPath;

    public FileLoggerTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("runly-logger-tests-").FullName;
        _logPath = Path.Combine(_tempDir, "runly.log");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z \[INFO\] merhaba$")]
    private static partial Regex InfoLinePattern();

    [Fact]
    public void Info_WritesLineInSpecFormat()
    {
        var logger = new FileLogger(enabled: true, logPath: _logPath);

        logger.Info("merhaba");

        var line = File.ReadAllText(_logPath).TrimEnd();
        Assert.Matches(InfoLinePattern(), line);
    }

    [Fact]
    public void Disabled_DoesNotCreateLogFile()
    {
        var logger = new FileLogger(enabled: false, logPath: _logPath);

        logger.Warn("should not be written");

        Assert.False(File.Exists(_logPath));
    }

    [Fact]
    public void Error_WithException_IncludesExceptionDetails()
    {
        var logger = new FileLogger(enabled: true, logPath: _logPath);

        logger.Error("hata oldu", new InvalidOperationException("boom"));

        var content = File.ReadAllText(_logPath);
        Assert.Contains("[ERROR] hata oldu", content, StringComparison.Ordinal);
        Assert.Contains("boom", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PastOneMegabyte_RotatesToDotOne()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllBytes(_logPath, new byte[1024 * 1024 + 1]);
        var logger = new FileLogger(enabled: true, logPath: _logPath);

        logger.Info("after rotation");

        var rotatedPath = _logPath + ".1";
        Assert.True(File.Exists(rotatedPath));
        Assert.True(new FileInfo(rotatedPath).Length >= 1024 * 1024 + 1);
        Assert.Contains("after rotation", File.ReadAllText(_logPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_DoesNotThrow_WhenLogFileNameHasInvalidCharacters()
    {
        // '|' is a reserved Windows filename character; AppendAllText throws IOException for it,
        // which SPEC 11 requires FileLogger to swallow rather than propagate.
        var invalidPath = Path.Combine(_tempDir, "inva|lid.log");
        var logger = new FileLogger(enabled: true, logPath: invalidPath);

        var exception = Record.Exception(() => logger.Info("still should not throw"));

        Assert.Null(exception);
    }
}
