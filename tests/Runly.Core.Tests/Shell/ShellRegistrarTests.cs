using Runly.Core.Abstractions;
using Runly.Core.Defaults;
using Runly.Core.Models;
using Runly.Core.Shell;

namespace Runly.Core.Tests.Shell;

/// <summary>
/// Covers install, uninstall and status against the in-memory registry. T4 forbids automated tests that touch
/// the real registry, so the whole shell layer runs on <see cref="FakeRegistryAccessor"/>.
/// </summary>
public sealed class ShellRegistrarTests : IDisposable
{
    private const string StoreNotepadProgId = "AppXxf01pj590w7z9mxmyv3nx0a9ewj3e51g";
    private const string ExePath = @"C:\Program Files\Runly\Runly.exe";
    private const string ConsoleExePath = @"C:\Program Files\Runly\RunlyConsole.exe";

    private readonly FakeRegistryAccessor _registry = new();
    private readonly FakePathSearcher _paths = new();
    private readonly CountingNotifier _notifier = new();
    private readonly string _backupDir =
        Path.Combine(Path.GetTempPath(), "runly-shell-tests-" + Guid.NewGuid().ToString("N"));

    private readonly FakeEffectiveHandlerQuery _handlers = new();

    private ShellRegistrar NewRegistrar() =>
        new(_registry, _paths, new RegistryBackup(_registry, _backupDir), _notifier, _handlers);

    /// <summary>Puts an extension in the state the "Birlikte aç → Her zaman" flow leaves behind.</summary>
    private void SeedApprovedByUser(string extension)
    {
        _registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(extension),
            "ProgId", RunlyRegistryLayout.ProgIdFor(extension));
        _handlers.Set(extension, ExePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_backupDir))
        {
            Directory.Delete(_backupDir, recursive: true);
        }
    }

    /// <summary>The measured target machine: node, py and powershell present, everything else missing.</summary>
    private static RunlyConfig TargetMachineConfig() => DefaultConfig.Create();

    private void SeedTargetMachineInterpreters()
    {
        _paths.Add("node", @"C:\Program Files\nodejs\node.exe");
        _paths.Add("py", @"C:\Users\Administrator\AppData\Local\Microsoft\WindowsApps\py.exe");
        _paths.Add("powershell", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe");
    }

    [Fact]
    public void Install_takes_a_backup_before_writing_anything()
    {
        SeedTargetMachineInterpreters();

        var result = NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));

        var text = File.ReadAllText(result.BackupPath!);
        Assert.StartsWith("Windows Registry Editor Version 5.00", text, StringComparison.Ordinal);
        Assert.Contains(@"[-HKEY_CURRENT_USER\Software\Classes\Runly.Script.js]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Install_writes_the_whole_ProgID_tree_with_all_four_verbs()
    {
        SeedTargetMachineInterpreters();
        NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        const string progId = @"Software\Classes\Runly.Script.js";

        Assert.Equal("JS dosyası (Runly)",
            _registry.GetValue(RegistryRoot.CurrentUser, progId, "")!.AsString());
        Assert.Equal(@"C:\Program Files\Runly\assets\js.ico,0",
            _registry.GetValue(RegistryRoot.CurrentUser, progId + @"\DefaultIcon", "")!.AsString());

        Assert.Equal("\"C:\\Program Files\\Runly\\RunlyConsole.exe\" \"%1\" %*",
            _registry.GetValue(RegistryRoot.CurrentUser, progId + @"\shell\open\command", "")!.AsString());
        Assert.Equal("\"C:\\Program Files\\Runly\\RunlyConsole.exe\" --verb runas \"%1\" %*",
            _registry.GetValue(RegistryRoot.CurrentUser, progId + @"\shell\runas\command", "")!.AsString());
        Assert.Equal("\"C:\\Program Files\\Runly\\RunlyConsole.exe\" --verb edit \"%1\"",
            _registry.GetValue(RegistryRoot.CurrentUser, progId + @"\shell\edit\command", "")!.AsString());
        Assert.Equal("\"C:\\Program Files\\Runly\\RunlyConsole.exe\" --verb prompt-args \"%1\"",
            _registry.GetValue(RegistryRoot.CurrentUser, progId + @"\shell\runlyargs\command", "")!.AsString());
        Assert.Equal("Runly ile argümanlarla çalıştır…",
            _registry.GetValue(RegistryRoot.CurrentUser, progId + @"\shell\runlyargs", "MUIVerb")!.AsString());
    }

    [Fact]
    public void An_Open_mapping_gets_the_GUI_launcher_so_no_console_window_flashes()
    {
        // K29: this is the whole reason two binaries exist. A Kind=Open mapping hands the file to a desktop
        // application, so its double-click command must never go through the console-subsystem binary.
        _paths.Add("notepad++", @"C:\Program Files\Notepad++\notepad++.exe");
        var config = TargetMachineConfig() with
        {
            Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                [".md"] = new()
                {
                    Kind = HandlerKind.Open,
                    OpenWith = @"C:\Program Files\Notepad++\notepad++.exe",
                    Args = "\"{script}\"",
                    Enabled = true,
                },
            },
        };

        NewRegistrar().Install(config, ExePath, ConsoleExePath);

        Assert.Equal("\"C:\\Program Files\\Runly\\Runly.exe\" \"%1\" %*", _registry
            .GetValue(RegistryRoot.CurrentUser, @"Software\Classes\Runly.Script.md\shell\open\command", "")!.AsString());
        Assert.Null(_registry
            .GetValue(RegistryRoot.CurrentUser, @"Software\Classes\Runly.Script.md\shell\runas\command", ""));
    }

    [Fact]
    public void A_binding_that_resolves_to_the_console_launcher_still_counts_as_bound()
    {
        // K29: a Run mapping's effective handler is RunlyConsole.exe, not the Runly.exe that was installed.
        SeedTargetMachineInterpreters();
        _registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".js"), "ProgId", "Runly.Script.js");
        _handlers.Set(".js", ConsoleExePath);

        var status = NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath).Extensions
            .Single(e => e.Extension == ".js");

        Assert.Equal(BindingState.Bound, status.Bound);
    }

    [Fact]
    public void Install_registers_the_application_so_it_appears_in_the_Open_with_list()
    {
        SeedTargetMachineInterpreters();
        NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        Assert.Equal("Runly", _registry
            .GetValue(RegistryRoot.CurrentUser, @"Software\Classes\Applications\Runly.exe", "FriendlyAppName")!.AsString());
        Assert.NotNull(_registry
            .GetValue(RegistryRoot.CurrentUser, @"Software\Classes\Applications\Runly.exe\SupportedTypes", ".ps1"));
        Assert.Equal(@"Software\Runly\Capabilities", _registry
            .GetValue(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "Runly")!.AsString());
        Assert.Equal("Runly.Script.py", _registry
            .GetValue(RegistryRoot.CurrentUser, @"Software\Runly\Capabilities\FileAssociations", ".py")!.AsString());
    }

    [Fact]
    public void OpenWithProgids_is_always_written_even_when_UserChoice_blocks_the_binding()
    {
        SeedTargetMachineInterpreters();
        _registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".ps1"), "ProgId", StoreNotepadProgId);

        NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        Assert.NotNull(_registry.GetValue(
            RegistryRoot.CurrentUser, @"Software\Classes\.ps1\OpenWithProgids", "Runly.Script.ps1"));
    }

    [Fact]
    public void Install_without_a_UserChoice_writes_the_default_but_still_reports_NeedsUserChoice()
    {
        // Decision K19: writing .ext\(default) does not decide anything on Windows 11 when another candidate
        // sits under OpenWithProgids, so it may never be reported as "bağlandı".
        SeedTargetMachineInterpreters();
        _registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".ps1"), "ProgId", StoreNotepadProgId);

        var result = NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);
        var byExtension = result.Extensions.ToDictionary(e => e.Extension, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(BindingState.NeedsUserChoice, byExtension[".js"].Bound);
        Assert.Equal(BindingState.NeedsUserChoice, byExtension[".py"].Bound);
        Assert.Equal(BindingState.NeedsUserChoice, byExtension[".ps1"].Bound);
        Assert.Equal("bir Microsoft Store uygulaması", byExtension[".ps1"].UserChoiceOwnerName);

        Assert.Equal("Runly.Script.js",
            _registry.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.js", "")!.AsString());
    }

    [Fact]
    public void Install_reports_Bound_only_when_UserChoice_points_at_Runly()
    {
        SeedTargetMachineInterpreters();
        SeedApprovedByUser(".js");

        var result = NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);
        var byExtension = result.Extensions.ToDictionary(e => e.Extension, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(BindingState.Bound, byExtension[".js"].Bound);
        Assert.Equal(BindingState.NeedsUserChoice, byExtension[".py"].Bound);
    }

    [Fact]
    public void A_UserChoice_that_disagrees_with_the_effective_handler_is_reported_pessimistically()
    {
        // UserChoice says Runly, AssocQueryString says something else: never claim the optimistic side.
        SeedTargetMachineInterpreters();
        _registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".js"), "ProgId", "Runly.Script.js");
        _handlers.Set(".js", @"C:\Program Files\Antigravity\AntigravityIDE.exe");

        var status = NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath).Extensions
            .Single(e => e.Extension == ".js");

        Assert.Equal(BindingState.NeedsUserChoice, status.Bound);
        Assert.Equal("AntigravityIDE.exe", status.UserChoiceOwnerName);
    }

    [Fact]
    public void Install_never_claims_an_unapproved_extension_was_bound()
    {
        SeedTargetMachineInterpreters();

        var result = NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        var jsLine = result.Actions.Single(a => a.StartsWith(".js →", StringComparison.Ordinal));
        Assert.Contains("Windows onayı bekliyor", jsLine, StringComparison.Ordinal);
        Assert.DoesNotContain("bağlandı", jsLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_blocked_extension_default_value_is_left_untouched()
    {
        SeedTargetMachineInterpreters();
        _registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".ps1"), "ProgId", StoreNotepadProgId);
        _registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.ps1", "", "Microsoft.PowerShellScript.1");

        NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        Assert.Equal("Microsoft.PowerShellScript.1",
            _registry.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.ps1", "")!.AsString());
    }

    [Fact]
    public void Install_never_touches_the_UserChoice_key()
    {
        SeedTargetMachineInterpreters();
        var key = UserChoiceInspector.UserChoiceKey(".ps1");
        _registry.Seed(RegistryRoot.CurrentUser, key, "ProgId", StoreNotepadProgId);

        NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        Assert.Equal(StoreNotepadProgId, _registry.GetValue(RegistryRoot.CurrentUser, key, "ProgId")!.AsString());
        Assert.Null(_registry.GetValue(RegistryRoot.CurrentUser, key, "Hash"));
        Assert.Single(_registry.GetValues(RegistryRoot.CurrentUser, key));
    }

    [Fact]
    public void Install_never_writes_outside_HKCU()
    {
        SeedTargetMachineInterpreters();

        NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        Assert.Empty(_registry.RejectedWrites);
        Assert.Empty(_registry.AllKeys(RegistryRoot.ClassesRoot));
    }

    [Fact]
    public void Extensions_without_an_interpreter_are_skipped_with_a_reason()
    {
        SeedTargetMachineInterpreters();
        var config = TargetMachineConfig() with
        {
            Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                [".js"] = new() { Interpreter = "node", Args = "\"{script}\" {args}", Enabled = true },
                [".rb"] = new() { Interpreter = "ruby", Args = "\"{script}\" {args}", Enabled = true },
                [".lua"] = new() { Interpreter = "lua", Args = "\"{script}\" {args}", Enabled = false },
            },
        };

        var result = NewRegistrar().Install(config, ExePath, ConsoleExePath);

        Assert.Single(result.Extensions);
        Assert.Equal(".js", result.Extensions[0].Extension);

        var skipped = result.Skipped.ToDictionary(s => s.Extension, s => s.Reason, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ruby", skipped[".rb"], StringComparison.Ordinal);
        Assert.Contains("kapalı", skipped[".lua"], StringComparison.Ordinal);
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, @"Software\Classes\Runly.Script.rb"));
    }

    [Fact]
    public void An_absolute_interpreter_path_that_does_not_exist_is_skipped()
    {
        var config = TargetMachineConfig() with
        {
            Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                [".js"] = new() { Interpreter = @"C:\yok\node.exe", Args = "\"{script}\"", Enabled = true },
            },
        };

        var result = NewRegistrar().Install(config, ExePath, ConsoleExePath);

        Assert.Empty(result.Extensions);
        Assert.Single(result.Skipped);
    }

    [Fact]
    public void Install_notifies_Explorer_once()
    {
        SeedTargetMachineInterpreters();
        NewRegistrar().Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        Assert.Equal(1, _notifier.Count);
    }

    [Fact]
    public void Uninstall_removes_every_key_Runly_wrote()
    {
        SeedTargetMachineInterpreters();
        var registrar = NewRegistrar();
        registrar.Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        var result = registrar.Uninstall();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(_registry.AllKeys(RegistryRoot.CurrentUser),
            k => k.Contains("Runly", StringComparison.OrdinalIgnoreCase));
        Assert.Null(_registry.GetValue(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "Runly"));
        Assert.Null(_registry.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.js\OpenWithProgids", "Runly.Script.js"));
        Assert.Null(_registry.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.js", ""));
    }

    [Fact]
    public void Uninstall_also_clears_the_Applications_key_Windows_wrote_for_the_console_launcher()
    {
        // K29: install only ever writes Applications\Runly.exe, but Windows creates the RunlyConsole.exe
        // key the moment the user picks that binary from "Open with". Leaving it behind points a live
        // registry key at a deleted executable, which is the B2/K24 failure this uninstall must not repeat.
        SeedTargetMachineInterpreters();
        var registrar = NewRegistrar();
        registrar.Install(TargetMachineConfig(), ExePath, ConsoleExePath);
        _registry.Seed(RegistryRoot.CurrentUser,
            @"Software\Classes\Applications\RunlyConsole.exe\shell\open\command", "", $"\"{ConsoleExePath}\" \"%1\"");

        var result = registrar.Uninstall();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, @"Software\Classes\Applications\RunlyConsole.exe"));
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, @"Software\Classes\Applications\Runly.exe"));
    }

    [Fact]
    public void Uninstall_leaves_other_applications_alone()
    {
        SeedTargetMachineInterpreters();
        _registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js\OpenWithProgids", "VSCode.js", "");
        _registry.Seed(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "Notepad++", @"Software\Notepad++\Capabilities");

        var registrar = NewRegistrar();
        registrar.Install(TargetMachineConfig(), ExePath, ConsoleExePath);
        registrar.Uninstall();

        Assert.NotNull(_registry.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.js\OpenWithProgids", "VSCode.js"));
        Assert.NotNull(_registry.GetValue(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "Notepad++"));
    }

    [Fact]
    public void Uninstall_does_not_restore_the_backup_by_default()
    {
        // SPEC 9: .js used to point at WScript.exe, which was a bad default; the user has to ask for it back.
        _paths.Add("node", @"C:\Program Files\nodejs\node.exe");
        _registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js", "", "JSFile");

        var config = TargetMachineConfig() with
        {
            Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                [".js"] = new() { Interpreter = "node", Args = "\"{script}\"", Enabled = true },
            },
        };

        var registrar = NewRegistrar();
        registrar.Install(config, ExePath, ConsoleExePath);

        var result = registrar.Uninstall();

        Assert.Null(result.RestoredBackupPath);
        Assert.Null(_registry.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.js", ""));
    }

    [Fact]
    public void Uninstall_with_RestoreBackup_brings_the_previous_association_back()
    {
        _paths.Add("node", @"C:\Program Files\nodejs\node.exe");
        _registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js", "", "JSFile");

        var config = TargetMachineConfig() with
        {
            Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                [".js"] = new() { Interpreter = "node", Args = "\"{script}\"", Enabled = true },
            },
        };

        var registrar = NewRegistrar();
        registrar.Install(config, ExePath, ConsoleExePath);

        var result = registrar.Uninstall(new UninstallOptions { RestoreBackup = true });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.RestoredBackupPath);
        Assert.Equal("JSFile", _registry.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.js", "")!.AsString());
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, @"Software\Classes\Runly.Script.js"));
    }

    [Fact]
    public void Uninstall_on_a_clean_machine_succeeds_quietly()
    {
        var result = NewRegistrar().Uninstall();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Empty(result.RemovedKeys);
    }

    [Fact]
    public void GetStatus_reports_every_configured_extension()
    {
        SeedTargetMachineInterpreters();
        var config = TargetMachineConfig();

        var statuses = NewRegistrar().GetStatus(config);

        Assert.Equal(config.Extensions.Count, statuses.Count);
        Assert.All(statuses, s => Assert.Equal(BindingState.NotBound, s.Bound));

        var byExtension = statuses.ToDictionary(s => s.Extension, StringComparer.OrdinalIgnoreCase);
        Assert.True(byExtension[".js"].InterpreterFound);
        Assert.Equal(@"C:\Program Files\nodejs\node.exe", byExtension[".js"].InterpreterPath);
        Assert.False(byExtension[".rb"].InterpreterFound);
        Assert.Null(byExtension[".rb"].InterpreterPath);
    }

    [Fact]
    public void GetStatus_after_install_matches_the_target_machine_expectation()
    {
        SeedTargetMachineInterpreters();
        _registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".ps1"), "ProgId", StoreNotepadProgId);

        var registrar = NewRegistrar();
        var config = TargetMachineConfig();
        registrar.Install(config, ExePath, ConsoleExePath);

        var byExtension = registrar.GetStatus(config)
            .ToDictionary(s => s.Extension, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(BindingState.NeedsUserChoice, byExtension[".js"].Bound);
        Assert.Equal(BindingState.NeedsUserChoice, byExtension[".py"].Bound);
        Assert.Equal(BindingState.NeedsUserChoice, byExtension[".ps1"].Bound);
        Assert.Equal("bir Microsoft Store uygulaması", byExtension[".ps1"].UserChoiceOwnerName);

        // Disabled extensions were never installed.
        Assert.Equal(BindingState.NotBound, byExtension[".rb"].Bound);
        Assert.Null(byExtension[".rb"].UserChoiceOwnerName);
    }

    [Fact]
    public void GetStatus_reports_Bound_when_Windows_UserChoice_already_points_at_Runly()
    {
        // This is the state after the user completes the "Open with → always" flow for .ps1.
        SeedTargetMachineInterpreters();
        var registrar = NewRegistrar();
        var config = TargetMachineConfig();
        registrar.Install(config, ExePath, ConsoleExePath);

        _registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".ps1"), "ProgId", "Runly.Script.ps1");

        var status = registrar.GetStatus(config).Single(s => s.Extension == ".ps1");

        Assert.Equal(BindingState.Bound, status.Bound);
        Assert.Null(status.UserChoiceOwnerName);
    }

    [Fact]
    public void GetStatus_reports_NotBound_when_another_application_owns_an_uninstalled_extension()
    {
        SeedTargetMachineInterpreters();
        _registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".ps1"), "ProgId", StoreNotepadProgId);

        var status = NewRegistrar().GetStatus(TargetMachineConfig()).Single(s => s.Extension == ".ps1");

        Assert.Equal(BindingState.NotBound, status.Bound);
    }

    [Fact]
    public void Installing_twice_is_idempotent()
    {
        SeedTargetMachineInterpreters();
        var registrar = NewRegistrar();
        var config = TargetMachineConfig();

        var first = registrar.Install(config, ExePath, ConsoleExePath);
        var keysAfterFirst = _registry.AllKeys(RegistryRoot.CurrentUser);

        var second = registrar.Install(config, ExePath, ConsoleExePath);

        Assert.True(second.Success, second.ErrorMessage);
        Assert.Equal(keysAfterFirst, _registry.AllKeys(RegistryRoot.CurrentUser));
        Assert.Equal(
            first.Extensions.Select(e => e.Bound),
            second.Extensions.Select(e => e.Bound));
    }

    [Fact]
    public void Uninstall_lists_an_extension_whose_UserChoice_Windows_refuses_to_release()
    {
        // Decision K20: the ACL protected key survives, so the extension is left pointing at a deleted ProgID.
        SeedTargetMachineInterpreters();
        var registrar = NewRegistrar();
        registrar.Install(TargetMachineConfig(), ExePath, ConsoleExePath);
        SeedApprovedByUser(".js");
        _registry.UndeletableKeys.Add(UserChoiceInspector.UserChoiceKey(".js"));

        var result = registrar.Uninstall();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.HasOrphanedUserChoices);

        var orphan = Assert.Single(result.AffectedUserChoices);
        Assert.Equal(".js", orphan.Extension);
        Assert.Equal("Runly.Script.js", orphan.ProgId);
        Assert.False(orphan.Removed);
        Assert.Contains("Erişim engellendi", orphan.FailureReason!, StringComparison.Ordinal);
        Assert.Contains(result.Actions, a => a.Contains("geçersiz bir uygulamaya bağlı kaldı", StringComparison.Ordinal));
    }

    [Fact]
    public void Uninstall_removes_our_own_UserChoice_when_Windows_allows_it()
    {
        SeedTargetMachineInterpreters();
        var registrar = NewRegistrar();
        registrar.Install(TargetMachineConfig(), ExePath, ConsoleExePath);
        SeedApprovedByUser(".js");

        var result = registrar.Uninstall();

        var entry = Assert.Single(result.AffectedUserChoices);
        Assert.True(entry.Removed);
        Assert.False(result.HasOrphanedUserChoices);
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".js")));
    }

    [Fact]
    public void Uninstall_leaves_another_applications_UserChoice_completely_alone()
    {
        SeedTargetMachineInterpreters();
        var key = UserChoiceInspector.UserChoiceKey(".ps1");
        _registry.Seed(RegistryRoot.CurrentUser, key, "ProgId", StoreNotepadProgId);

        var registrar = NewRegistrar();
        registrar.Install(TargetMachineConfig(), ExePath, ConsoleExePath);

        var result = registrar.Uninstall();

        Assert.Empty(result.AffectedUserChoices);
        Assert.Equal(StoreNotepadProgId, _registry.GetValue(RegistryRoot.CurrentUser, key, "ProgId")!.AsString());
    }

    [Fact]
    public void Install_RefusesBlockedSystemExtensionsEvenWhenEnabled()
    {
        var config = DefaultConfig.Create() with
        {
            Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                [".exe"] = new() { Kind = HandlerKind.Open, OpenWith = ExePath, Args = "\"{script}\"", Enabled = true },
            },
        };

        var result = NewRegistrar().Install(config, ExePath, ConsoleExePath);

        Assert.Empty(result.Extensions);
        Assert.Contains(result.Skipped, item => item.Extension == ".exe" && item.Reason.Contains("güvenliği", StringComparison.Ordinal));
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, RunlyRegistryLayout.ProgIdKey(".exe")));
    }

    [Fact]
    public void Install_OpenMappingWritesOnlyOpenVerb()
    {
        var handler = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        var config = DefaultConfig.Create() with
        {
            Extensions = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                [".md"] = new() { Kind = HandlerKind.Open, Category = "text", TypeName = "Markdown belgesi", OpenWith = handler, Args = "\"{script}\" {args}", Enabled = true },
            },
        };

        var result = NewRegistrar().Install(config, ExePath, ConsoleExePath);

        Assert.True(result.Success, result.ErrorMessage);
        var shell = RunlyRegistryLayout.ProgIdKey(".md") + @"\shell";
        Assert.True(_registry.KeyExists(RegistryRoot.CurrentUser, shell + @"\open"));
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, shell + @"\runas"));
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, shell + @"\edit"));
        Assert.False(_registry.KeyExists(RegistryRoot.CurrentUser, shell + @"\runlyargs"));
        Assert.Equal("Markdown belgesi (Runly)",
            _registry.GetValue(RegistryRoot.CurrentUser, RunlyRegistryLayout.ProgIdKey(".md"), "")!.AsString());
    }

    /// <summary>Stands in for Windows' own association resolution (<c>AssocQueryString</c>).</summary>
    private sealed class FakeEffectiveHandlerQuery : IEffectiveHandlerQuery
    {
        private readonly Dictionary<string, string> _handlers = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string extension, string exePath) => _handlers[extension] = exePath;

        public string? GetExecutable(string extension) => _handlers.GetValueOrDefault(extension);
    }

    /// <summary>Stands in for T2's PATH scanner; T4 injects the interface and never implements it.</summary>
    private sealed class FakePathSearcher : IPathSearcher
    {
        private readonly Dictionary<string, string> _known = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string name, string path) => _known[name] = path;

        public string? Find(string exeName) => _known.GetValueOrDefault(exeName);
    }

    private sealed class CountingNotifier : IShellNotifier
    {
        public int Count { get; private set; }

        public void AssociationsChanged() => Count++;
    }
}
