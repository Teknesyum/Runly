using Runly.Core.Models;

namespace Runly.Core.Shell;

/// <summary>
/// Hides other programs' context-menu entries on Runly's script types and puts them back. Everything is
/// written under HKCU and recorded in a ledger under <c>Software\Runly\MenuCleanup</c>, so a revert touches
/// only what Runly itself changed.
/// </summary>
public sealed class ContextMenuCleaner
{
    /// <summary>Ledger root; lives under <see cref="RunlyRegistryLayout.VendorKey"/>.</summary>
    public const string LedgerKey = RunlyRegistryLayout.VendorKey + @"\MenuCleanup";

    /// <summary>The per-user list of shell extension handlers Explorer refuses to load.</summary>
    public const string BlockedKey = @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

    private const string AppliesToLedger = LedgerKey + @"\AppliesTo";
    private const string BlockedLedger = LedgerKey + @"\Blocked";
    private const string VerbLedger = LedgerKey + @"\Verbs";
    private const string AppliesToValue = "AppliesTo";
    private const string HideValue = "ProgrammaticAccessOnly";

    private readonly IRegistryAccessor _registry;
    private readonly ContextMenuScanner _scanner;

    /// <summary>Creates a cleaner over explicit collaborators.</summary>
    public ContextMenuCleaner(IRegistryAccessor registry, ContextMenuScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scanner);
        _registry = registry;
        _scanner = scanner;
    }

    /// <summary>The script types whose menu is cleaned: enabled Run mappings, where "Runly: Düzenle" exists.</summary>
    public static IReadOnlyList<string> RunExtensions(RunlyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Extensions
            .Where(p => p.Value.Enabled && p.Value.Kind == HandlerKind.Run)
            .Select(p => RunlyRegistryLayout.NormalizeExtension(p.Key))
            .Where(e => e.Length > 1 && !RunlyRegistryLayout.IsBlockedExtension(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The ids the config asks to hide: the user's list, or the recommended ones when there is none.</summary>
    public static IReadOnlySet<string> DesiredIds(RunlyConfig config, IEnumerable<ContextMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(items);
        return config.HiddenMenuItems is { } ids
            ? new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(items.Where(i => i.Recommended).Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The ids the user asked to lose from every file type, not only Runly's (K32).</summary>
    public static IReadOnlySet<string> EverywhereIds(RunlyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.HiddenEverywhere is { } ids
            ? new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Lists the entries on the config's script types, marking the ones hidden right now.</summary>
    public IReadOnlyList<ContextMenuItem> Scan(RunlyConfig config)
    {
        var items = _scanner.Scan(RunExtensions(config));
        var ours = ReadAppliesToLedger().Concat(ReadLedger(VerbLedger))
            .Select(e => e.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blocked = ReadBlocked();

        return items
            .Select(i => i with
            {
                Hidden = i.Method == MenuHideMethod.AppliesTo
                    ? i.Keys.All(k => ours.Contains(ClassesKey(k)))
                    : i.Clsids.All(blocked.Contains),
            })
            .ToList();
    }

    /// <summary>Brings the registry in line with the config: hides what it asks for, restores the rest.</summary>
    public MenuCleanupResult Apply(RunlyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var actions = new List<string>();
        RevertAppliesTo();
        RevertVerbs();

        var items = _scanner.Scan(RunExtensions(config));
        var desired = DesiredIds(config, items);
        var everywhere = EverywhereIds(config);

        foreach (var item in items.Where(i => i.Method == MenuHideMethod.AppliesTo && desired.Contains(i.Id)))
        {
            // K32: an entry the user wants gone from every file cannot be expressed as an AppliesTo
            // clause over Runly's types; the verb itself is marked programmatic-access-only instead.
            if (everywhere.Contains(item.Id))
            {
                foreach (var key in item.Keys)
                {
                    HideVerbEverywhere(key);
                }

                actions.Add($"Sağ menüden gizlendi (tüm dosyalarda): {item.Label}");
                continue;
            }

            if (item.Extensions is not { Count: > 0 } extensions)
            {
                continue;
            }

            foreach (var key in item.Keys)
            {
                HideStatic(key, extensions);
            }

            actions.Add($"Sağ menüden gizlendi (yalnız {string.Join(", ", extensions)}): {item.Label}");
        }

        var wanted = items
            .Where(i => i.Method == MenuHideMethod.Blocked && desired.Contains(i.Id))
            .SelectMany(i => i.Clsids)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var clsid in ReadBlockedLedger().Where(c => !wanted.Contains(c)))
        {
            _registry.DeleteValue(RegistryRoot.CurrentUser, BlockedKey, clsid);
            _registry.DeleteValue(RegistryRoot.CurrentUser, BlockedLedger, clsid);
            changed = true;
        }

        var blocked = ReadBlocked();
        foreach (var clsid in wanted.Where(c => !blocked.Contains(c)))
        {
            _registry.SetValue(RegistryRoot.CurrentUser, BlockedKey, RegistryValueEntry.FromString(clsid, "Runly"));
            _registry.SetValue(RegistryRoot.CurrentUser, BlockedLedger, RegistryValueEntry.FromString(clsid, string.Empty));
            changed = true;
        }

        foreach (var item in items.Where(i => i.Method == MenuHideMethod.Blocked && desired.Contains(i.Id)))
        {
            var scope = item.Extensions is null ? "tüm dosyalarda" : "yalnız " + string.Join(", ", item.Extensions);
            actions.Add($"Sağ menüden gizlendi ({scope}): {item.Label}");
        }

        if (changed)
        {
            actions.Add("Engellenen menü eklentileri değişti; Explorer yeniden başlayınca görünür.");
        }

        return new MenuCleanupResult { Actions = actions, ExplorerRestartNeeded = changed };
    }

    /// <summary>Undoes every change Runly made to other programs' menu entries and drops the ledger.</summary>
    public MenuCleanupResult Revert()
    {
        var actions = new List<string>();
        var restored = RevertAppliesTo() + RevertVerbs();
        if (restored > 0)
        {
            actions.Add($"Sağ menüde gizlenen {restored} öğe geri getirildi.");
        }

        var changed = false;
        foreach (var clsid in ReadBlockedLedger())
        {
            _registry.DeleteValue(RegistryRoot.CurrentUser, BlockedKey, clsid);
            changed = true;
        }

        if (changed)
        {
            actions.Add("Runly'nin engellediği menü eklentileri serbest bırakıldı; Explorer yeniden başlayınca görünür.");
        }

        _registry.DeleteKeyTree(RegistryRoot.CurrentUser, LedgerKey);
        return new MenuCleanupResult { Actions = actions, ExplorerRestartNeeded = changed };
    }

    private void HideVerbEverywhere(string classesRelativeKey)
    {
        var key = ClassesKey(classesRelativeKey);
        var hadKey = _registry.KeyExists(RegistryRoot.CurrentUser, key);
        var original = hadKey ? _registry.GetValue(RegistryRoot.CurrentUser, key, HideValue)?.AsString() : null;

        var entry = VerbLedger + @"\" + _registry.GetSubKeyNames(RegistryRoot.CurrentUser, VerbLedger).Count;
        _registry.SetValue(RegistryRoot.CurrentUser, entry, RegistryValueEntry.FromString("Key", key));
        _registry.SetValue(RegistryRoot.CurrentUser, entry, RegistryValueEntry.FromDWord("HadKey", hadKey ? 1u : 0u));
        if (original is not null)
        {
            _registry.SetValue(RegistryRoot.CurrentUser, entry, RegistryValueEntry.FromString("Original", original));
        }

        _registry.SetValue(RegistryRoot.CurrentUser, key, RegistryValueEntry.FromString(HideValue, string.Empty));
    }

    private int RevertVerbs()
    {
        var entries = ReadLedger(VerbLedger);
        foreach (var (key, hadKey, original) in entries)
        {
            if (original is not null)
            {
                _registry.SetValue(RegistryRoot.CurrentUser, key, RegistryValueEntry.FromString(HideValue, original));
            }
            else
            {
                _registry.DeleteValue(RegistryRoot.CurrentUser, key, HideValue);
            }

            if (!hadKey &&
                _registry.KeyExists(RegistryRoot.CurrentUser, key) &&
                _registry.GetValues(RegistryRoot.CurrentUser, key).Count == 0 &&
                _registry.GetSubKeyNames(RegistryRoot.CurrentUser, key).Count == 0)
            {
                _registry.DeleteKeyTree(RegistryRoot.CurrentUser, key);
            }
        }

        _registry.DeleteKeyTree(RegistryRoot.CurrentUser, VerbLedger);
        return entries.Count;
    }

    private void HideStatic(string classesRelativeKey, IReadOnlyList<string> extensions)
    {
        var key = ClassesKey(classesRelativeKey);
        var hadKey = _registry.KeyExists(RegistryRoot.CurrentUser, key);
        var original = hadKey ? _registry.GetValue(RegistryRoot.CurrentUser, key, AppliesToValue)?.AsString() : null;
        var effective = _registry.GetValue(RegistryRoot.ClassesRoot, classesRelativeKey, AppliesToValue)?.AsString();

        var clause = "NOT (" + string.Join(" OR ", extensions.Select(e => "System.FileExtension:=" + e)) + ")";
        var combined = string.IsNullOrWhiteSpace(effective) ? clause : $"({effective}) AND {clause}";

        var entry = AppliesToLedger + @"\" + _registry.GetSubKeyNames(RegistryRoot.CurrentUser, AppliesToLedger).Count;
        _registry.SetValue(RegistryRoot.CurrentUser, entry, RegistryValueEntry.FromString("Key", key));
        _registry.SetValue(RegistryRoot.CurrentUser, entry, RegistryValueEntry.FromDWord("HadKey", hadKey ? 1u : 0u));
        if (original is not null)
        {
            _registry.SetValue(RegistryRoot.CurrentUser, entry, RegistryValueEntry.FromString("Original", original));
        }

        _registry.SetValue(RegistryRoot.CurrentUser, key, RegistryValueEntry.FromString(AppliesToValue, combined));
    }

    private int RevertAppliesTo()
    {
        var entries = ReadAppliesToLedger();
        foreach (var (key, hadKey, original) in entries)
        {
            if (original is not null)
            {
                _registry.SetValue(RegistryRoot.CurrentUser, key, RegistryValueEntry.FromString(AppliesToValue, original));
            }
            else
            {
                _registry.DeleteValue(RegistryRoot.CurrentUser, key, AppliesToValue);
            }

            if (!hadKey &&
                _registry.KeyExists(RegistryRoot.CurrentUser, key) &&
                _registry.GetValues(RegistryRoot.CurrentUser, key).Count == 0 &&
                _registry.GetSubKeyNames(RegistryRoot.CurrentUser, key).Count == 0)
            {
                _registry.DeleteKeyTree(RegistryRoot.CurrentUser, key);
            }
        }

        _registry.DeleteKeyTree(RegistryRoot.CurrentUser, AppliesToLedger);
        return entries.Count;
    }

    private List<(string Key, bool HadKey, string? Original)> ReadAppliesToLedger() => ReadLedger(AppliesToLedger);

    private List<(string Key, bool HadKey, string? Original)> ReadLedger(string ledgerKey)
    {
        var result = new List<(string, bool, string?)>();
        foreach (var name in _registry.GetSubKeyNames(RegistryRoot.CurrentUser, ledgerKey))
        {
            var entry = ledgerKey + @"\" + name;
            var key = _registry.GetValue(RegistryRoot.CurrentUser, entry, "Key")?.AsString();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var hadKey = _registry.GetValue(RegistryRoot.CurrentUser, entry, "HadKey")?.AsDWord() == 1;
            var original = _registry.GetValue(RegistryRoot.CurrentUser, entry, "Original")?.AsString();
            result.Add((key, hadKey, original));
        }

        return result;
    }

    private List<string> ReadBlockedLedger() =>
        _registry.GetValues(RegistryRoot.CurrentUser, BlockedLedger)
            .Select(v => ContextMenuScanner.NormalizeClsid(v.Name))
            .OfType<string>()
            .ToList();

    private HashSet<string> ReadBlocked() =>
        _registry.GetValues(RegistryRoot.CurrentUser, BlockedKey)
            .Select(v => ContextMenuScanner.NormalizeClsid(v.Name))
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string ClassesKey(string classesRelativeKey) => RunlyRegistryLayout.ClassesKey + @"\" + classesRelativeKey;
}
