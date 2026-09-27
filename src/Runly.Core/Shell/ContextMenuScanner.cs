using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Runly.Core.Shell;

/// <summary>
/// Finds the context-menu entries other programs put on Runly's file types: static verbs under
/// <c>*</c>, <c>AllFilesystemObjects</c> and <c>SystemFileAssociations</c>, and the handlers packaged apps
/// declare in their manifest (<c>desktop4:FileExplorerContextMenus</c>).
/// </summary>
public sealed class ContextMenuScanner
{
    /// <summary>HKCR-relative key under which every installed package records its root folder.</summary>
    public const string PackagesKey = @"Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    private static readonly string[] AllFileShellKeys = [@"*\shell", @"AllFilesystemObjects\shell"];

    private static readonly string[] AllFileShellexKeys =
        [@"*\shellex\ContextMenuHandlers", @"AllFilesystemObjects\shellex\ContextMenuHandlers"];

    private static readonly (string Id, string Label, string[] Clsids)[] KnownGroups =
    [
        ("notepad++", "Edit with Notepad++", ["{B298D29A-A6ED-11DE-BA8C-A68E55D89593}", "{E6950302-61F0-4FEB-97DB-855E30D4A991}"]),
        ("windows-notepad", "Not Defteri'nde düzenle", ["{CA6CC9F1-867A-481E-951E-A28C5E4F01EA}"]),
        ("idle", "Edit in IDLE", ["{C7E29CB0-9691-4DE8-B72B-6719DDC0B4A1}"]),
        ("defender", "Microsoft Defender ile tara", ["{09A47860-11B0-4DA5-AFA5-26D86198A780}"]),
        ("file-locksmith", "File Locksmith ile kilidi aç", ["{84D68575-E186-46AD-B0CB-BAEB45EE29C0}"]),
    ];

    /// <summary>
    /// Handlers the menu cannot lose without losing function: Windows' own sharing, send-to, pinning and
    /// "Open with" live here. They are not offered, so the preview cannot talk the user into breaking Explorer.
    /// </summary>
    private static readonly HashSet<string> ProtectedClsids = new(StringComparer.OrdinalIgnoreCase)
    {
        "{09799AFB-AD67-11D1-ABCD-00C04FC30936}",
        "{7BA4C740-9E81-11CF-99D3-00AA004AE837}",
        "{F81E9010-6EA4-11CE-A7FF-00AA003CA9F6}",
        "{E2BF9676-5F8F-435C-97EB-11607A5BEDF7}",
        "{90AA3A4E-1CBA-4233-B8BB-535773D48449}",
        "{A2A9545D-A0C2-42B4-9708-A0B2BADD77C8}",
        "{F3D06E7C-1E45-4A26-847E-F9FCDEE59BE0}",
        "{A470F8CF-A1E8-4F65-8335-227475AA5C46}",
        "{A2A9545D-A0C2-42B4-9708-A0B2BADD77C9}",
        "{596AB062-B4D2-4215-9F74-E9109B0A8153}",
        "{7AD84985-87B4-4A16-BE58-8B72A5B390F7}",
    };

    /// <summary>Package identities whose own name reads better in the menu than the manifest's display name.</summary>
    private static readonly (string Fragment, string Id, string Label)[] KnownPackages =
    [
        ("PowerRename", "powertoys-rename", "PowerRename ile yeniden adlandır"),
        ("FileLocksmith", "file-locksmith", "File Locksmith ile kilidi aç"),
        ("ImageResizer", "powertoys-resize", "Resimleri yeniden boyutlandır"),
    ];

    private static readonly HashSet<string> KnownEditors = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "code - insiders", "cursor", "windsurf", "zed", "notepad", "notepad++", "sublime_text", "subl",
        "atom", "idle", "idle3", "powershell_ise", "pycharm64", "pycharm", "idea64", "webstorm64", "phpstorm64", "rider64",
        "fleet", "kate", "gvim", "vim", "nvim-qt", "emeditor", "textpad", "ultraedit", "uedit64", "notepad2", "notepad3",
    };

    private readonly IRegistryAccessor _registry;
    private readonly Func<string, string?> _readFile;
    private readonly Func<string, string?> _resolveIndirect;
    private readonly Func<bool> _defenderActive;

    /// <summary>Creates a scanner; tests pass their own manifest reader, label resolver and Defender state.</summary>
    public ContextMenuScanner(
        IRegistryAccessor registry,
        Func<string, string?>? readFile = null,
        Func<string, string?>? resolveIndirect = null,
        Func<bool>? defenderActive = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _readFile = readFile ?? ReadFileOrNull;
        _resolveIndirect = resolveIndirect ?? ResolveIndirectString;
        _defenderActive = defenderActive ?? (() => DefenderState.IsActive(registry));
    }

    /// <summary>Lists what can be hidden on <paramref name="extensions"/>, Runly's own script types.</summary>
    public IReadOnlyList<ContextMenuItem> Scan(IReadOnlyCollection<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        if (extensions.Count == 0)
        {
            return [];
        }

        var groups = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
        var clsidToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (id, label, clsids) in KnownGroups)
        {
            foreach (var clsid in clsids)
            {
                clsidToGroup[clsid] = "group:" + id;
            }

            groups["group:" + id] = new Group("group:" + id, label, isEditor: true);
        }

        ScanPackages(extensions, groups, clsidToGroup);

        var statics = new List<ContextMenuItem>();
        foreach (var shellKey in AllFileShellKeys)
        {
            ScanStatic(shellKey, extensions.ToList(), extensions, statics, groups, clsidToGroup, everyFile: true);
        }

        foreach (var extension in extensions)
        {
            ScanStatic($@"SystemFileAssociations\{extension}\shell", [extension], extensions, statics, groups, clsidToGroup, everyFile: false);
        }

        foreach (var shellexKey in AllFileShellexKeys)
        {
            ScanShellex(shellexKey, null, groups, clsidToGroup);
        }

        foreach (var extension in extensions)
        {
            ScanShellex($@"SystemFileAssociations\{extension}\shellex\ContextMenuHandlers", [extension], groups, clsidToGroup);
        }

        var defenderOff = !_defenderActive();

        var blocked = groups.Values
            .Where(g => g.Clsids.Count > 0)
            .Select(g => g.ToItem(extensions))
            .Select(i => i.Id == "group:defender" && defenderOff
                ? i with { Recommended = true, Note = "Defender'ın gerçek zamanlı koruması kapalı; bu girdi iş görmüyor." }
                : i);

        return statics.Concat(blocked)
            .OrderBy(i => i.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void ScanStatic(
        string shellKey,
        IReadOnlyList<string> scope,
        IReadOnlyCollection<string> runExtensions,
        List<ContextMenuItem> statics,
        Dictionary<string, Group> groups,
        Dictionary<string, string> clsidToGroup,
        bool everyFile)
    {
        foreach (var verb in _registry.GetSubKeyNames(RegistryRoot.ClassesRoot, shellKey))
        {
            var key = shellKey + @"\" + verb;
            var values = _registry.GetValues(RegistryRoot.ClassesRoot, key);

            if (Has(values, "ProgrammaticAccessOnly") || Has(values, "LegacyDisable") || Has(values, "Extended"))
            {
                continue;
            }

            var label = LabelFor(values, verb);
            var handler = NormalizeClsid(Text(values, "ExplorerCommandHandler"));

            if (handler is not null)
            {
                var groupId = clsidToGroup.GetValueOrDefault(handler) ?? "clsid:" + handler;
                if (!groups.TryGetValue(groupId, out var group))
                {
                    group = new Group(groupId, label, isEditor: false);
                    groups[groupId] = group;
                }

                group.Add(handler, everyFile ? null : scope, "HKCR\\" + key);
                continue;
            }

            var commandKey = key + @"\command";
            var hasCommand = _registry.KeyExists(RegistryRoot.ClassesRoot, commandKey);
            if (!hasCommand && !Has(values, "SubCommands") && !Has(values, "ExtendedSubCommandsKey"))
            {
                continue;
            }

            var command = hasCommand
                ? _registry.GetValue(RegistryRoot.ClassesRoot, commandKey, RegistryValueEntry.DefaultValueName)?.AsString()
                : null;
            var isEditor = IsEditorCommand(command);

            statics.Add(new ContextMenuItem
            {
                Id = "static:" + key,
                Label = label,
                Method = MenuHideMethod.AppliesTo,
                Source = "HKCR\\" + key,
                Extensions = scope,
                Keys = [key],
                IsEditor = isEditor,
                Recommended = isEditor && scope.Any(e => runExtensions.Contains(e, StringComparer.OrdinalIgnoreCase)),
            });
        }
    }

    /// <summary>
    /// Reads the classic <c>shellex\ContextMenuHandlers</c> handlers. The static-verb scan never saw these,
    /// which is why "Microsoft Defender ile tara" and WinRAR were missing from the list; they are hidden the
    /// same way packaged handlers are, by CLSID.
    /// </summary>
    private void ScanShellex(
        string handlersKey,
        IReadOnlyList<string>? scope,
        Dictionary<string, Group> groups,
        Dictionary<string, string> clsidToGroup)
    {
        foreach (var name in _registry.GetSubKeyNames(RegistryRoot.ClassesRoot, handlersKey))
        {
            var key = handlersKey + @"\" + name;
            var clsid = NormalizeClsid(_registry.GetValue(RegistryRoot.ClassesRoot, key, RegistryValueEntry.DefaultValueName)?.AsString())
                        ?? NormalizeClsid(name);

            if (clsid is null || ProtectedClsids.Contains(clsid))
            {
                continue;
            }

            var groupId = clsidToGroup.GetValueOrDefault(clsid) ?? "clsid:" + clsid;
            clsidToGroup[clsid] = groupId;

            if (!groups.TryGetValue(groupId, out var group))
            {
                // A handler whose name resolves to nothing but its own GUID cannot be recognised in the
                // menu either, so offering it would be a toggle over an unknown entry.
                if (ShellexLabel(clsid, name) is not { } label)
                {
                    continue;
                }

                group = new Group(groupId, label, isEditor: false);
                groups[groupId] = group;
            }

            group.Add(clsid, scope, "HKCR\\" + key);
        }
    }

    private string? ShellexLabel(string clsid, string keyName)
    {
        var raw = _registry.GetValue(RegistryRoot.ClassesRoot, @"CLSID\" + clsid, RegistryValueEntry.DefaultValueName)?.AsString();
        if (!string.IsNullOrWhiteSpace(raw) && raw.StartsWith('@'))
        {
            raw = _resolveIndirect(raw);
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.Trim();
        }

        return NormalizeClsid(keyName) is null ? keyName : null;
    }

    private void ScanPackages(
        IReadOnlyCollection<string> runExtensions,
        Dictionary<string, Group> groups,
        Dictionary<string, string> clsidToGroup)
    {
        foreach (var package in _registry.GetSubKeyNames(RegistryRoot.ClassesRoot, PackagesKey))
        {
            var root = _registry.GetValue(RegistryRoot.ClassesRoot, PackagesKey + @"\" + package, "PackageRootFolder")?.AsString();
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var xml = _readFile(Path.Combine(root, "AppxManifest.xml"));
            if (xml is null || !xml.Contains("FileExplorerContextMenus", StringComparison.Ordinal))
            {
                continue;
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(xml);
            }
            catch (XmlException)
            {
                continue;
            }

            var identity = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Identity")?.Attribute("Name")?.Value ?? package;
            var displayName = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Properties")
                ?.Elements().FirstOrDefault(e => e.Name.LocalName == "DisplayName")?.Value;
            var packageLabel = string.IsNullOrWhiteSpace(displayName) || displayName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)
                ? identity
                : displayName.Trim();

            var typesByClsid = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var menus in document.Descendants().Where(e => e.Name.LocalName == "FileExplorerContextMenus"))
            {
                foreach (var itemType in menus.Elements().Where(e => e.Name.LocalName == "ItemType"))
                {
                    var type = itemType.Attribute("Type")?.Value?.Trim();
                    if (string.IsNullOrEmpty(type))
                    {
                        continue;
                    }

                    foreach (var verb in itemType.Elements().Where(e => e.Name.LocalName == "Verb"))
                    {
                        var clsid = NormalizeClsid(verb.Attribute("Clsid")?.Value);
                        if (clsid is null)
                        {
                            continue;
                        }

                        if (!typesByClsid.TryGetValue(clsid, out var types))
                        {
                            types = [];
                            typesByClsid[clsid] = types;
                        }

                        types.Add(type);
                    }
                }
            }

            foreach (var (clsid, types) in typesByClsid)
            {
                var fileTypes = types.Where(t => t == "*" || t.StartsWith('.')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (fileTypes.Count == 0)
                {
                    continue;
                }

                var everyFile = fileTypes.Contains("*");
                if (!everyFile && !fileTypes.Any(t => runExtensions.Contains(t, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var known = KnownPackages.FirstOrDefault(p => identity.Contains(p.Fragment, StringComparison.OrdinalIgnoreCase));
                var groupId = clsidToGroup.GetValueOrDefault(clsid)
                              ?? (known.Id is null ? "package:" + identity : "group:" + known.Id);
                clsidToGroup[clsid] = groupId;
                if (!groups.TryGetValue(groupId, out var group))
                {
                    group = new Group(groupId, known.Label ?? packageLabel, isEditor: false);
                    groups[groupId] = group;
                }

                group.Add(clsid, everyFile ? null : fileTypes.Select(t => t.ToLowerInvariant()).ToList(), "Paket: " + identity);
            }
        }
    }

    private string LabelFor(IReadOnlyList<RegistryValueEntry> values, string verb)
    {
        var raw = Text(values, "MUIVerb");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = Text(values, RegistryValueEntry.DefaultValueName);
        }

        if (!string.IsNullOrWhiteSpace(raw) && raw.StartsWith('@'))
        {
            raw = _resolveIndirect(raw);
        }

        return string.IsNullOrWhiteSpace(raw) ? verb : raw.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static bool IsEditorCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var text = command.Trim();
        string exe;
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            exe = end > 0 ? text[1..end] : text[1..];
        }
        else
        {
            var space = text.IndexOf(' ', StringComparison.Ordinal);
            exe = space > 0 ? text[..space] : text;
        }

        try
        {
            return KnownEditors.Contains(Path.GetFileNameWithoutExtension(exe));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool Has(IReadOnlyList<RegistryValueEntry> values, string name) =>
        values.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string? Text(IReadOnlyList<RegistryValueEntry> values, string name) =>
        values.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))?.AsString();

    /// <summary>Upper-cases a CLSID and puts it in braces; returns <see langword="null"/> for anything that is not a GUID.</summary>
    public static string? NormalizeClsid(string? value) =>
        Guid.TryParse(value?.Trim(), out var guid) ? guid.ToString("B").ToUpperInvariant() : null;

    private static string? ReadFileOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ResolveIndirectString(string source)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var buffer = new StringBuilder(512);
        return SHLoadIndirectString(source, buffer, buffer.Capacity, IntPtr.Zero) == 0 ? buffer.ToString() : null;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHLoadIndirectString(string source, StringBuilder output, int outputSize, IntPtr reserved);

    private sealed class Group(string id, string label, bool isEditor)
    {
        private readonly List<string> _sources = [];
        private List<string>? _extensions = [];

        public HashSet<string> Clsids { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string clsid, IReadOnlyList<string>? extensions, string source)
        {
            Clsids.Add(clsid);
            if (!_sources.Contains(source, StringComparer.OrdinalIgnoreCase))
            {
                _sources.Add(source);
            }

            if (extensions is null)
            {
                _extensions = null;
            }
            else if (_extensions is not null)
            {
                foreach (var extension in extensions)
                {
                    if (!_extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        _extensions.Add(extension);
                    }
                }
            }
        }

        public ContextMenuItem ToItem(IReadOnlyCollection<string> runExtensions) => new()
        {
            Id = id,
            Label = label,
            Method = MenuHideMethod.Blocked,
            Source = string.Join(" · ", _sources),
            Extensions = _extensions,
            Clsids = Clsids.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            IsEditor = isEditor,
            Recommended = isEditor && _extensions is not null &&
                          _extensions.Any(e => runExtensions.Contains(e, StringComparer.OrdinalIgnoreCase)),
        };
    }
}
