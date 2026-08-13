using System.Globalization;
using System.Text;

namespace Runly.Core.Shell;

/// <summary>
/// Parses <c>.reg</c> text into registry operations. Runly applies backups through this parser instead of
/// <c>regedit /s</c>, which fails silently and can raise a UAC prompt (SPEC 9, T4).
/// </summary>
public static class RegFileParser
{
    /// <summary>
    /// Parses a complete <c>.reg</c> document. Only <c>HKEY_CURRENT_USER</c> is accepted; any other hive is a
    /// hard error so that a tampered or hand-edited backup can never reach a system wide key.
    /// </summary>
    public static IReadOnlyList<RegKeyBlock> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = Unfold(text);
        var blocks = new List<RegKeyBlock>();
        var headerSeen = false;

        string? currentKey = null;
        var currentDelete = false;
        List<RegValueOperation>? currentValues = null;

        foreach (var (content, lineNumber) in lines)
        {
            var line = content.Trim();
            if (line.Length == 0 || line[0] == ';')
            {
                continue;
            }

            if (!headerSeen)
            {
                if (!line.StartsWith("Windows Registry Editor Version 5", StringComparison.OrdinalIgnoreCase))
                {
                    throw new RegFileFormatException(
                        "Dosya geçerli bir .reg başlığıyla başlamıyor.", lineNumber);
                }

                headerSeen = true;
                continue;
            }

            if (line[0] == '[')
            {
                Flush(blocks, ref currentKey, ref currentDelete, ref currentValues);

                if (line[^1] != ']')
                {
                    throw new RegFileFormatException("Anahtar satırı ']' ile kapanmıyor.", lineNumber);
                }

                var inner = line[1..^1].Trim();
                currentDelete = inner.Length != 0 && inner[0] == '-';
                if (currentDelete)
                {
                    inner = inner[1..].Trim();
                }

                currentKey = RequireHkcu(inner, lineNumber);
                currentValues = [];
                continue;
            }

            if (currentKey is null)
            {
                throw new RegFileFormatException(
                    "Değer satırı bir anahtar bloğunun dışında.", lineNumber);
            }

            if (currentDelete)
            {
                throw new RegFileFormatException(
                    "Silme bloğunun ([-HKEY…]) içinde değer satırı olamaz.", lineNumber);
            }

            currentValues!.Add(ParseValueLine(line, lineNumber));
        }

        if (!headerSeen)
        {
            throw new RegFileFormatException("Dosya boş ya da .reg başlığı yok.");
        }

        Flush(blocks, ref currentKey, ref currentDelete, ref currentValues);
        return blocks;
    }

    private static void Flush(
        List<RegKeyBlock> blocks,
        ref string? currentKey,
        ref bool currentDelete,
        ref List<RegValueOperation>? currentValues)
    {
        if (currentKey is null)
        {
            return;
        }

        blocks.Add(new RegKeyBlock
        {
            SubKey = currentKey,
            Delete = currentDelete,
            Values = currentDelete ? [] : (currentValues ?? []),
        });

        currentKey = null;
        currentDelete = false;
        currentValues = null;
    }

    /// <summary>
    /// Verifies that a bracketed path names <c>HKEY_CURRENT_USER</c> and returns the part below it.
    /// </summary>
    private static string RequireHkcu(string path, int lineNumber)
    {
        if (path.Length == 0)
        {
            throw new RegFileFormatException("Anahtar yolu boş.", lineNumber);
        }

        var separator = path.IndexOf('\\', StringComparison.Ordinal);
        var root = separator < 0 ? path : path[..separator];
        var rest = separator < 0 ? string.Empty : path[(separator + 1)..];

        if (!root.Equals(RegFileWriter.HkcuPrefix, StringComparison.OrdinalIgnoreCase) &&
            !root.Equals("HKCU", StringComparison.OrdinalIgnoreCase))
        {
            throw new RegFileFormatException(
                $"Yalnızca HKEY_CURRENT_USER kabul edilir, '{root}' reddedildi.", lineNumber);
        }

        return rest.Trim().Trim('\\');
    }

    private static RegValueOperation ParseValueLine(string line, int lineNumber)
    {
        string name;
        int equalsIndex;

        if (line[0] == '@')
        {
            name = RegistryValueEntry.DefaultValueName;
            equalsIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex != 1)
            {
                throw new RegFileFormatException("'@' işaretinden sonra '=' bekleniyordu.", lineNumber);
            }
        }
        else if (line[0] == '"')
        {
            name = ReadQuoted(line, 0, out var afterName, lineNumber);
            equalsIndex = line.IndexOf('=', afterName);
            if (equalsIndex < 0 || line[afterName..equalsIndex].Trim().Length != 0)
            {
                throw new RegFileFormatException("Değer adından sonra '=' bekleniyordu.", lineNumber);
            }
        }
        else
        {
            throw new RegFileFormatException("Değer satırı '@' ya da tırnakla başlamalı.", lineNumber);
        }

        var rhs = line[(equalsIndex + 1)..].Trim();
        if (rhs.Length == 0)
        {
            throw new RegFileFormatException("Değer verisi eksik.", lineNumber);
        }

        if (rhs == "-")
        {
            return RegValueOperation.Remove(name);
        }

        if (rhs[0] == '"')
        {
            var text = ReadQuoted(rhs, 0, out _, lineNumber);
            return RegValueOperation.Set(RegistryValueEntry.FromString(name, text));
        }

        if (rhs.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
        {
            var digits = rhs["dword:".Length..].Trim();
            if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
            {
                throw new RegFileFormatException($"'{digits}' geçerli bir dword değeri değil.", lineNumber);
            }

            return RegValueOperation.Set(RegistryValueEntry.FromDWord(name, number));
        }

        if (rhs.StartsWith("hex", StringComparison.OrdinalIgnoreCase))
        {
            return RegValueOperation.Set(ParseHex(name, rhs, lineNumber));
        }

        throw new RegFileFormatException($"Tanınmayan değer biçimi: '{rhs}'.", lineNumber);
    }

    private static RegistryValueEntry ParseHex(string name, string rhs, int lineNumber)
    {
        var colon = rhs.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            throw new RegFileFormatException("hex değerinde ':' yok.", lineNumber);
        }

        var head = rhs[..colon];
        var kind = RegistryValueKind.Binary;

        if (head.Length > 3)
        {
            if (head[3] != '(' || head[^1] != ')')
            {
                throw new RegFileFormatException($"Bozuk hex tip belirteci: '{head}'.", lineNumber);
            }

            var code = head[4..^1];
            if (!int.TryParse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var kindCode))
            {
                throw new RegFileFormatException($"'{code}' geçerli bir kayıt defteri tipi değil.", lineNumber);
            }

            kind = (RegistryValueKind)kindCode;
        }

        var body = rhs[(colon + 1)..].Replace(" ", string.Empty, StringComparison.Ordinal);
        var bytes = new List<byte>();

        foreach (var token in body.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                throw new RegFileFormatException($"'{token}' geçerli bir onaltılık bayt değil.", lineNumber);
            }

            bytes.Add(b);
        }

        return new RegistryValueEntry { Name = name, Kind = kind, Data = [.. bytes] };
    }

    private static string ReadQuoted(string line, int start, out int afterIndex, int lineNumber)
    {
        if (line[start] != '"')
        {
            throw new RegFileFormatException("Tırnak bekleniyordu.", lineNumber);
        }

        var sb = new StringBuilder();
        var i = start + 1;

        while (i < line.Length)
        {
            var ch = line[i];
            if (ch == '\\')
            {
                if (i + 1 >= line.Length)
                {
                    throw new RegFileFormatException("Satır kaçış karakteriyle bitiyor.", lineNumber);
                }

                sb.Append(line[i + 1]);
                i += 2;
                continue;
            }

            if (ch == '"')
            {
                afterIndex = i + 1;
                return sb.ToString();
            }

            sb.Append(ch);
            i++;
        }

        throw new RegFileFormatException("Kapanmayan tırnak.", lineNumber);
    }

    /// <summary>
    /// Splits the document into logical lines, joining regedit's backslash continuations and reporting the
    /// line number the logical line started on.
    /// </summary>
    private static List<(string Content, int LineNumber)> Unfold(string text)
    {
        var result = new List<(string, int)>();
        var raw = text.TrimStart('﻿').Split('\n');

        var pending = new StringBuilder();
        var pendingStart = 0;

        for (var i = 0; i < raw.Length; i++)
        {
            var line = raw[i].TrimEnd('\r');

            if (pending.Length == 0)
            {
                pendingStart = i + 1;
            }

            var trimmed = line.TrimEnd();
            if (trimmed.EndsWith('\\'))
            {
                pending.Append(pending.Length == 0 ? trimmed[..^1] : trimmed[..^1].TrimStart());
                continue;
            }

            if (pending.Length == 0)
            {
                result.Add((line, i + 1));
            }
            else
            {
                pending.Append(trimmed.TrimStart());
                result.Add((pending.ToString(), pendingStart));
                pending.Clear();
            }
        }

        if (pending.Length != 0)
        {
            result.Add((pending.ToString(), pendingStart));
        }

        return result;
    }
}
