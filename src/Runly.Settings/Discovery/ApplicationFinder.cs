using Microsoft.Win32;

namespace Runly.Settings.Discovery;

/// <summary>Read-only discovery of applications Windows exposes as file handlers.</summary>
internal sealed class ApplicationFinder
{
    public IReadOnlyList<InstalledApplication> FindAll()
    {
        var found = new Dictionary<string, InstalledApplication>(StringComparer.OrdinalIgnoreCase);
        ScanAppPaths(Registry.CurrentUser, found);
        ScanAppPaths(Registry.LocalMachine, found);
        ScanApplications(Registry.CurrentUser, found);
        ScanApplications(Registry.LocalMachine, found);
        ScanStartMenu(found);
        return found.Values.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public static IReadOnlyList<InstalledApplication> MatchSuggested(
        IEnumerable<InstalledApplication> applications,
        IEnumerable<string> suggestedNames)
    {
        var names = suggestedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return applications.Where(app => names.Contains(app.ExecutableName)).ToArray();
    }

    private static void ScanAppPaths(RegistryKey root, IDictionary<string, InstalledApplication> found)
    {
        using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths", writable: false);
        if (key is null) return;
        foreach (var name in SafeSubKeyNames(key))
        {
            using var app = key.OpenSubKey(name, writable: false);
            Add(found, app?.GetValue(null) as string, name, "App Paths");
        }
    }

    private static void ScanApplications(RegistryKey root, IDictionary<string, InstalledApplication> found)
    {
        using var key = root.OpenSubKey(@"Software\Classes\Applications", writable: false);
        if (key is null) return;
        foreach (var name in SafeSubKeyNames(key))
        {
            using var command = key.OpenSubKey(name + @"\shell\open\command", writable: false);
            var raw = command?.GetValue(null) as string;
            Add(found, ExtractExecutable(raw), name, "Applications");
        }
    }

    private static void ScanStartMenu(IDictionary<string, InstalledApplication> found)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };
        foreach (var root in roots.Where(Directory.Exists))
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            foreach (var shortcut in Directory.EnumerateFiles(root, "*.lnk", options))
            {
                var target = ShortcutTargetReader.TryRead(shortcut);
                Add(found, target, Path.GetFileNameWithoutExtension(shortcut), "Start menu");
            }
        }
    }

    private static string[] SafeSubKeyNames(RegistryKey key)
    {
        try { return key.GetSubKeyNames(); }
        catch (UnauthorizedAccessException) { return []; }
        catch (System.Security.SecurityException) { return []; }
    }

    private static void Add(IDictionary<string, InstalledApplication> found, string? path, string displayName, string source)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (!Path.IsPathFullyQualified(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return;
        var fullPath = Path.GetFullPath(path);
        found.TryAdd(fullPath, new(Path.GetFileName(fullPath), Path.GetFileNameWithoutExtension(displayName), fullPath, source));
    }

    private static string? ExtractExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = Environment.ExpandEnvironmentVariables(command.Trim());
        if (command[0] == '"')
        {
            var end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }
        var exeEnd = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeEnd >= 0 ? command[..(exeEnd + 4)] : null;
    }
}
