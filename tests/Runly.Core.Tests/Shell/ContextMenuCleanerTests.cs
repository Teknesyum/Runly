using Runly.Core.Models;
using Runly.Core.Shell;

namespace Runly.Core.Tests.Shell;

public sealed class ContextMenuCleanerTests
{
    private const string Idle = "{C7E29CB0-9691-4DE8-B72B-6719DDC0B4A1}";
    private const string NppStatic = "{B298D29A-A6ED-11DE-BA8C-A68E55D89593}";
    private const string NppPackaged = "{E6950302-61F0-4FEB-97DB-855E30D4A991}";
    private const string VsCodeKey = @"Software\Classes\*\shell\VSCode";

    private readonly FakeRegistryAccessor _registry = new();
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public ContextMenuCleanerTests()
    {
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shell\VSCode", "", "Code ile aç");
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shell\VSCode\command", "", "\"C:\\VS Code\\Code.exe\" \"%1\"");
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shell\Scan", "MUIVerb", "&Tara");
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shell\Scan\command", "", @"C:\scan.exe %1");
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shell\Hidden", "Extended", "");
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shell\Hidden\command", "", @"C:\code.exe %1");
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shell\ANotepad++64", "ExplorerCommandHandler", NppStatic.ToLowerInvariant());

        SeedPackage("PythonSoftwareFoundation.PythonManager", (".py", Idle), (".pyw", Idle));
        SeedPackage("NotepadPlusPlus", ("*", NppPackaged));
        SeedPackage("Folders.Only", ("Directory", "{11111111-1111-1111-1111-111111111111}"));
        SeedPackage("Word.Thing", (".docx", "{22222222-2222-2222-2222-222222222222}"));
    }

    private void SeedPackage(string identity, params (string Type, string Clsid)[] verbs)
    {
        var root = @"C:\Packages\" + identity;
        _registry.Seed(RegistryRoot.ClassesRoot, ContextMenuScanner.PackagesKey + @"\" + identity + "_1.0_x64", "PackageRootFolder", root);

        var itemTypes = string.Concat(verbs.Select(v =>
            $"<desktop4:ItemType Type=\"{v.Type}\"><desktop4:Verb Id=\"v\" Clsid=\"{v.Clsid.Trim('{', '}')}\" /></desktop4:ItemType>"));

        _files[Path.Combine(root, "AppxManifest.xml")] =
            "<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\" " +
            "xmlns:desktop4=\"http://schemas.microsoft.com/appx/manifest/desktop/windows10/4\">" +
            $"<Identity Name=\"{identity}\" /><Properties><DisplayName>{identity}</DisplayName></Properties>" +
            "<Applications><Application><Extensions><desktop4:Extension Category=\"windows.fileExplorerContextMenus\">" +
            $"<desktop4:FileExplorerContextMenus>{itemTypes}</desktop4:FileExplorerContextMenus>" +
            "</desktop4:Extension></Extensions></Application></Applications></Package>";
    }

    private ContextMenuCleaner NewCleaner(bool defenderActive = true) =>
        new(_registry, new ContextMenuScanner(_registry, p => _files.GetValueOrDefault(p), _ => null, () => defenderActive));

    private static RunlyConfig Config(List<string>? hidden = null) => new()
    {
        HiddenMenuItems = hidden,
        Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
        {
            [".py"] = new() { Interpreter = "py", Enabled = true },
            [".js"] = new() { Interpreter = "node", Enabled = true },
            [".md"] = new() { Kind = HandlerKind.Open, OpenWith = "notepad", Enabled = true },
        },
    };

    private string? Blocked(string clsid) =>
        _registry.GetValue(RegistryRoot.CurrentUser, ContextMenuCleaner.BlockedKey, clsid)?.AsString();

    private const string Defender = "{09A47860-11B0-4DA5-AFA5-26D86198A780}";
    private const string OpenWith = "{09799AFB-AD67-11D1-ABCD-00C04FC30936}";

    private void SeedShellex()
    {
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shellex\ContextMenuHandlers\EPP", "", Defender);
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shellex\ContextMenuHandlers\Open With", "", OpenWith);
        _registry.Seed(RegistryRoot.ClassesRoot, @"CLSID\" + Defender, "", "Microsoft Defender ile tara");
    }

    [Fact]
    public void Classic_shellex_handlers_are_listed_but_never_Windows_own()
    {
        SeedShellex();

        var items = NewCleaner().Scan(Config()).ToDictionary(i => i.Id);

        Assert.Contains("group:defender", items.Keys);
        Assert.DoesNotContain("clsid:" + OpenWith, items.Keys);
        Assert.Equal(MenuHideMethod.Blocked, items["group:defender"].Method);
        Assert.False(items["group:defender"].Recommended);
        Assert.Null(items["group:defender"].Note);
    }

    [Fact]
    public void A_handler_with_no_name_but_its_own_guid_is_not_offered()
    {
        const string nameless = "{474C98EE-CF3D-41F5-80E3-4AAB0AB04301}";
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shellex\ContextMenuHandlers\" + nameless, "", nameless);

        Assert.DoesNotContain("clsid:" + nameless, NewCleaner().Scan(Config()).Select(i => i.Id));
    }

    [Fact]
    public void A_switched_off_Defender_makes_its_scan_entry_recommended()
    {
        SeedShellex();

        var defender = NewCleaner(defenderActive: false).Scan(Config()).Single(i => i.Id == "group:defender");

        Assert.True(defender.Recommended);
        Assert.Contains("gerçek zamanlı koruması kapalı", defender.Note);
    }

    [Fact]
    public void An_entry_marked_everywhere_loses_the_verb_on_every_file_and_comes_back()
    {
        var config = Config([@"static:*\shell\VSCode"]) with { HiddenEverywhere = [@"static:*\shell\VSCode"] };

        NewCleaner().Apply(config);

        Assert.Equal(string.Empty,
            _registry.GetValue(RegistryRoot.CurrentUser, VsCodeKey, "ProgrammaticAccessOnly")!.AsString());
        Assert.Null(_registry.GetValue(RegistryRoot.CurrentUser, VsCodeKey, "AppliesTo"));
        Assert.True(NewCleaner().Scan(config).Single(i => i.Id == @"static:*\shell\VSCode").Hidden);

        NewCleaner().Revert();

        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, VsCodeKey));
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, ContextMenuCleaner.LedgerKey));
    }

    [Fact]
    public void Dropping_the_everywhere_flag_narrows_the_entry_back_to_Runly_types()
    {
        var everywhere = Config([@"static:*\shell\VSCode"]) with { HiddenEverywhere = [@"static:*\shell\VSCode"] };
        NewCleaner().Apply(everywhere);
        NewCleaner().Apply(Config([@"static:*\shell\VSCode"]));

        Assert.Null(_registry.GetValue(RegistryRoot.CurrentUser, VsCodeKey, "ProgrammaticAccessOnly"));
        Assert.Equal("NOT (System.FileExtension:=.js OR System.FileExtension:=.py)",
            _registry.GetValue(RegistryRoot.CurrentUser, VsCodeKey, "AppliesTo")!.AsString());
    }

    [Fact]
    public void Scan_lists_static_and_packaged_entries_and_recommends_editor_duplicates()
    {
        var items = NewCleaner().Scan(Config()).ToDictionary(i => i.Id);

        Assert.Equal(["group:idle", "group:notepad++", @"static:*\shell\Scan", @"static:*\shell\VSCode"],
            items.Keys.Order(StringComparer.Ordinal));

        Assert.True(items[@"static:*\shell\VSCode"].Recommended);
        Assert.Equal([".js", ".py"], items[@"static:*\shell\VSCode"].Extensions!);
        Assert.False(items[@"static:*\shell\Scan"].Recommended);
        Assert.Equal("Tara", items[@"static:*\shell\Scan"].Label);

        Assert.True(items["group:idle"].Recommended);
        Assert.Equal([".py", ".pyw"], items["group:idle"].Extensions!);

        Assert.False(items["group:notepad++"].Recommended);
        Assert.Null(items["group:notepad++"].Extensions);
        Assert.Equal([NppStatic, NppPackaged], items["group:notepad++"].Clsids.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Default_apply_hides_only_the_recommended_entries()
    {
        var result = NewCleaner().Apply(Config());

        Assert.Equal("NOT (System.FileExtension:=.js OR System.FileExtension:=.py)",
            _registry.GetValue(RegistryRoot.CurrentUser, VsCodeKey, "AppliesTo")!.AsString());
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, @"Software\Classes\*\shell\Scan"));
        Assert.Equal("Runly", Blocked(Idle));
        Assert.Null(Blocked(NppStatic));
        Assert.True(result.ExplorerRestartNeeded);

        var scanned = NewCleaner().Scan(Config()).ToDictionary(i => i.Id);
        Assert.True(scanned[@"static:*\shell\VSCode"].Hidden);
        Assert.True(scanned["group:idle"].Hidden);
        Assert.False(scanned["group:notepad++"].Hidden);
    }

    [Fact]
    public void Apply_twice_changes_nothing_the_second_time()
    {
        NewCleaner().Apply(Config());
        var second = NewCleaner().Apply(Config());

        Assert.False(second.ExplorerRestartNeeded);
        Assert.Single(_registry.GetSubKeyNames(RegistryRoot.CurrentUser, ContextMenuCleaner.LedgerKey + @"\AppliesTo"));
        Assert.Equal("NOT (System.FileExtension:=.js OR System.FileExtension:=.py)",
            _registry.GetValue(RegistryRoot.CurrentUser, VsCodeKey, "AppliesTo")!.AsString());
    }

    [Fact]
    public void The_users_list_replaces_the_recommendation()
    {
        NewCleaner().Apply(Config());
        var result = NewCleaner().Apply(Config(["group:notepad++"]));

        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, VsCodeKey));
        Assert.Null(Blocked(Idle));
        Assert.Equal("Runly", Blocked(NppStatic));
        Assert.Equal("Runly", Blocked(NppPackaged));
        Assert.True(result.ExplorerRestartNeeded);
    }

    [Fact]
    public void An_existing_AppliesTo_is_kept_and_restored()
    {
        _registry.Seed(RegistryRoot.ClassesRoot, @"*\shell\VSCode", "AppliesTo", "System.Size:>0");
        _registry.Seed(RegistryRoot.CurrentUser, VsCodeKey, "AppliesTo", "System.Size:>0");

        NewCleaner().Apply(Config());
        Assert.Equal("(System.Size:>0) AND NOT (System.FileExtension:=.js OR System.FileExtension:=.py)",
            _registry.GetValue(RegistryRoot.CurrentUser, VsCodeKey, "AppliesTo")!.AsString());

        NewCleaner().Revert();
        Assert.Equal("System.Size:>0", _registry.GetValue(RegistryRoot.CurrentUser, VsCodeKey, "AppliesTo")!.AsString());
    }

    [Fact]
    public void Revert_removes_only_what_Runly_added()
    {
        _registry.Seed(RegistryRoot.CurrentUser, ContextMenuCleaner.BlockedKey, Idle, "user");
        _registry.Seed(RegistryRoot.CurrentUser, ContextMenuCleaner.BlockedKey, "{CB3D0F55-BC2C-4C1A-85ED-23ED75B5106B}", "");

        NewCleaner().Apply(Config(["group:idle", "group:notepad++", @"static:*\shell\VSCode"]));
        var result = NewCleaner().Revert();

        Assert.Equal("user", Blocked(Idle));
        Assert.NotNull(Blocked("{CB3D0F55-BC2C-4C1A-85ED-23ED75B5106B}"));
        Assert.Null(Blocked(NppStatic));
        Assert.Null(Blocked(NppPackaged));
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, VsCodeKey));
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, ContextMenuCleaner.LedgerKey));
        Assert.True(result.ExplorerRestartNeeded);
    }

    [Fact]
    public void Nothing_is_touched_without_a_script_type()
    {
        var config = Config() with
        {
            Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                [".md"] = new() { Kind = HandlerKind.Open, OpenWith = "notepad", Enabled = true },
            },
        };

        var result = NewCleaner().Apply(config);

        Assert.Empty(NewCleaner().Scan(config));
        Assert.False(result.ExplorerRestartNeeded);
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, VsCodeKey));
    }
}
