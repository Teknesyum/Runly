using Microsoft.Win32;
using Runly.Core.Models;
using Runly.Core.Services;

namespace Runly.Settings.Discovery;

/// <summary>The registry reads <see cref="UsageHistory"/> needs, behind an interface so the ranking
/// itself can be exercised without a machine-shaped HKCU under it.</summary>
internal interface IUsageHistorySource
{
    /// <summary>Raw <c>MRUList</c> value of the extension's <c>OpenWithList</c> key, or
    /// <see langword="null"/> when the key or the value is missing.</summary>
    string? OpenWithListOrder(string extension);

    /// <summary>Value name to value data of the extension's <c>OpenWithList</c> key, without
    /// <c>MRUList</c>. The names are the single letters <c>MRUList</c> refers to.</summary>
    IReadOnlyDictionary<string, string> OpenWithList(string extension);

    /// <summary>ProgIDs registered under the extension's <c>OpenWithProgids</c> key.</summary>
    IReadOnlyList<string> OpenWithProgIds(string extension);

    /// <summary>Full path of an executable named by file name or by path, or <see langword="null"/>
    /// when it cannot be resolved to a file that exists.</summary>
    string? ResolveExecutable(string candidate);

    /// <summary>Full path behind a ProgID's <c>shell\open\command</c>, or <see langword="null"/>.</summary>
    string? ResolveProgId(string progId);
}

/// <summary>What the machine says the user actually opens a given extension with, most recent first.
///
/// This sits beside <see cref="AssocHandlerFinder"/> rather than inside it: the shell's handler list
/// says which applications <em>can</em> open the extension and carries a recommendation flag, but it
/// has no record of the one the user reached for last. Windows keeps that under
/// <c>Explorer\FileExts</c>, and Runly keeps its own in the configuration file.
///
/// Read only. Nothing here writes to the registry.</summary>
internal static class UsageHistory
{
    private const string FileExtsRoot =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts";

    /// <summary>Ranked executables for <paramref name="extension"/>, best first. Never throws: a
    /// registry that cannot be read yields an empty list and the caller keeps its old order.</summary>
    public static IReadOnlyList<string> Rank(
        string extension,
        IReadOnlyDictionary<string, ExtensionMapping>? mappings)
    {
        try
        {
            return Rank(extension, new RegistryUsageHistorySource(), mappings);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (System.Security.SecurityException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    /// <summary>Ranking proper. The three sources are appended in weight order and the first
    /// appearance of an executable wins, so a later, weaker source can never demote it.</summary>
    public static IReadOnlyList<string> Rank(
        string extension,
        IUsageHistorySource source,
        IReadOnlyDictionary<string, ExtensionMapping>? mappings)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!IsExtension(extension))
        {
            return [];
        }

        var ranked = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in OrderByMru(source.OpenWithListOrder(extension), source.OpenWithList(extension)))
        {
            Append(ranked, seen, Resolve(source, name));
        }

        foreach (var progId in source.OpenWithProgIds(extension))
        {
            if (string.IsNullOrWhiteSpace(progId))
            {
                continue;
            }

            Append(ranked, seen, source.ResolveProgId(progId));
        }

        if (mappings is null)
        {
            return ranked;
        }

        foreach (var own in OwnHistory(extension, mappings))
        {
            Append(ranked, seen, Resolve(source, own));
        }

        return ranked;
    }

    /// <summary><c>MRUList</c> spells the order out as letters: <c>"cab"</c> means the <c>c</c> value
    /// was used last. The letters are the order; sorting the value names instead would throw the whole
    /// signal away. Names the list does not mention still count, just behind the ones it does — a
    /// truncated or corrupt <c>MRUList</c> degrades to alphabetical rather than to nothing.</summary>
    public static IReadOnlyList<string> OrderByMru(
        string? mruList,
        IReadOnlyDictionary<string, string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var ordered = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var letter in mruList ?? string.Empty)
        {
            var name = letter.ToString();
            if (!used.Add(name))
            {
                continue;
            }

            if (entries.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                ordered.Add(value.Trim());
            }
        }

        foreach (var entry in entries.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (used.Contains(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
            {
                continue;
            }

            ordered.Add(entry.Value.Trim());
        }

        return ordered;
    }

    /// <summary>Choices the user already made inside Runly: the exact extension first, then the other
    /// extensions of the same category. Picking Notepad++ for <c>.md</c> is evidence about <c>.txt</c>,
    /// but weaker evidence than anything recorded for <c>.txt</c> itself.</summary>
    private static IEnumerable<string> OwnHistory(
        string extension,
        IReadOnlyDictionary<string, ExtensionMapping> mappings)
    {
        if (mappings.TryGetValue(extension, out var exact))
        {
            foreach (var path in Executables(exact))
            {
                yield return path;
            }
        }

        var category = exact?.Category;
        if (string.IsNullOrWhiteSpace(category))
        {
            yield break;
        }

        var neighbours = mappings
            .Where(pair =>
                !string.Equals(pair.Key, extension, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(pair.Value.Category, category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var neighbour in neighbours)
        {
            foreach (var path in Executables(neighbour.Value))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> Executables(ExtensionMapping mapping)
    {
        if (!string.IsNullOrWhiteSpace(mapping.OpenWith))
        {
            yield return mapping.OpenWith;
        }

        if (!string.IsNullOrWhiteSpace(mapping.Interpreter))
        {
            yield return mapping.Interpreter;
        }
    }

    /// <summary>A packaged application is registered as <c>Family_hash!App</c>, which is an activation
    /// moniker and not a file. <c>ProcessLauncher</c> cannot start one, so it is dropped before the
    /// source is asked to look anything up.</summary>
    private static string? Resolve(IUsageHistorySource source, string candidate) =>
        string.IsNullOrWhiteSpace(candidate) || candidate.Contains('!', StringComparison.Ordinal)
            ? null
            : source.ResolveExecutable(candidate);

    private static void Append(List<string> ranked, HashSet<string> seen, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsOwnExecutable(path) || !seen.Add(path))
        {
            return;
        }

        ranked.Add(path);
    }

    /// <summary>Runly proposing Runly would hand the file straight back to itself.
    /// <c>IsRunlyExecutable</c> only recognises the launcher and the running process, so the settings
    /// executable is named here as well: this list is built by the settings process for the launcher.</summary>
    private static bool IsOwnExecutable(string path)
    {
        try
        {
            return ProcessLauncher.IsRunlyExecutable(path) ||
                   string.Equals(Path.GetFileName(path), "RunlySettings.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static bool IsExtension(string extension) =>
        !string.IsNullOrWhiteSpace(extension) && extension.Length > 1 && extension[0] == '.';

    private sealed class RegistryUsageHistorySource : IUsageHistorySource
    {
        public string? OpenWithListOrder(string extension) =>
            OpenWithListKey(extension) is { } value ? value.MruList : null;

        public IReadOnlyDictionary<string, string> OpenWithList(string extension) =>
            OpenWithListKey(extension) is { } value
                ? value.Entries
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> OpenWithProgIds(string extension)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    $@"{FileExtsRoot}\{extension}\OpenWithProgids", writable: false);
                return key is null ? [] : key.GetValueNames().Where(name => name.Length > 0).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                return [];
            }
            catch (System.Security.SecurityException)
            {
                return [];
            }
            catch (IOException)
            {
                return [];
            }
        }

        public string? ResolveProgId(string progId)
        {
            var command = ReadDefault(Registry.ClassesRoot, $@"{progId}\shell\open\command");
            return command is null ? null : ResolveExecutable(ExtractExecutable(command) ?? string.Empty);
        }

        /// <summary>The <c>OpenWithList</c> values are bare file names, not paths, so the file name has
        /// to be turned back into a program: first <c>App Paths</c>, which is what the shell itself
        /// consults, then the per-file-name <c>Applications</c> registration.</summary>
        public string? ResolveExecutable(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            var name = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));
            if (name.Length == 0)
            {
                return null;
            }

            if (Path.IsPathFullyQualified(name))
            {
                return Existing(name);
            }

            var fileName = Path.GetFileName(name);
            if (fileName.Length == 0)
            {
                return null;
            }

            var appPath = ReadDefault(Registry.CurrentUser, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{fileName}")
                ?? ReadDefault(Registry.LocalMachine, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{fileName}");
            if (appPath is not null && Existing(appPath.Trim().Trim('"')) is { } fromAppPaths)
            {
                return fromAppPaths;
            }

            var command = ReadDefault(Registry.ClassesRoot, $@"Applications\{fileName}\shell\open\command");
            var executable = ExtractExecutable(command);
            return executable is null ? null : Existing(Environment.ExpandEnvironmentVariables(executable));
        }

        private static (string? MruList, IReadOnlyDictionary<string, string> Entries)? OpenWithListKey(string extension)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    $@"{FileExtsRoot}\{extension}\OpenWithList", writable: false);
                if (key is null)
                {
                    return null;
                }

                var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in key.GetValueNames())
                {
                    if (name.Length == 0 || string.Equals(name, "MRUList", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (key.GetValue(name) is string value && value.Length > 0)
                    {
                        entries[name] = value;
                    }
                }

                return (key.GetValue("MRUList") as string, entries);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static string? ReadDefault(RegistryKey root, string path)
        {
            try
            {
                using var key = root.OpenSubKey(path, writable: false);
                return key?.GetValue(null) as string;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static string? Existing(string path)
        {
            try
            {
                var full = Path.GetFullPath(path);
                return File.Exists(full) ? full : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private static string? ExtractExecutable(string? command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            command = command.Trim();
            if (command[0] == '"')
            {
                var end = command.IndexOf('"', 1);
                return end > 1 ? command[1..end] : null;
            }

            var exeEnd = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exeEnd >= 0 ? command[..(exeEnd + 4)] : null;
        }
    }
}
