using System.Security.Cryptography;
using System.Text;
using Runly.Core.Abstractions;
using Runly.Core.Models;

namespace Runly.Core.Services;

/// <summary>
/// Gathers size, timestamp, streaming SHA-256, mark-of-the-web and the leading lines/shebang of a script file
/// (SPEC 6, SPEC 8). Never consults PATH itself — decision K4 leaves the shebang→PATH fallback chain to
/// <see cref="InterpreterResolver"/> so this class stays IO-minimal and easy to unit test.
/// </summary>
public sealed class ScriptInspector : IScriptInspector
{
    /// <summary>Files larger than this are not hashed; hashing a huge file on every launch would be too slow (SPEC 5.1).</summary>
    public const long HashSizeLimitBytes = 100L * 1024 * 1024;

    private const int HeaderReadLimitBytes = 64 * 1024;

    private readonly IMotwService _motwService;

    /// <summary>Creates an inspector that reads mark-of-the-web state through the given service.</summary>
    public ScriptInspector(IMotwService motwService)
    {
        ArgumentNullException.ThrowIfNull(motwService);
        _motwService = motwService;
    }

    /// <inheritdoc />
    public ScriptInfo Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Script bulunamadı.", path);
        }

        var (firstLines, shebangRaw, shebangInterpreter) = ReadHeader(fileInfo);
        var sha256 = fileInfo.Length > HashSizeLimitBytes ? null : ComputeSha256(fileInfo.FullName);

        return new ScriptInfo
        {
            Path = fileInfo.FullName,
            Extension = fileInfo.Extension.ToLowerInvariant(),
            SizeBytes = fileInfo.Length,
            ModifiedUtc = fileInfo.LastWriteTimeUtc,
            Sha256 = sha256,
            HasMotw = _motwService.HasMotw(fileInfo.FullName),
            ZoneId = _motwService.GetZoneId(fileInfo.FullName),
            FirstLines = firstLines,
            Shebang = shebangRaw,
            ShebangInterpreter = shebangInterpreter,
        };
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static (IReadOnlyList<string> Lines, string? ShebangRaw, string? ShebangInterpreter) ReadHeader(FileInfo fileInfo)
    {
        var bytesToRead = (int)Math.Min(HeaderReadLimitBytes, fileInfo.Length);
        var buffer = new byte[bytesToRead];

        using (var stream = fileInfo.OpenRead())
        {
            var totalRead = 0;
            int read;
            while (totalRead < bytesToRead && (read = stream.Read(buffer, totalRead, bytesToRead - totalRead)) > 0)
            {
                totalRead += read;
            }

            if (totalRead != buffer.Length)
            {
                Array.Resize(ref buffer, totalRead);
            }
        }

        var span = buffer.AsSpan();
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            span = span[3..];
        }

        var text = Encoding.UTF8.GetString(span);

        var lines = new List<string>();
        using var reader = new StringReader(text);
        string? line;
        while (lines.Count < ScriptInfo.MaxFirstLines && (line = reader.ReadLine()) is not null)
        {
            lines.Add(line);
        }

        string? shebangRaw = null;
        string? shebangInterpreter = null;
        if (lines.Count > 0 && lines[0].StartsWith("#!", StringComparison.Ordinal))
        {
            shebangRaw = lines[0][2..].Trim();
            shebangInterpreter = ParseShebangInterpreter(shebangRaw);
        }

        return (lines, shebangRaw, shebangInterpreter);
    }

    private static string? ParseShebangInterpreter(string shebangRaw)
    {
        if (string.IsNullOrWhiteSpace(shebangRaw))
        {
            return null;
        }

        var tokens = shebangRaw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return null;
        }

        // Windows path separators are tolerated here even though a real shebang uses '/' (T2.md).
        var interpreterName = GetLastSegment(tokens[0]);

        // #!/usr/bin/env node style: the real interpreter is the next token.
        if (string.Equals(interpreterName, "env", StringComparison.OrdinalIgnoreCase) && tokens.Length > 1)
        {
            // `env -S python -u` and `env --split-string python -u` are common portable shebangs.
            // Skip env's own options and pick the first command token.
            var commandIndex = 1;
            while (commandIndex < tokens.Length && tokens[commandIndex].StartsWith('-'))
            {
                commandIndex++;
            }

            interpreterName = commandIndex < tokens.Length ? GetLastSegment(tokens[commandIndex]) : string.Empty;
        }

        return string.IsNullOrWhiteSpace(interpreterName) ? null : interpreterName;
    }

    private static string GetLastSegment(string token) =>
        Path.GetFileName(token.Replace('\\', '/'));
}
