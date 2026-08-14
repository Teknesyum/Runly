using System.Text.Json;
using Runly.Core.Abstractions;
using Runly.Core.Defaults;
using Runly.Core.Json;
using Runly.Core.Models;
using Runly.Core.Paths;

namespace Runly.Core.Services;

/// <summary>File-backed <see cref="IConfigStore"/> that repairs a corrupt <c>config.json</c> instead of failing (SPEC 5.1).</summary>
public sealed class ConfigStore : IConfigStore
{
    private readonly ILogger? _logger;

    /// <summary>Creates a store at the default <c>%APPDATA%\Runly\config.json</c> location, or a custom path for tests.</summary>
    public ConfigStore(string? configPath = null, ILogger? logger = null)
    {
        ConfigPath = configPath ?? RunlyPaths.ConfigPath;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ConfigPath { get; }

    /// <inheritdoc />
    public RunlyConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var fresh = DefaultConfig.Create();
            TrySave(fresh);
            return fresh;
        }

        var config = TryReadConfig();
        if (config is null)
        {
            return RecoverFromCorruption();
        }

        config = Normalize(config);

        if (config.Version < RunlyConfig.CurrentVersion)
        {
            config = MigrateFromOlderVersion(config);
            TrySave(config);
        }

        return config;
    }

    /// <inheritdoc />
    public void Save(RunlyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var json = JsonSerializer.Serialize(config, RunlyJson.Config);
        AtomicFileWriter.Write(ConfigPath, json);
    }

    private RunlyConfig? TryReadConfig()
    {
        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize(json, RunlyJson.Config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger?.Warn($"config.json okunamadı veya bozuk: {ex.Message}");
            return null;
        }
    }

    private RunlyConfig RecoverFromCorruption()
    {
        try
        {
            AtomicFileWriter.RenameToBackup(ConfigPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.Warn($"Bozuk config.json .bak olarak taşınamadı: {ex.Message}");
        }

        var fresh = DefaultConfig.Create();
        TrySave(fresh);
        return fresh;
    }

    private static RunlyConfig MigrateFromOlderVersion(RunlyConfig config)
    {
        // HandlerKind.Run is deliberately enum value zero: a v1 mapping with no "kind" field
        // deserializes as Run and retains every existing user-edited value.
        return config with { Version = RunlyConfig.CurrentVersion };
    }

    private static RunlyConfig Normalize(RunlyConfig config)
    {
        var extensions = RunlyConfig.CreateExtensionDictionary();
        if (config.Extensions is not null)
        {
            foreach (var pair in config.Extensions)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                {
                    continue;
                }

                var key = RunlyConfig.NormalizeExtension(pair.Key);
                if (key.Length == 0)
                {
                    continue;
                }

                extensions[key] = pair.Value with
                {
                    Kind = Enum.IsDefined(pair.Value.Kind) ? pair.Value.Kind : HandlerKind.Run,
                    Category = NormalizeCategory(pair.Value.Category),
                    Interpreter = pair.Value.Interpreter ?? string.Empty,
                    OpenWith = string.IsNullOrWhiteSpace(pair.Value.OpenWith) ? null : pair.Value.OpenWith,
                    Args = pair.Value.Args ?? string.Empty,
                };
            }
        }

        return config with
        {
            SecurityMode = Enum.IsDefined(config.SecurityMode) ? config.SecurityMode : SecurityMode.TrustOnFirstUse,
            KeepWindowOpen = Enum.IsDefined(config.KeepWindowOpen) ? config.KeepWindowOpen : KeepWindowMode.OnError,
            EditorCommand = config.EditorCommand ?? string.Empty,
            Extensions = extensions,
        };
    }

    private static string NormalizeCategory(string? category) => category switch
    {
        null or "" or "scripts" or "Betikler" => "scripts",
        "code" or "Kod/Geliştirme" => "code",
        "text" or "Metin ve Belge" => "text",
        "data" or "Yapılandırma/Veri" => "data",
        "web" or "Web" => "web",
        "images" or "Görseller" => "images",
        "audio" or "Ses" => "audio",
        "video" or "Video" => "video",
        "archive" or "Arşiv" => "archive",
        "office" or "Ofis/Doküman" => "office",
        "design" or "3B ve Tasarım" => "design",
        "fonts" or "Yazı Tipleri" => "fonts",
        "locked" or "Kilitli" => "locked",
        "special" or "Özel" => "special",
        _ => "special",
    };

    private void TrySave(RunlyConfig config)
    {
        try
        {
            Save(config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.Warn($"config.json yazılamadı: {ex.Message}");
        }
    }
}
