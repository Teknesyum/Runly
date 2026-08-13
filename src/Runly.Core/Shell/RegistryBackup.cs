using System.Globalization;
using System.Text;
using Runly.Core.Paths;

namespace Runly.Core.Shell;

/// <summary>A registry backup file on disk, as listed in the settings GUI (SPEC 5, SPEC 10).</summary>
public sealed record BackupInfo
{
    /// <summary>Full path of the <c>.reg</c> file.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>File name without the directory part.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Creation time taken from the file name, falling back to the file system timestamp.</summary>
    public DateTime CreatedUtc { get; init; }

    /// <summary>Size of the backup file in bytes.</summary>
    public long SizeBytes { get; init; }
}

/// <summary>
/// Writes and replays <c>%APPDATA%\Runly\backups\assoc-*.reg</c>. Everything is produced and applied by
/// Runly's own code: SPEC 9 and T4 forbid <c>reg.exe export</c>, <c>reg.exe import</c> and <c>regedit /s</c>.
/// </summary>
public sealed class RegistryBackup
{
    private const string FilePrefix = "assoc-";
    private const string FileExtension = ".reg";
    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    private readonly IRegistryAccessor _registry;
    private readonly Func<DateTime> _clock;

    /// <summary>Creates a backup service writing to <c>%APPDATA%\Runly\backups</c>.</summary>
    public RegistryBackup(IRegistryAccessor registry)
        : this(registry, RunlyPaths.BackupDir)
    {
    }

    /// <summary>Creates a backup service writing to an explicit directory; used by tests.</summary>
    public RegistryBackup(IRegistryAccessor registry, string backupDirectory)
        : this(registry, backupDirectory, () => DateTime.Now)
    {
    }

    /// <summary>Creates a backup service with an injected clock; used by tests that need deterministic names.</summary>
    public RegistryBackup(IRegistryAccessor registry, string backupDirectory, Func<DateTime> clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(clock);

        _registry = registry;
        _clock = clock;
        BackupDirectory = backupDirectory;
    }

    /// <summary>Directory the <c>.reg</c> backups are written to.</summary>
    public string BackupDirectory { get; }

    /// <summary>
    /// Snapshots the given HKCU sub keys into a new timestamped <c>.reg</c> file and returns its full path.
    /// </summary>
    public string CreateBackup(IEnumerable<string> hkcuSubKeys)
    {
        var text = BuildBackupText(hkcuSubKeys);

        Directory.CreateDirectory(BackupDirectory);
        var path = NextFilePath();

        // UTF-16 LE with BOM, so the file also opens correctly in regedit if the user ever inspects it.
        File.WriteAllText(path, text, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));
        return path;
    }

    /// <summary>
    /// Builds the <c>.reg</c> text for the given HKCU sub keys without touching the disk.
    /// <para>
    /// A key that does not exist yet gets a <c>[-HKEY_CURRENT_USER\…]</c> delete line, so replaying the backup
    /// removes what Runly created. A key Runly owns outright gets a delete line as well, so that a stale
    /// installation is wiped rather than merged.
    /// </para>
    /// <para>
    /// A key that already exists and is <em>shared</em> with the rest of Windows — <c>.ext</c> and
    /// <c>RegisteredApplications</c> — is exported without a delete line. Deleting and recreating those would
    /// briefly destroy every other application's entries, and a failure halfway through would leave the user
    /// with none of them. Removing Runly's own values from shared keys is <see cref="ShellRegistrar.Uninstall"/>'s
    /// job, and it does that value by value.
    /// </para>
    /// </summary>
    public string BuildBackupText(IEnumerable<string> hkcuSubKeys)
    {
        ArgumentNullException.ThrowIfNull(hkcuSubKeys);

        var roots = NormalizeRoots(hkcuSubKeys);
        var blocks = new List<RegKeyBlock>();

        foreach (var root in roots)
        {
            var exists = _registry.KeyExists(RegistryRoot.CurrentUser, root);

            if (!exists || IsRunlyOwned(root))
            {
                blocks.Add(RegKeyBlock.DeleteKey(root));
            }

            if (exists)
            {
                Export(root, blocks);
            }
        }

        return RegFileWriter.Write(blocks);
    }

    /// <summary>
    /// Whether a key belongs to Runly alone, so that deleting it before restoring cannot harm anything else.
    /// </summary>
    public static bool IsRunlyOwned(string subKey)
    {
        ArgumentNullException.ThrowIfNull(subKey);

        var path = RegFileWriter.NormalizeSubKey(subKey);

        return IsAtOrUnder(path, RunlyRegistryLayout.ApplicationKey)
            || IsAtOrUnder(path, RunlyRegistryLayout.VendorKey)
            || RunlyRegistryLayout.IsRunlyProgId(RelativeProgId(path));
    }

    private static string? RelativeProgId(string path)
    {
        var prefix = RunlyRegistryLayout.ClassesKey + "\\";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = path[prefix.Length..];
        var cut = rest.IndexOf('\\', StringComparison.Ordinal);
        return cut < 0 ? rest : rest[..cut];
    }

    private static bool IsAtOrUnder(string path, string ancestor) =>
        path.Equals(ancestor, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(ancestor + "\\", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Applies a backup with Runly's own parser. The whole file is parsed and validated before the first write,
    /// so a malformed or non HKCU file is rejected without leaving the registry half changed.
    /// </summary>
    public void RestoreBackup(string regFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regFilePath);

        if (!File.Exists(regFilePath))
        {
            throw new FileNotFoundException($"Yedek dosyası bulunamadı: {regFilePath}", regFilePath);
        }

        var blocks = RegFileParser.Parse(File.ReadAllText(regFilePath));

        foreach (var block in blocks)
        {
            if (block.Delete)
            {
                _registry.DeleteKeyTree(RegistryRoot.CurrentUser, block.SubKey);
                continue;
            }

            _registry.CreateKey(RegistryRoot.CurrentUser, block.SubKey);

            foreach (var op in block.Values)
            {
                if (op.Delete)
                {
                    _registry.DeleteValue(RegistryRoot.CurrentUser, block.SubKey, op.Name);
                }
                else if (op.Value is not null)
                {
                    _registry.SetValue(RegistryRoot.CurrentUser, block.SubKey, op.Value);
                }
            }
        }
    }

    /// <summary>Lists the backups in the backup directory, newest first.</summary>
    public IReadOnlyList<BackupInfo> ListBackups()
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return [];
        }

        var list = new List<BackupInfo>();

        foreach (var path in Directory.EnumerateFiles(BackupDirectory, FilePrefix + "*" + FileExtension))
        {
            var info = new FileInfo(path);
            list.Add(new BackupInfo
            {
                Path = info.FullName,
                FileName = info.Name,
                CreatedUtc = ParseTimestamp(info.Name) ?? info.CreationTimeUtc,
                SizeBytes = info.Length,
            });
        }

        list.Sort((a, b) => b.CreatedUtc.CompareTo(a.CreatedUtc));
        return list;
    }

    /// <summary>Returns the most recent backup, or <see langword="null"/> when none has been written yet.</summary>
    public BackupInfo? GetLatestBackup() => ListBackups().FirstOrDefault();

    private void Export(string subKey, List<RegKeyBlock> blocks)
    {
        var values = _registry.GetValues(RegistryRoot.CurrentUser, subKey);
        var ops = new List<RegValueOperation>(values.Count);

        foreach (var value in values)
        {
            ops.Add(RegValueOperation.Set(value));
        }

        blocks.Add(new RegKeyBlock { SubKey = subKey, Values = ops });

        foreach (var child in _registry.GetSubKeyNames(RegistryRoot.CurrentUser, subKey))
        {
            Export(subKey + "\\" + child, blocks);
        }
    }

    /// <summary>
    /// De-duplicates the requested keys and drops any key that already lives under another requested key.
    /// Without this, the delete line of a nested key would wipe out what its parent's blocks just restored.
    /// </summary>
    private static List<string> NormalizeRoots(IEnumerable<string> hkcuSubKeys)
    {
        var cleaned = new List<string>();

        foreach (var raw in hkcuSubKeys)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var path = RegFileWriter.NormalizeSubKey(raw);
            if (path.Length == 0)
            {
                throw new ArgumentException("Yedeklenecek anahtar yolu boş olamaz.", nameof(hkcuSubKeys));
            }

            if (!cleaned.Any(existing => existing.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                cleaned.Add(path);
            }
        }

        var result = new List<string>(cleaned.Count);

        foreach (var path in cleaned)
        {
            var nested = cleaned.Any(other =>
                other.Length < path.Length &&
                path.StartsWith(other + "\\", StringComparison.OrdinalIgnoreCase));

            if (!nested)
            {
                result.Add(path);
            }
        }

        return result;
    }

    private string NextFilePath()
    {
        var stamp = _clock().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var candidate = Path.Combine(BackupDirectory, FilePrefix + stamp + FileExtension);

        var suffix = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(
                BackupDirectory,
                FilePrefix + stamp + "-" + suffix.ToString(CultureInfo.InvariantCulture) + FileExtension);
            suffix++;
        }

        return candidate;
    }

    private static DateTime? ParseTimestamp(string fileName)
    {
        if (!fileName.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var core = fileName[FilePrefix.Length..];
        if (core.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            core = core[..^FileExtension.Length];
        }

        if (core.Length > TimestampFormat.Length)
        {
            core = core[..TimestampFormat.Length];
        }

        return DateTime.TryParseExact(core, TimestampFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}
