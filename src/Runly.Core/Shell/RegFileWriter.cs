using System.Globalization;
using System.Text;

namespace Runly.Core.Shell;

/// <summary>
/// Renders registry blocks as regedit compatible <c>.reg</c> text by hand; SPEC 9 forbids shelling out to
/// <c>reg.exe export</c>.
/// </summary>
public static class RegFileWriter
{
    /// <summary>The mandatory first line of a version 5 <c>.reg</c> file.</summary>
    public const string Header = "Windows Registry Editor Version 5.00";

    /// <summary>The hive prefix Runly writes and accepts; nothing else is ever emitted.</summary>
    public const string HkcuPrefix = "HKEY_CURRENT_USER";

    private const string LineEnding = "\r\n";
    private const int MaxLineLength = 76;

    /// <summary>Renders the blocks as a complete <c>.reg</c> document, header included.</summary>
    public static string Write(IEnumerable<RegKeyBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var sb = new StringBuilder();
        sb.Append(Header).Append(LineEnding);

        foreach (var block in blocks)
        {
            var path = NormalizeSubKey(block.SubKey);
            sb.Append(LineEnding);

            if (block.Delete)
            {
                sb.Append("[-").Append(HkcuPrefix);
                if (path.Length != 0)
                {
                    sb.Append('\\').Append(path);
                }

                sb.Append(']').Append(LineEnding);
                continue;
            }

            sb.Append('[').Append(HkcuPrefix);
            if (path.Length != 0)
            {
                sb.Append('\\').Append(path);
            }

            sb.Append(']').Append(LineEnding);

            foreach (var op in block.Values)
            {
                AppendValueLine(sb, op);
            }
        }

        return sb.ToString();
    }

    /// <summary>Escapes a string for use inside <c>.reg</c> double quotes: backslash and quote are doubled up.</summary>
    public static string EscapeString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var sb = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            if (ch is '\\' or '"')
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    /// <summary>Strips leading and trailing backslashes from a key path so the writer can join it consistently.</summary>
    public static string NormalizeSubKey(string subKey)
    {
        ArgumentNullException.ThrowIfNull(subKey);
        return subKey.Trim().Trim('\\');
    }

    private static void AppendValueLine(StringBuilder sb, RegValueOperation op)
    {
        var namePart = op.Name.Length == 0 ? "@" : "\"" + EscapeString(op.Name) + "\"";

        if (op.Delete)
        {
            sb.Append(namePart).Append("=-").Append(LineEnding);
            return;
        }

        var value = op.Value ?? throw new InvalidOperationException(
            $"'{op.Name}' değeri için veri yok ama silme işlemi de değil.");

        switch (value.Kind)
        {
            case RegistryValueKind.String:
                sb.Append(namePart).Append("=\"").Append(EscapeString(value.AsString())).Append('"').Append(LineEnding);
                return;

            case RegistryValueKind.DWord:
                var number = value.AsDWord() ?? 0u;
                sb.Append(namePart)
                  .Append("=dword:")
                  .Append(number.ToString("x8", CultureInfo.InvariantCulture))
                  .Append(LineEnding);
                return;

            default:
                AppendHexLine(sb, namePart, value);
                return;
        }
    }

    private static void AppendHexLine(StringBuilder sb, string namePart, RegistryValueEntry value)
    {
        // REG_BINARY uses the bare "hex:" form; every other type carries its numeric code, e.g. "hex(2):".
        var prefix = value.Kind == RegistryValueKind.Binary
            ? namePart + "=hex:"
            : namePart + "=hex(" + ((int)value.Kind).ToString("x", CultureInfo.InvariantCulture) + "):";

        sb.Append(prefix);
        var column = prefix.Length;

        for (var i = 0; i < value.Data.Length; i++)
        {
            var token = value.Data[i].ToString("x2", CultureInfo.InvariantCulture);
            var needsComma = i < value.Data.Length - 1;
            var chunk = needsComma ? token + "," : token;

            if (column + chunk.Length > MaxLineLength)
            {
                sb.Append('\\').Append(LineEnding).Append("  ");
                column = 2;
            }

            sb.Append(chunk);
            column += chunk.Length;
        }

        sb.Append(LineEnding);
    }
}
