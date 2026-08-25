namespace Runly.Core.Services;

/// <summary>
/// The command line <c>RunlySettings.exe</c> understands. Only <c>--select &lt;extension&gt;</c> is
/// recognised; anything else is ignored on purpose, because a shortcut or a shell verb can hand the
/// process arguments it never asked for and losing the window over one of them is worse than ignoring it.
/// </summary>
public static class SettingsCommandLine
{
    /// <summary>The switch that carries the extension to preselect.</summary>
    public const string SelectSwitch = "--select";

    /// <summary>Longest accepted extension, dot included.</summary>
    public const int MaxExtensionLength = 24;

    /// <summary>
    /// Reads the extension to preselect out of <paramref name="args"/>; returns <see langword="null"/>
    /// when the switch is absent or its value is not a usable extension.
    /// </summary>
    public static string? ParseSelectedExtension(string[]? args)
    {
        if (args is null)
        {
            return null;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], SelectSwitch, StringComparison.OrdinalIgnoreCase) || i + 1 >= args.Length)
            {
                continue;
            }

            return NormalizeExtension(args[i + 1]);
        }

        return null;
    }

    /// <summary>
    /// Normalizes one extension: a leading dot is added when missing, the result is lowercased and
    /// rejected unless it is at most <see cref="MaxExtensionLength"/> characters of
    /// <c>[A-Za-z0-9_.-]</c>.
    /// </summary>
    public static string? NormalizeExtension(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed[0] != '.')
        {
            trimmed = "." + trimmed;
        }

        if (trimmed.Length <= 1 || trimmed.Length > MaxExtensionLength)
        {
            return null;
        }

        foreach (var character in trimmed)
        {
            var accepted = character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9')
                or '_' or '.' or '-';
            if (!accepted)
            {
                return null;
            }
        }

        return trimmed.ToLowerInvariant();
    }
}
