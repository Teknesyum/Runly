namespace Runly.Core.Shell;

/// <summary>
/// Every HKCU key path and display string Runly writes, in one place (SPEC 9). Install, uninstall, status and
/// backup all derive their paths from here so the three can never drift apart.
/// </summary>
public static class RunlyRegistryLayout
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".sys", ".com", ".bat", ".cmd", ".msi", ".msc", ".cpl", ".scr",
        ".lnk", ".pif", ".url", ".job", ".ocx", ".drv",
    };

    /// <summary>Prefix shared by every Runly ProgID, for example <c>Runly.Script.js</c>.</summary>
    public const string ProgIdPrefix = "Runly.Script.";

    /// <summary>File name of the GUI launcher, used as the key name under <c>Classes\Applications</c>.</summary>
    public const string LauncherFileName = "Runly.exe";

    /// <summary>File name of the console launcher that runs scripts (K29).</summary>
    public const string ConsoleLauncherFileName = "RunlyConsole.exe";

    /// <summary>Value name Runly registers under <c>RegisteredApplications</c>.</summary>
    public const string RegisteredApplicationName = "Runly";

    /// <summary><c>Software\Classes</c>.</summary>
    public const string ClassesKey = @"Software\Classes";

    /// <summary><c>Software\Classes\Applications\Runly.exe</c> — the identity the user sees in "Open with".</summary>
    public const string ApplicationKey = @"Software\Classes\Applications\" + LauncherFileName;

    /// <summary><c>Software\Classes\Applications\RunlyConsole.exe</c>, so uninstall can clear it even though install never writes it (K29).</summary>
    public const string ConsoleApplicationKey = @"Software\Classes\Applications\" + ConsoleLauncherFileName;

    /// <summary><c>Software\Runly</c> — the root of Runly's capability registration.</summary>
    public const string VendorKey = @"Software\Runly";

    /// <summary><c>Software\Runly\Capabilities</c>.</summary>
    public const string CapabilitiesKey = VendorKey + @"\Capabilities";

    /// <summary><c>Software\RegisteredApplications</c>.</summary>
    public const string RegisteredApplicationsKey = @"Software\RegisteredApplications";

    /// <summary>Application name shown in the Windows "Default apps" list.</summary>
    public const string ApplicationName = "Runly";

    /// <summary>Application description shown in the Windows "Default apps" list.</summary>
    public const string ApplicationDescription = "Script dosyalarını çift tıkla çalıştırır";

    /// <summary>Normalises an extension to the lower-case, single-dot form used in every key path.</summary>
    public static string NormalizeExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        var trimmed = extension.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Uzantı boş olamaz.", nameof(extension));
        }

        if (trimmed[0] != '.')
        {
            trimmed = "." + trimmed;
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>Builds the ProgID for an extension, for example <c>.js</c> becomes <c>Runly.Script.js</c>.</summary>
    public static string ProgIdFor(string extension) =>
        ProgIdPrefix + NormalizeExtension(extension)[1..];

    /// <summary>Whether Runly must refuse to register this Windows executable or system type.</summary>
    public static bool IsBlockedExtension(string extension) => BlockedExtensions.Contains(NormalizeExtension(extension));

    /// <summary>Whether a file name is one of Runly's two launcher binaries (K29).</summary>
    public static bool IsLauncherFileName(string? fileName) =>
        string.Equals(fileName, LauncherFileName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fileName, ConsoleLauncherFileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a ProgID belongs to Runly.</summary>
    public static bool IsRunlyProgId(string? progId) =>
        progId is not null && progId.StartsWith(ProgIdPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Recovers the extension from one of Runly's ProgIDs, or <see langword="null"/> when it is not ours.</summary>
    public static string? ExtensionFromProgId(string progId) =>
        IsRunlyProgId(progId) && progId.Length > ProgIdPrefix.Length
            ? "." + progId[ProgIdPrefix.Length..].ToLowerInvariant()
            : null;

    /// <summary><c>Software\Classes\Runly.Script.&lt;ext&gt;</c>.</summary>
    public static string ProgIdKey(string extension) => ClassesKey + @"\" + ProgIdFor(extension);

    /// <summary><c>Software\Classes\.&lt;ext&gt;</c>.</summary>
    public static string ExtensionKey(string extension) => ClassesKey + @"\" + NormalizeExtension(extension);

    /// <summary><c>Software\Classes\.&lt;ext&gt;\OpenWithProgids</c>.</summary>
    public static string OpenWithProgidsKey(string extension) => ExtensionKey(extension) + @"\OpenWithProgids";

    /// <summary><c>Software\Classes\Applications\Runly.exe\SupportedTypes</c>.</summary>
    public static string SupportedTypesKey => ApplicationKey + @"\SupportedTypes";

    /// <summary><c>Software\Runly\Capabilities\FileAssociations</c>.</summary>
    public static string FileAssociationsKey => CapabilitiesKey + @"\FileAssociations";

    /// <summary>Turkish type name shown in Explorer's "Type" column, for example "JavaScript Betiği (Runly)".</summary>
    public static string TypeNameFor(string extension, string? typeName = null)
    {
        var ext = NormalizeExtension(extension);
        var label = string.IsNullOrWhiteSpace(typeName) ? ext[1..].ToUpperInvariant() + " dosyası" : typeName.Trim();
        return label + " (Runly)";
    }

    /// <summary>The <c>DefaultIcon</c> value for an extension, falling back to the launcher's own icon.</summary>
    public static string IconValue(string installDir, string? iconFileName, string? category = null)
    {
        var selected = iconFileName;
        if (string.IsNullOrWhiteSpace(selected) && !string.IsNullOrWhiteSpace(category))
        {
            selected = CategoryIconFileName(category);
        }
        return string.IsNullOrWhiteSpace(selected)
            ? Path.Combine(installDir, LauncherFileName) + ",0"
            : Path.Combine(installDir, "assets", selected) + ",0";
    }

    /// <summary>Maps a catalog category to its single shared neon icon file.</summary>
    public static string CategoryIconFileName(string category) => category switch
    {
        "scripts" => "category-scripts.ico",
        "code" => "category-code.ico",
        "text" => "category-text.ico",
        "data" => "category-data.ico",
        "web" => "category-web.ico",
        "images" => "category-images.ico",
        "audio" => "category-audio.ico",
        "video" => "category-video.ico",
        "archive" => "category-archive.ico",
        "office" => "category-office.ico",
        "design" => "category-design.ico",
        "fonts" => "category-fonts.ico",
        "locked" => "category-locked.ico",
        _ => "category-special.ico",
    };
}
