using Runly.Core.Shell;

namespace Runly.Core.Tests.Shell;

/// <summary>
/// An in-memory stand-in for the Windows registry. Every shell test runs against this; T4 forbids automated
/// tests that touch the real registry.
/// </summary>
internal sealed class FakeRegistryAccessor : IRegistryAccessor
{
    private readonly Dictionary<RegistryRoot, Dictionary<string, Dictionary<string, RegistryValueEntry>>> _hives =
        new()
        {
            [RegistryRoot.CurrentUser] = new(StringComparer.OrdinalIgnoreCase),
            [RegistryRoot.ClassesRoot] = new(StringComparer.OrdinalIgnoreCase),
        };

    /// <summary>Every write that was rejected because it targeted a hive other than HKCU.</summary>
    public List<string> RejectedWrites { get; } = [];

    public bool KeyExists(RegistryRoot root, string subKey) =>
        _hives[root].ContainsKey(Normalize(subKey));

    public IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string subKey)
    {
        var prefix = Normalize(subKey);
        var names = new List<string>();

        foreach (var key in _hives[root].Keys)
        {
            if (key.Length <= prefix.Length ||
                !key.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = key[(prefix.Length + 1)..];
            var cut = rest.IndexOf('\\', StringComparison.Ordinal);
            var child = cut < 0 ? rest : rest[..cut];

            if (!names.Contains(child, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(child);
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public IReadOnlyList<RegistryValueEntry> GetValues(RegistryRoot root, string subKey) =>
        _hives[root].TryGetValue(Normalize(subKey), out var values)
            ? values.Values.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : [];

    public RegistryValueEntry? GetValue(RegistryRoot root, string subKey, string valueName) =>
        _hives[root].TryGetValue(Normalize(subKey), out var values) &&
        values.TryGetValue(valueName, out var entry)
            ? entry
            : null;

    public void CreateKey(RegistryRoot root, string subKey)
    {
        if (!Writable(root, subKey))
        {
            return;
        }

        EnsureKey(root, Normalize(subKey));
    }

    public void SetValue(RegistryRoot root, string subKey, RegistryValueEntry value)
    {
        if (!Writable(root, subKey))
        {
            return;
        }

        EnsureKey(root, Normalize(subKey))[value.Name] = value;
    }

    public void DeleteValue(RegistryRoot root, string subKey, string valueName)
    {
        if (!Writable(root, subKey))
        {
            return;
        }

        if (_hives[root].TryGetValue(Normalize(subKey), out var values))
        {
            values.Remove(valueName);
        }
    }

    /// <summary>Keys the real Windows ACL would refuse to delete, such as a protected <c>UserChoice</c>.</summary>
    public List<string> UndeletableKeys { get; } = [];

    public void DeleteKeyTree(RegistryRoot root, string subKey)
    {
        if (!Writable(root, subKey))
        {
            return;
        }

        if (UndeletableKeys.Any(k => k.Equals(Normalize(subKey), StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException("Erişim engellendi");
        }

        var path = Normalize(subKey);
        var doomed = _hives[root].Keys
            .Where(k => k.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in doomed)
        {
            _hives[root].Remove(key);
        }
    }

    /// <summary>Seeds a key with no values, as the real registry would have after a bare CreateKey.</summary>
    public void Seed(RegistryRoot root, string subKey) => EnsureKey(root, Normalize(subKey));

    /// <summary>Seeds a string value, creating the key when needed.</summary>
    public void Seed(RegistryRoot root, string subKey, string valueName, string value) =>
        EnsureKey(root, Normalize(subKey))[valueName] = RegistryValueEntry.FromString(valueName, value);

    /// <summary>Seeds a raw value, creating the key when needed.</summary>
    public void Seed(RegistryRoot root, string subKey, RegistryValueEntry value) =>
        EnsureKey(root, Normalize(subKey))[value.Name] = value;

    /// <summary>All key paths currently present in a hive, for assertions.</summary>
    public IReadOnlyList<string> AllKeys(RegistryRoot root) =>
        _hives[root].Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    private Dictionary<string, RegistryValueEntry> EnsureKey(RegistryRoot root, string path)
    {
        var hive = _hives[root];

        // Creating a key implicitly creates its parents, just like RegCreateKeyEx.
        var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var walked = string.Empty;

        foreach (var part in parts)
        {
            walked = walked.Length == 0 ? part : walked + "\\" + part;
            if (!hive.ContainsKey(walked))
            {
                hive[walked] = new Dictionary<string, RegistryValueEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        return hive[path];
    }

    private bool Writable(RegistryRoot root, string subKey)
    {
        if (root == RegistryRoot.CurrentUser)
        {
            return true;
        }

        RejectedWrites.Add($"{root}:{subKey}");
        return false;
    }

    private static string Normalize(string subKey) => subKey.Trim().Trim('\\');
}
