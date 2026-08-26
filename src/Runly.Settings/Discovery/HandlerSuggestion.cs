namespace Runly.Settings.Discovery;

/// <summary>Turns a <see cref="UsageHistory"/> ranking into the one handler the grid offers for a row.
///
/// Pure on purpose: the grid needs the decision itself testable without a machine-shaped registry
/// under it, and the registry read is expensive enough that the caller has to cache it separately.</summary>
internal static class HandlerSuggestion
{
    /// <summary>Full path of the suggested executable, or <see langword="null"/> when the row already
    /// has a handler, the extension is not one, or nothing in the ranking is usable.</summary>
    public static string? Pick(string? extension, string? currentHandler, IReadOnlyList<string>? ranked)
    {
        if (!string.IsNullOrWhiteSpace(currentHandler))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(extension) || extension.Length < 2 || extension[0] != '.')
        {
            return null;
        }

        if (ranked is null)
        {
            return null;
        }

        foreach (var candidate in ranked)
        {
            if (FileNameOf(candidate) is not null)
            {
                return candidate.Trim();
            }
        }

        return null;
    }

    /// <summary>File name of <see cref="Pick"/>, which is what the İşleyici column has room for.</summary>
    public static string? DisplayName(string? extension, string? currentHandler, IReadOnlyList<string>? ranked) =>
        Pick(extension, currentHandler, ranked) is { } path ? FileNameOf(path) : null;

    private static string? FileNameOf(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        try
        {
            var name = Path.GetFileName(candidate.Trim());
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
