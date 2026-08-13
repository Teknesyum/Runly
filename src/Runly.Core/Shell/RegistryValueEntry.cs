using System.Text;

namespace Runly.Core.Shell;

/// <summary>The registry hives Runly touches; it only ever writes under <see cref="CurrentUser"/> (SPEC 9).</summary>
public enum RegistryRoot
{
    /// <summary><c>HKEY_CURRENT_USER</c> — the only hive Runly is allowed to write to.</summary>
    CurrentUser,

    /// <summary><c>HKEY_CLASSES_ROOT</c> — read only, used to turn a ProgID into a friendly name.</summary>
    ClassesRoot,
}

/// <summary>The registry value types Runly can read, write and serialise to a <c>.reg</c> file.</summary>
public enum RegistryValueKind
{
    /// <summary><c>REG_SZ</c>.</summary>
    String = 1,

    /// <summary><c>REG_EXPAND_SZ</c>.</summary>
    ExpandString = 2,

    /// <summary><c>REG_BINARY</c>.</summary>
    Binary = 3,

    /// <summary><c>REG_DWORD</c> (little endian).</summary>
    DWord = 4,

    /// <summary><c>REG_MULTI_SZ</c>.</summary>
    MultiString = 7,

    /// <summary><c>REG_QWORD</c> (little endian).</summary>
    QWord = 11,
}

/// <summary>A single registry value carried as raw bytes so that backups round-trip without loss.</summary>
public sealed record RegistryValueEntry
{
    /// <summary>The name of a key's unnamed (default) value.</summary>
    public const string DefaultValueName = "";

    /// <summary>Value name; the empty string means the key's default value.</summary>
    public string Name { get; init; } = DefaultValueName;

    /// <summary>Registry type of the value.</summary>
    public RegistryValueKind Kind { get; init; } = RegistryValueKind.String;

    /// <summary>Raw bytes exactly as stored in the registry.</summary>
    public byte[] Data { get; init; } = [];

    /// <summary>Whether this entry is the key's unnamed default value.</summary>
    public bool IsDefault => Name.Length == 0;

    /// <summary>Creates a <c>REG_SZ</c> value; the stored bytes include the terminating null, as the registry does.</summary>
    public static RegistryValueEntry FromString(string name, string value) =>
        new() { Name = name, Kind = RegistryValueKind.String, Data = EncodeString(value) };

    /// <summary>Creates a <c>REG_EXPAND_SZ</c> value.</summary>
    public static RegistryValueEntry FromExpandString(string name, string value) =>
        new() { Name = name, Kind = RegistryValueKind.ExpandString, Data = EncodeString(value) };

    /// <summary>Creates a <c>REG_DWORD</c> value.</summary>
    public static RegistryValueEntry FromDWord(string name, uint value) =>
        new() { Name = name, Kind = RegistryValueKind.DWord, Data = BitConverter.GetBytes(value) };

    /// <summary>Creates a <c>REG_BINARY</c> value.</summary>
    public static RegistryValueEntry FromBinary(string name, byte[] value) =>
        new() { Name = name, Kind = RegistryValueKind.Binary, Data = (byte[])value.Clone() };

    /// <summary>Creates a <c>REG_MULTI_SZ</c> value.</summary>
    public static RegistryValueEntry FromMultiString(string name, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var sb = new StringBuilder();
        foreach (var item in values)
        {
            sb.Append(item).Append('\0');
        }

        sb.Append('\0');
        return new RegistryValueEntry
        {
            Name = name,
            Kind = RegistryValueKind.MultiString,
            Data = Encoding.Unicode.GetBytes(sb.ToString()),
        };
    }

    /// <summary>Decodes the value as text; returns the empty string for types that are not string based.</summary>
    public string AsString()
    {
        if (Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString) || Data.Length == 0)
        {
            return string.Empty;
        }

        var text = Encoding.Unicode.GetString(Data);
        var end = text.IndexOf('\0', StringComparison.Ordinal);
        return end >= 0 ? text[..end] : text;
    }

    /// <summary>Decodes the value as a 32 bit number; returns <see langword="null"/> when the type or size does not fit.</summary>
    public uint? AsDWord() =>
        Kind == RegistryValueKind.DWord && Data.Length >= 4 ? BitConverter.ToUInt32(Data, 0) : null;

    private static byte[] EncodeString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.Unicode.GetBytes(value + '\0');
    }
}
