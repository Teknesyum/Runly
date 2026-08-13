namespace Runly.Core.Shell;

/// <summary>Who owns the Windows <c>UserChoice</c> key for an extension (SPEC 2, SPEC 9).</summary>
public enum UserChoiceOwner
{
    /// <summary>No <c>UserChoice</c> key exists, so Runly can bind the extension directly.</summary>
    None,

    /// <summary>The key already points at one of Runly's ProgIDs.</summary>
    OwnedByRunly,

    /// <summary>Another application owns the key; only the Windows "Open with" dialog can change it.</summary>
    OwnedByOther,
}

/// <summary>The result of inspecting an extension's <c>UserChoice</c> key.</summary>
public sealed record UserChoiceState
{
    /// <summary>Nothing owns the extension.</summary>
    public static UserChoiceState Free { get; } = new();

    /// <summary>Who currently owns the extension.</summary>
    public UserChoiceOwner Owner { get; init; } = UserChoiceOwner.None;

    /// <summary>The ProgID recorded in the key, or <see langword="null"/> when there is none.</summary>
    public string? ProgId { get; init; }

    /// <summary>Human readable name of the owning application, ready to put in a Turkish sentence.</summary>
    public string? FriendlyName { get; init; }
}

/// <summary>
/// Reads <c>HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\&lt;ext&gt;\UserChoice</c>.
/// The key is hash protected and is only ever read here: SPEC 2 and T4 forbid writing, deleting or forging it.
/// </summary>
public sealed class UserChoiceInspector
{
    /// <summary>The text shown for Store applications, whose ProgIDs cannot be resolved to a real name.</summary>
    public const string StoreAppName = "bir Microsoft Store uygulaması";

    private const string FileExtsRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts";

    private readonly IRegistryAccessor _registry;

    /// <summary>Creates an inspector over the given registry accessor.</summary>
    public UserChoiceInspector(IRegistryAccessor registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>Builds the <c>FileExts</c> key path for an extension.</summary>
    public static string UserChoiceKey(string extension) =>
        $@"{FileExtsRoot}\{RunlyRegistryLayout.NormalizeExtension(extension)}\UserChoice";

    /// <summary>Reports who owns the given extension according to Windows.</summary>
    public UserChoiceState Check(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var progId = _registry
            .GetValue(RegistryRoot.CurrentUser, UserChoiceKey(extension), "ProgId")
            ?.AsString();

        if (string.IsNullOrWhiteSpace(progId))
        {
            return UserChoiceState.Free;
        }

        if (RunlyRegistryLayout.IsRunlyProgId(progId))
        {
            return new UserChoiceState
            {
                Owner = UserChoiceOwner.OwnedByRunly,
                ProgId = progId,
                FriendlyName = "Runly",
            };
        }

        return new UserChoiceState
        {
            Owner = UserChoiceOwner.OwnedByOther,
            ProgId = progId,
            FriendlyName = ResolveFriendlyName(progId),
        };
    }

    /// <summary>
    /// Turns a ProgID into something worth showing a user. Packaged applications carry opaque
    /// <c>AppX…</c> ProgIDs (on this machine <c>.ps1</c> is held by the Store Notepad), so they are reported
    /// generically instead of being chased through the packaging registry.
    /// </summary>
    public string ResolveFriendlyName(string progId)
    {
        ArgumentNullException.ThrowIfNull(progId);

        if (progId.StartsWith("AppX", StringComparison.OrdinalIgnoreCase))
        {
            return StoreAppName;
        }

        // Classic ProgIDs carry their label in the default value.
        var name = _registry
            .GetValue(RegistryRoot.ClassesRoot, progId, RegistryValueEntry.DefaultValueName)
            ?.AsString();

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var friendly = _registry
            .GetValue(RegistryRoot.ClassesRoot, progId, "FriendlyTypeName")
            ?.AsString();

        if (!string.IsNullOrWhiteSpace(friendly))
        {
            return friendly;
        }

        // "Applications\notepad++.exe" style ProgIDs label themselves with FriendlyAppName; when that is
        // missing the bare executable name still reads better than the whole registry path.
        var appName = _registry
            .GetValue(RegistryRoot.ClassesRoot, progId, "FriendlyAppName")
            ?.AsString();

        if (!string.IsNullOrWhiteSpace(appName))
        {
            return appName;
        }

        if (progId.StartsWith(@"Applications\", StringComparison.OrdinalIgnoreCase))
        {
            var exe = progId[@"Applications\".Length..];
            if (exe.Length != 0)
            {
                return exe;
            }
        }

        return progId;
    }
}
