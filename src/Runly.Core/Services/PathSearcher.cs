using System.Text.Json;
using System.Text.Json.Serialization;
using Runly.Core.Abstractions;
using Runly.Core.Paths;

namespace Runly.Core.Services;

/// <summary>One cached PATH lookup result, keyed by the requested executable name (SPEC 8, decision K2).</summary>
internal sealed record InterpreterCacheEntry
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("cachedUtc")]
    public DateTimeOffset CachedUtc { get; init; }
}

/// <summary>The on-disk shape of <c>ipcache.json</c> (SPEC 8, decision K2).</summary>
internal sealed record InterpreterCacheFile
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("entries")]
    public Dictionary<string, InterpreterCacheEntry> Entries { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>AOT-safe source-generated context for the interpreter cache file, private to <see cref="PathSearcher"/>.</summary>
[JsonSerializable(typeof(InterpreterCacheFile))]
internal sealed partial class InterpreterCacheJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Finds executables by scanning PATH and PATHEXT manually instead of shelling out to <c>where.exe</c>, with a
/// 24 hour result cache at <see cref="RunlyPaths.CachePath"/> (SPEC 8, decision K2).
/// </summary>
public sealed class PathSearcher : IPathSearcher
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly string[] DefaultPathExtensions = [".COM", ".EXE", ".BAT", ".CMD"];
    private static readonly JsonSerializerOptions CacheJsonOptions = new() { WriteIndented = true };
    private static readonly InterpreterCacheJsonContext CacheJsonContext = new(CacheJsonOptions);

    private readonly string _cachePath;
    private readonly ILogger? _logger;
    private readonly string _pathEnv;
    private readonly string _pathExtEnv;
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>Creates a searcher against the real PATH/PATHEXT and the default cache location, or overrides for tests.</summary>
    public PathSearcher(
        string? cachePath = null,
        ILogger? logger = null,
        string? pathEnvOverride = null,
        string? pathExtEnvOverride = null,
        Func<DateTimeOffset>? utcNowOverride = null)
    {
        _cachePath = cachePath ?? RunlyPaths.CachePath;
        _logger = logger;
        _pathEnv = pathEnvOverride ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        _pathExtEnv = pathExtEnvOverride ?? Environment.GetEnvironmentVariable("PATHEXT") ?? string.Empty;
        _utcNow = utcNowOverride ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public string? Find(string exeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeName);

        if (Path.IsPathRooted(exeName) || exeName.Contains(Path.DirectorySeparatorChar) ||
            exeName.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(exeName) ? Path.GetFullPath(exeName) : null;
        }

        var cache = LoadCache();
        if (cache.Entries.TryGetValue(exeName, out var cached))
        {
            var age = _utcNow() - cached.CachedUtc;
            if (age >= TimeSpan.Zero && age < CacheTtl && File.Exists(cached.Path))
            {
                return cached.Path;
            }
        }

        var found = SearchPathAndPathExt(exeName);
        if (found is not null)
        {
            cache.Entries[exeName] = new InterpreterCacheEntry { Path = found, CachedUtc = _utcNow() };
            SaveCache(cache);
        }

        return found;
    }

    private string? SearchPathAndPathExt(string exeName)
    {
        var directories = _pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var extensions = GetPathExtensions();
        var hasExtension = Path.HasExtension(exeName);

        // Zero-byte candidates are app-execution aliases. Most are Store install stubs that just open
        // the Store, but a working alias (py.exe on this machine) is byte-identical, so size alone
        // cannot tell them apart. Prefer any real executable; accept an alias only if nothing else
        // exists (decision K9).
        string? zeroByteFallback = null;

        foreach (var directory in directories)
        {
            IEnumerable<string> candidates = hasExtension
                ? [Path.Combine(directory, exeName)]
                : extensions.Select(extension => Path.Combine(directory, exeName + extension));

            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                // Store deep-link stubs report zero bytes (SPEC 8, SPEC 2); skip and keep searching.
                if (new FileInfo(candidate).Length == 0)
                {
                    _logger?.Info($"Sıfır boyutlu Store stub'ı atlandı: {candidate}");
                    zeroByteFallback ??= candidate;
                    continue;
                }

                return candidate;
            }
        }

        if (zeroByteFallback is not null)
        {
            _logger?.Warn($"Yalnızca sıfır boyutlu aday bulundu, son çare olarak kabul edildi: {zeroByteFallback}");
        }

        return zeroByteFallback;
    }

    private string[] GetPathExtensions() =>
        string.IsNullOrWhiteSpace(_pathExtEnv)
            ? DefaultPathExtensions
            : _pathExtEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private InterpreterCacheFile LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return new InterpreterCacheFile();
            }

            var json = File.ReadAllText(_cachePath);
            return JsonSerializer.Deserialize(json, CacheJsonContext.InterpreterCacheFile) ?? new InterpreterCacheFile();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger?.Warn($"ipcache.json okunamadı: {ex.Message}");
            return new InterpreterCacheFile();
        }
    }

    private void SaveCache(InterpreterCacheFile cache)
    {
        try
        {
            var json = JsonSerializer.Serialize(cache, CacheJsonContext.InterpreterCacheFile);
            AtomicFileWriter.Write(_cachePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.Warn($"ipcache.json yazılamadı: {ex.Message}");
        }
    }
}
