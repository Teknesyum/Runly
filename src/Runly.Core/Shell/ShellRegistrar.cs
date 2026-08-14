using System.Runtime.Versioning;
using Runly.Core.Abstractions;
using Runly.Core.Models;

namespace Runly.Core.Shell;

/// <summary>
/// Writes, inspects and removes Runly's HKCU file associations (SPEC 9). Everything lives under
/// <c>HKEY_CURRENT_USER</c>, so no elevation is ever required, and every run takes a registry backup first.
/// </summary>
public sealed class ShellRegistrar : IShellRegistrar
{
    private readonly IRegistryAccessor _registry;
    private readonly IPathSearcher _pathSearcher;
    private readonly RegistryBackup _backup;
    private readonly IShellNotifier _notifier;
    private readonly UserChoiceInspector _userChoice;
    private readonly IEffectiveHandlerQuery _effectiveHandler;

    /// <summary>Creates a registrar over explicit collaborators; this is the constructor tests use.</summary>
    public ShellRegistrar(
        IRegistryAccessor registry,
        IPathSearcher pathSearcher,
        RegistryBackup backup,
        IShellNotifier notifier,
        IEffectiveHandlerQuery? effectiveHandler = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(pathSearcher);
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(notifier);

        _registry = registry;
        _pathSearcher = pathSearcher;
        _backup = backup;
        _notifier = notifier;
        _userChoice = new UserChoiceInspector(registry);
        _effectiveHandler = effectiveHandler ?? UnknownEffectiveHandlerQuery.Instance;
    }

    /// <summary>Creates a registrar wired to the real registry, the real backup folder and Explorer.</summary>
    [SupportedOSPlatform("windows")]
    public ShellRegistrar(IPathSearcher pathSearcher)
        : this(
            new Win32RegistryAccessor(),
            pathSearcher,
            new RegistryBackup(new Win32RegistryAccessor()),
            new Win32ShellNotifier(),
            new Win32EffectiveHandlerQuery())
    {
    }

    /// <inheritdoc />
    public InstallResult Install(RunlyConfig config, string exePath)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);

        var actions = new List<string>();
        var skipped = new List<SkippedExtension>();
        var statuses = new List<ExtensionStatus>();

        try
        {
            var installDir = Path.GetDirectoryName(Path.GetFullPath(exePath));
            if (string.IsNullOrEmpty(installDir))
            {
                throw new ArgumentException($"'{exePath}' bir klasör içermiyor.", nameof(exePath));
            }

            // 1. Work out which extensions can actually be installed.
            var candidates = new List<(string Extension, ExtensionMapping Mapping, string InterpreterPath)>();

            foreach (var (rawExtension, mapping) in config.Extensions.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
            {
                var extension = RunlyRegistryLayout.NormalizeExtension(rawExtension);

                if (!mapping.Enabled)
                {
                    skipped.Add(new SkippedExtension { Extension = extension, Reason = "Ayarlarda kapalı." });
                    continue;
                }

                if (RunlyRegistryLayout.IsBlockedExtension(extension))
                {
                    skipped.Add(new SkippedExtension { Extension = extension, Reason = "Windows güvenliği nedeniyle yönetilemez." });
                    continue;
                }

                var interpreterPath = FindInterpreter(mapping);
                if (interpreterPath is null)
                {
                    skipped.Add(new SkippedExtension
                    {
                        Extension = extension,
                        Reason = mapping.Kind == HandlerKind.Open && string.IsNullOrWhiteSpace(mapping.OpenWith)
                            ? "Bu uzantı için uygulama seçilmedi."
                            : mapping.Kind == HandlerKind.Open
                                ? $"Uygulama bulunamadı: {mapping.OpenWith}"
                                : $"Yorumlayıcı bulunamadı: {mapping.Interpreter}",
                    });
                    continue;
                }

                candidates.Add((extension, mapping, interpreterPath));
            }

            // 2. Back up every key we are about to touch, before the first write.
            var backupKeys = new List<string> { RunlyRegistryLayout.ApplicationKey, RunlyRegistryLayout.VendorKey, RunlyRegistryLayout.RegisteredApplicationsKey };
            foreach (var (extension, _, _) in candidates)
            {
                backupKeys.Add(RunlyRegistryLayout.ProgIdKey(extension));
                backupKeys.Add(RunlyRegistryLayout.ExtensionKey(extension));
            }

            var backupPath = _backup.CreateBackup(backupKeys);
            actions.Add($"Kayıt defteri yedeği alındı: {backupPath}");

            var command = $"\"{Path.GetFullPath(exePath)}\"";

            // 3-4. Application registration, so Runly shows up in the "Open with" list.
            WriteApplicationRegistration(command, candidates.Select(c => c.Extension).ToList());
            actions.Add("Uygulama kaydı yazıldı (Birlikte aç listesi, Varsayılan uygulamalar).");

            foreach (var (extension, mapping, interpreterPath) in candidates)
            {
                var progId = RunlyRegistryLayout.ProgIdFor(extension);

                // 3. The ProgID tree with all four verbs.
                WriteProgId(extension, mapping, installDir, command);

                // 5. OpenWithProgids is always written: it is what puts Runly in the "Open with" list.
                _registry.SetValue(
                    RegistryRoot.CurrentUser,
                    RunlyRegistryLayout.OpenWithProgidsKey(extension),
                    RegistryValueEntry.FromString(progId, string.Empty));

                // 6. Bind the extension, then report what Windows will *actually* do (decision K19).
                var state = _userChoice.Check(extension);

                if (state.Owner == UserChoiceOwner.None)
                {
                    // Harmless and some flows still read it, but on Windows 11 it does not decide anything
                    // on its own: with a second candidate under OpenWithProgids the shell asks the user.
                    _registry.SetValue(
                        RegistryRoot.CurrentUser,
                        RunlyRegistryLayout.ExtensionKey(extension),
                        RegistryValueEntry.FromString(RegistryValueEntry.DefaultValueName, progId));
                }

                var (binding, ownerName) = Evaluate(extension, state, exePath);

                if (binding == BindingState.Bound)
                {
                    actions.Add($"{extension} → Runly'ye bağlı ✅ (Windows'un etkin seçimi Runly).");
                }
                else if (state.Owner == UserChoiceOwner.OwnedByOther)
                {
                    actions.Add(
                        $"{extension} → Windows onayı bekliyor ⚠ — uzantı şu anda {ownerName} ile açılıyor " +
                        "(\"Birlikte aç\" adımı gerekiyor).");
                }
                else
                {
                    actions.Add(
                        $"{extension} → Windows onayı bekliyor ⚠ (\"Birlikte aç\" adımı gerekiyor); " +
                        "kayıtlar yazıldı ama çift tıkta Windows hangi uygulamayı kullanacağını soracak.");
                }

                statuses.Add(new ExtensionStatus
                {
                    Extension = extension,
                    InterpreterFound = true,
                    InterpreterPath = interpreterPath,
                    Bound = binding,
                    UserChoiceOwnerName = ownerName,
                });
            }

            // 7. Let Explorer refresh its icons and menus.
            _notifier.AssociationsChanged();
            actions.Add("Explorer'a dosya ilişkilerinin değiştiği bildirildi.");

            return new InstallResult
            {
                Success = true,
                BackupPath = backupPath,
                Extensions = statuses,
                Skipped = skipped,
                Actions = actions,
            };
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            return new InstallResult
            {
                Success = false,
                Extensions = statuses,
                Skipped = skipped,
                Actions = actions,
                ErrorMessage = $"Kurulum tamamlanamadı: {ex.Message}",
            };
        }
    }

    /// <inheritdoc />
    public UninstallResult Uninstall(UninstallOptions? options = null)
    {
        options ??= UninstallOptions.Default;

        var actions = new List<string>();
        var removed = new List<string>();
        var affected = new List<OrphanedUserChoice>();

        try
        {
            // Every extension we ever registered is discoverable from the ProgID names themselves,
            // so uninstall works even without a config file.
            var progIds = _registry
                .GetSubKeyNames(RegistryRoot.CurrentUser, RunlyRegistryLayout.ClassesKey)
                .Where(RunlyRegistryLayout.IsRunlyProgId)
                .ToList();

            foreach (var progId in progIds)
            {
                var extension = RunlyRegistryLayout.ExtensionFromProgId(progId);

                // Decision K20: check *before* the ProgID goes away, otherwise the extension is left pointing
                // at a handler that no longer exists and there is no way to tell it was ever ours.
                if (extension is not null)
                {
                    var orphan = TryReleaseUserChoice(extension);
                    if (orphan is not null)
                    {
                        affected.Add(orphan);
                        actions.Add(orphan.Removed
                            ? $"{extension} → Windows'un \"Birlikte aç\" seçimi (UserChoice) Runly'yi gösteriyordu, silindi."
                            : $"{extension} → Windows'un \"Birlikte aç\" seçimi (UserChoice) hâlâ Runly'yi gösteriyor ve " +
                              $"silinemedi ({orphan.FailureReason}). Bu uzantı geçersiz bir uygulamaya bağlı kaldı.");
                    }
                }

                var progIdKey = RunlyRegistryLayout.ClassesKey + @"\" + progId;
                _registry.DeleteKeyTree(RegistryRoot.CurrentUser, progIdKey);
                removed.Add(progIdKey);

                if (extension is null)
                {
                    continue;
                }

                _registry.DeleteValue(
                    RegistryRoot.CurrentUser, RunlyRegistryLayout.OpenWithProgidsKey(extension), progId);

                // Only clear the extension default when it is still pointing at us.
                var current = _registry
                    .GetValue(RegistryRoot.CurrentUser, RunlyRegistryLayout.ExtensionKey(extension), RegistryValueEntry.DefaultValueName)
                    ?.AsString();

                if (RunlyRegistryLayout.IsRunlyProgId(current))
                {
                    _registry.DeleteValue(
                        RegistryRoot.CurrentUser, RunlyRegistryLayout.ExtensionKey(extension), RegistryValueEntry.DefaultValueName);
                    actions.Add($"{extension} bağlantısı kaldırıldı (eski değere döndürülmedi, boş bırakıldı).");
                }

                actions.Add($"{progId} anahtarı silindi.");
            }

            foreach (var key in new[] { RunlyRegistryLayout.ApplicationKey, RunlyRegistryLayout.VendorKey })
            {
                if (_registry.KeyExists(RegistryRoot.CurrentUser, key))
                {
                    _registry.DeleteKeyTree(RegistryRoot.CurrentUser, key);
                    removed.Add(key);
                    actions.Add($"{key} anahtarı silindi.");
                }
            }

            _registry.DeleteValue(
                RegistryRoot.CurrentUser,
                RunlyRegistryLayout.RegisteredApplicationsKey,
                RunlyRegistryLayout.RegisteredApplicationName);
            actions.Add("Kayıtlı uygulamalar listesinden çıkarıldı.");

            // Restoring the backup is deliberately opt-in: .js used to point at WScript.exe (SPEC 9).
            string? restoredPath = null;
            if (options.RestoreBackup)
            {
                restoredPath = options.BackupPath ?? _backup.GetLatestBackup()?.Path;

                if (restoredPath is null)
                {
                    actions.Add("Geri yüklenecek yedek bulunamadı, eski ilişkiler geri getirilmedi.");
                }
                else
                {
                    _backup.RestoreBackup(restoredPath);
                    actions.Add($"Yedek geri yüklendi: {restoredPath}");
                }
            }

            _notifier.AssociationsChanged();
            actions.Add("Explorer'a dosya ilişkilerinin değiştiği bildirildi.");

            return new UninstallResult
            {
                Success = true,
                RemovedKeys = removed,
                RestoredBackupPath = restoredPath,
                Actions = actions,
                AffectedUserChoices = affected,
            };
        }
        catch (Exception ex)
        {
            return new UninstallResult
            {
                Success = false,
                RemovedKeys = removed,
                Actions = actions,
                AffectedUserChoices = affected,
                ErrorMessage = $"Kaldırma tamamlanamadı: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Attempts to remove an extension's <c>UserChoice</c> key when it points at one of Runly's ProgIDs
    /// (decision K20). Windows protects the key with an ACL and typically refuses; that is recorded, not thrown,
    /// because a refusal is not a failed uninstall — it is something the user has to be told about.
    /// </summary>
    /// <returns><see langword="null"/> when the extension was never ours to release.</returns>
    private OrphanedUserChoice? TryReleaseUserChoice(string extension)
    {
        var state = _userChoice.Check(extension);
        if (state.Owner != UserChoiceOwner.OwnedByRunly)
        {
            return null;
        }

        var key = UserChoiceInspector.UserChoiceKey(extension);
        string? failure = null;

        try
        {
            _registry.DeleteKeyTree(RegistryRoot.CurrentUser, key);
        }
        catch (Exception ex)
        {
            failure = ex.Message;
        }

        // Trust the registry, not the return value: the delete can "succeed" and leave the key in place.
        var stillThere = _registry.KeyExists(RegistryRoot.CurrentUser, key);

        return new OrphanedUserChoice
        {
            Extension = extension,
            ProgId = state.ProgId ?? RunlyRegistryLayout.ProgIdFor(extension),
            Removed = !stillThere,
            FailureReason = stillThere ? failure ?? "Windows anahtarın silinmesine izin vermiyor" : null,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<ExtensionStatus> GetStatus(RunlyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var result = new List<ExtensionStatus>(config.Extensions.Count);

        foreach (var (rawExtension, mapping) in config.Extensions.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            var extension = RunlyRegistryLayout.NormalizeExtension(rawExtension);
            var interpreterPath = FindInterpreter(mapping);

            var progIdExists = _registry.KeyExists(RegistryRoot.CurrentUser, RunlyRegistryLayout.ProgIdKey(extension));
            var state = _userChoice.Check(extension);

            var (binding, ownerName) = Evaluate(extension, state, expectedExePath: null);

            // Nothing of Runly's is registered for this extension, so "waiting for approval" would be a lie:
            // there is nothing to approve until the user installs.
            if (binding == BindingState.NeedsUserChoice && !progIdExists)
            {
                binding = BindingState.NotBound;
                ownerName = null;
            }

            result.Add(new ExtensionStatus
            {
                Extension = extension,
                InterpreterFound = interpreterPath is not null,
                InterpreterPath = interpreterPath,
                Bound = binding,
                UserChoiceOwnerName = ownerName,
            });
        }

        return result;
    }

    /// <summary>
    /// The single place that decides whether an extension is really bound (decision K19). <c>Bound</c> requires
    /// <c>FileExts\&lt;ext&gt;\UserChoice\ProgId</c> to be one of Runly's ProgIDs — nothing else counts, because
    /// nothing else decides what a double click does. When <c>AssocQueryString</c> can be asked and disagrees,
    /// the pessimistic answer wins.
    /// </summary>
    private (BindingState Binding, string? OwnerName) Evaluate(string extension, UserChoiceState state, string? expectedExePath)
    {
        if (state.Owner != UserChoiceOwner.OwnedByRunly)
        {
            return (BindingState.NeedsUserChoice,
                state.Owner == UserChoiceOwner.OwnedByOther ? state.FriendlyName : null);
        }

        var handler = SafeGetEffectiveHandler(extension);
        if (handler is not null && !PointsAtRunly(handler, expectedExePath))
        {
            // UserChoice says Runly, the shell says something else. Never claim the optimistic side.
            return (BindingState.NeedsUserChoice, Path.GetFileName(handler));
        }

        return (BindingState.Bound, null);
    }

    private string? SafeGetEffectiveHandler(string extension)
    {
        try
        {
            return _effectiveHandler.GetExecutable(extension);
        }
        catch (Exception)
        {
            // No second opinion is available; fall back to UserChoice alone rather than failing the whole run.
            return null;
        }
    }

    private static bool PointsAtRunly(string handlerPath, string? expectedExePath)
    {
        if (expectedExePath is not null)
        {
            try
            {
                if (string.Equals(Path.GetFullPath(handlerPath), Path.GetFullPath(expectedExePath), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // A malformed path from the shell is simply not a match.
            }
        }

        return string.Equals(
            Path.GetFileName(handlerPath), RunlyRegistryLayout.LauncherFileName, StringComparison.OrdinalIgnoreCase);
    }

    private void WriteProgId(string extension, ExtensionMapping mapping, string installDir, string command)
    {
        var key = RunlyRegistryLayout.ProgIdKey(extension);

        _registry.SetValue(RegistryRoot.CurrentUser, key,
            RegistryValueEntry.FromString(RegistryValueEntry.DefaultValueName, RunlyRegistryLayout.TypeNameFor(extension, mapping.TypeName)));

        _registry.SetValue(RegistryRoot.CurrentUser, key + @"\DefaultIcon",
            RegistryValueEntry.FromString(RegistryValueEntry.DefaultValueName, RunlyRegistryLayout.IconValue(installDir, mapping.Icon, mapping.Category)));

        if (mapping.Kind == HandlerKind.Open)
        {
            WriteVerb(key, "open", "Runly ile aç", command + " \"%1\" %*");
            return;
        }

        WriteVerb(key, "open", "Runly ile çalıştır", command + " \"%1\" %*");
        WriteVerb(key, "runas", "Yönetici olarak çalıştır (Runly)", command + " --verb runas \"%1\" %*");
        WriteVerb(key, "edit", "Düzenle", command + " --verb edit \"%1\"");
        WriteVerb(key, "runlyargs", "Runly ile argümanlarla çalıştır…", command + " --verb prompt-args \"%1\"");
    }

    private void WriteVerb(string progIdKey, string verb, string muiVerb, string commandLine)
    {
        var verbKey = $@"{progIdKey}\shell\{verb}";

        _registry.SetValue(RegistryRoot.CurrentUser, verbKey, RegistryValueEntry.FromString("MUIVerb", muiVerb));
        _registry.SetValue(RegistryRoot.CurrentUser, verbKey + @"\command",
            RegistryValueEntry.FromString(RegistryValueEntry.DefaultValueName, commandLine));
    }

    private void WriteApplicationRegistration(string command, IReadOnlyList<string> extensions)
    {
        _registry.SetValue(RegistryRoot.CurrentUser, RunlyRegistryLayout.ApplicationKey,
            RegistryValueEntry.FromString("FriendlyAppName", RunlyRegistryLayout.ApplicationName));

        _registry.SetValue(RegistryRoot.CurrentUser, RunlyRegistryLayout.ApplicationKey + @"\shell\open\command",
            RegistryValueEntry.FromString(RegistryValueEntry.DefaultValueName, command + " \"%1\" %*"));

        _registry.CreateKey(RegistryRoot.CurrentUser, RunlyRegistryLayout.SupportedTypesKey);
        _registry.SetValue(RegistryRoot.CurrentUser, RunlyRegistryLayout.CapabilitiesKey,
            RegistryValueEntry.FromString("ApplicationName", RunlyRegistryLayout.ApplicationName));
        _registry.SetValue(RegistryRoot.CurrentUser, RunlyRegistryLayout.CapabilitiesKey,
            RegistryValueEntry.FromString("ApplicationDescription", RunlyRegistryLayout.ApplicationDescription));

        foreach (var extension in extensions)
        {
            _registry.SetValue(RegistryRoot.CurrentUser, RunlyRegistryLayout.SupportedTypesKey,
                RegistryValueEntry.FromString(extension, string.Empty));

            _registry.SetValue(RegistryRoot.CurrentUser, RunlyRegistryLayout.FileAssociationsKey,
                RegistryValueEntry.FromString(extension, RunlyRegistryLayout.ProgIdFor(extension)));
        }

        _registry.SetValue(RegistryRoot.CurrentUser, RunlyRegistryLayout.RegisteredApplicationsKey,
            RegistryValueEntry.FromString(RunlyRegistryLayout.RegisteredApplicationName, RunlyRegistryLayout.CapabilitiesKey));
    }

    /// <summary>
    /// Resolves the mapping's interpreter. Absolute paths are checked directly; bare names go through
    /// <see cref="IPathSearcher"/>, whose implementation belongs to T2.
    /// </summary>
    private string? FindInterpreter(ExtensionMapping mapping)
    {
        var name = (mapping.Kind == HandlerKind.Open ? mapping.OpenWith : mapping.Interpreter)?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (name.Contains('\\', StringComparison.Ordinal) ||
            name.Contains('/', StringComparison.Ordinal) ||
            Path.IsPathRooted(name))
        {
            return File.Exists(name) ? Path.GetFullPath(name) : null;
        }

        return _pathSearcher.Find(name);
    }
}
