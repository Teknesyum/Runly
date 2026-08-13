namespace Runly.Core.Models;

/// <summary>Everything the security gate and the interpreter resolver need to know about one script file.</summary>
public sealed record ScriptInfo
{
    /// <summary>Number of leading lines captured in <see cref="FirstLines"/> for the "show code" pane (SPEC 6).</summary>
    public const int MaxFirstLines = 100;

    /// <summary>Full path of the script file.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Lower-case extension including the leading dot, or an empty string when the file has none.</summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Last write time in UTC.</summary>
    public DateTimeOffset ModifiedUtc { get; init; }

    /// <summary>Lower-case hexadecimal SHA-256 of the contents, or <see langword="null"/> when hashing was skipped.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Whether the file carries a mark-of-the-web with a zone identifier of 3 or higher.</summary>
    public bool HasMotw { get; init; }

    /// <summary>Zone identifier read from the <c>:Zone.Identifier</c> stream, or <see langword="null"/> when absent.</summary>
    public int? ZoneId { get; init; }

    /// <summary>First <see cref="MaxFirstLines"/> lines of the file, without line terminators.</summary>
    public IReadOnlyList<string> FirstLines { get; init; } = [];

    /// <summary>The raw shebang line without the <c>#!</c> prefix, or <see langword="null"/> when the file has none.</summary>
    public string? Shebang { get; init; }

    /// <summary>Interpreter name parsed out of <see cref="Shebang"/>, or <see langword="null"/> when there is none.</summary>
    public string? ShebangInterpreter { get; init; }

    /// <summary>File name with extension.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Containing folder, used as the child process working directory (SPEC 7).</summary>
    public string? DirectoryPath => System.IO.Path.GetDirectoryName(Path);
}
