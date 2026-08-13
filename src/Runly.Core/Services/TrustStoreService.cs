using System.Text.Json;
using Runly.Core.Abstractions;
using Runly.Core.Defaults;
using Runly.Core.Json;
using Runly.Core.Models;
using Runly.Core.Paths;

namespace Runly.Core.Services;

/// <summary>File-backed <see cref="ITrustStore"/> that repairs a corrupt <c>trust.json</c> instead of failing (SPEC 5.2).</summary>
public sealed class TrustStoreService : ITrustStore
{
    private readonly ILogger? _logger;

    /// <summary>Creates a store at the default <c>%APPDATA%\Runly\trust.json</c> location, or a custom path for tests.</summary>
    public TrustStoreService(string? trustPath = null, ILogger? logger = null)
    {
        TrustPath = trustPath ?? RunlyPaths.TrustPath;
        _logger = logger;
        Data = DefaultConfig.CreateTrustStore();
    }

    /// <inheritdoc />
    public string TrustPath { get; }

    /// <inheritdoc />
    public TrustStore Data { get; private set; }

    /// <inheritdoc />
    public void Load()
    {
        if (!File.Exists(TrustPath))
        {
            Data = DefaultConfig.CreateTrustStore();
            TrySave();
            return;
        }

        var loaded = TryReadTrustStore();
        Data = loaded is null ? RecoverFromCorruption() : Normalize(loaded);
    }

    /// <inheritdoc />
    public void Save()
    {
        var json = JsonSerializer.Serialize(Data, RunlyJson.TrustStore);
        AtomicFileWriter.Write(TrustPath, json);
    }

    /// <inheritdoc />
    public bool IsTrusted(ScriptInfo script)
    {
        ArgumentNullException.ThrowIfNull(script);

        if (TrustMatching.IsWithinAnyTrustedFolder(script.Path, Data.TrustedFolders))
        {
            return true;
        }

        if (TrustMatching.TryGetTrustedFile(script.Path, Data.TrustedFiles, out var entry))
        {
            return script.Sha256 is not null &&
                   string.Equals(entry.Sha256, script.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <inheritdoc />
    public void TrustFile(ScriptInfo script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var key = TrustMatching.NormalizeFullPath(script.Path);
        Data.TrustedFiles[key] = new TrustedFileEntry
        {
            Sha256 = script.Sha256 ?? string.Empty,
            AddedUtc = DateTimeOffset.UtcNow,
        };
    }

    /// <inheritdoc />
    public void TrustFolder(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var normalized = TrustMatching.NormalizeFolderPath(folderPath);
        var alreadyTrusted = Data.TrustedFolders.Any(folder =>
            TrustMatching.NormalizeFolderPath(folder).Equals(normalized, StringComparison.OrdinalIgnoreCase));

        if (!alreadyTrusted)
        {
            Data.TrustedFolders.Add(normalized);
        }
    }

    /// <inheritdoc />
    public void UntrustFolder(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var normalized = TrustMatching.NormalizeFolderPath(folderPath);
        Data.TrustedFolders.RemoveAll(folder =>
            TrustMatching.NormalizeFolderPath(folder).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public void ClearTrustedFiles() => Data.TrustedFiles.Clear();

    private TrustStore? TryReadTrustStore()
    {
        try
        {
            var json = File.ReadAllText(TrustPath);
            return JsonSerializer.Deserialize(json, RunlyJson.TrustStore);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger?.Warn($"trust.json okunamadı veya bozuk: {ex.Message}");
            return null;
        }
    }

    private TrustStore RecoverFromCorruption()
    {
        try
        {
            AtomicFileWriter.RenameToBackup(TrustPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.Warn($"Bozuk trust.json .bak olarak taşınamadı: {ex.Message}");
        }

        var fresh = DefaultConfig.CreateTrustStore();
        Data = fresh;
        TrySave();
        return fresh;
    }

    private static TrustStore Normalize(TrustStore store)
    {
        var folders = new List<string>();
        if (store.TrustedFolders is not null)
        {
            foreach (var folder in store.TrustedFolders)
            {
                if (!TryNormalizeFolder(folder, out var normalized) ||
                    folders.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                folders.Add(normalized);
            }
        }

        var files = TrustStore.CreateFileDictionary();
        if (store.TrustedFiles is not null)
        {
            foreach (var pair in store.TrustedFiles)
            {
                if (pair.Value is null || !TryNormalizeFile(pair.Key, out var normalized))
                {
                    continue;
                }

                files[normalized] = pair.Value with { Sha256 = pair.Value.Sha256 ?? string.Empty };
            }
        }

        return store with { TrustedFolders = folders, TrustedFiles = files };
    }

    private static bool TryNormalizeFolder(string? path, out string normalized)
    {
        try
        {
            normalized = string.IsNullOrWhiteSpace(path) ? string.Empty : TrustMatching.NormalizeFolderPath(path);
            return normalized.Length > 0;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            normalized = string.Empty;
            return false;
        }
    }

    private static bool TryNormalizeFile(string? path, out string normalized)
    {
        try
        {
            normalized = string.IsNullOrWhiteSpace(path) ? string.Empty : TrustMatching.NormalizeFullPath(path);
            return normalized.Length > 0;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            normalized = string.Empty;
            return false;
        }
    }

    private void TrySave()
    {
        try
        {
            Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.Warn($"trust.json yazılamadı: {ex.Message}");
        }
    }
}
