namespace Runly.Core.Shell;

/// <summary>One <c>[HKEY_CURRENT_USER\…]</c> block of a <c>.reg</c> file, or a <c>[-HKEY_CURRENT_USER\…]</c> delete line.</summary>
public sealed record RegKeyBlock
{
    /// <summary>Key path relative to <c>HKEY_CURRENT_USER</c>, without leading or trailing backslashes.</summary>
    public string SubKey { get; init; } = string.Empty;

    /// <summary>When <see langword="true"/> the block is a <c>[-HKEY…]</c> line that removes the key and everything under it.</summary>
    public bool Delete { get; init; }

    /// <summary>Value operations inside the block; always empty for delete blocks.</summary>
    public IReadOnlyList<RegValueOperation> Values { get; init; } = [];

    /// <summary>Builds a <c>[-HKEY…]</c> delete block for the given key.</summary>
    public static RegKeyBlock DeleteKey(string subKey) => new() { SubKey = subKey, Delete = true };
}

/// <summary>A single value line inside a <c>.reg</c> block: either a write or a <c>"name"=-</c> removal.</summary>
public sealed record RegValueOperation
{
    /// <summary>Value name; the empty string means the key's default value.</summary>
    public string Name { get; init; } = RegistryValueEntry.DefaultValueName;

    /// <summary>When <see langword="true"/> the line removes the value instead of writing it.</summary>
    public bool Delete { get; init; }

    /// <summary>The value to write; <see langword="null"/> for removals.</summary>
    public RegistryValueEntry? Value { get; init; }

    /// <summary>Wraps a value in a write operation.</summary>
    public static RegValueOperation Set(RegistryValueEntry value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new RegValueOperation { Name = value.Name, Value = value };
    }

    /// <summary>Builds a <c>"name"=-</c> removal operation.</summary>
    public static RegValueOperation Remove(string name) => new() { Name = name, Delete = true };
}

/// <summary>Thrown when a <c>.reg</c> file is malformed or refers to a hive other than <c>HKEY_CURRENT_USER</c>.</summary>
public sealed class RegFileFormatException : Exception
{
    /// <summary>Creates the exception for a specific line of the file.</summary>
    public RegFileFormatException(string message, int lineNumber)
        : base($"{message} (satır {lineNumber})")
    {
        LineNumber = lineNumber;
    }

    /// <summary>Creates the exception without a line reference.</summary>
    public RegFileFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a default message.</summary>
    public RegFileFormatException()
        : base("Geçersiz .reg dosyası.")
    {
    }

    /// <summary>Creates the exception wrapping an inner cause.</summary>
    public RegFileFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>One based line number the problem was found on, or zero when unknown.</summary>
    public int LineNumber { get; }
}
