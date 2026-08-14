using Runly.Core.Defaults;
using Runly.Core.Models;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>Proves <see cref="ConfigStore"/> never throws and always recovers to a usable configuration.</summary>
public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigStoreTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("runly-config-tests-").FullName;
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Load_WhenFileMissing_WritesAndReturnsDefaults()
    {
        var store = new ConfigStore(_configPath);

        var config = store.Load();

        Assert.True(File.Exists(_configPath));
        Assert.Equal(SecurityMode.TrustOnFirstUse, config.SecurityMode);
        Assert.Equal(14, config.Extensions.Count);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsWithoutLoss()
    {
        var store = new ConfigStore(_configPath);
        var edited = store.Load() with { SecurityMode = SecurityMode.AlwaysAsk, EditorCommand = "notepad" };

        store.Save(edited);
        var reloaded = new ConfigStore(_configPath).Load();

        Assert.Equal(SecurityMode.AlwaysAsk, reloaded.SecurityMode);
        Assert.Equal("notepad", reloaded.EditorCommand);
    }

    [Fact]
    public void Load_Version1Config_MigratesLosslesslyAndDefaultsKindToRun()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_configPath, """
            {
              "version": 1,
              "language": "en",
              "securityMode": "AlwaysAsk",
              "keepWindowOpen": "Always",
              "editorCommand": "custom-editor",
              "logEnabled": false,
              "extensions": {
                ".XYZ": { "interpreter": "custom.exe", "args": "--flag \"{script}\"", "enabled": true, "icon": "mine.ico" }
              }
            }
            """);

        var config = new ConfigStore(_configPath).Load();

        Assert.Equal(2, config.Version);
        Assert.Equal("en", config.Language);
        Assert.Equal(SecurityMode.AlwaysAsk, config.SecurityMode);
        Assert.Equal(KeepWindowMode.Always, config.KeepWindowOpen);
        Assert.Equal("custom-editor", config.EditorCommand);
        Assert.False(config.LogEnabled);
        var mapping = Assert.Single(config.Extensions).Value;
        Assert.Equal(HandlerKind.Run, mapping.Kind);
        Assert.Equal("Betikler", mapping.Category);
        Assert.Equal("custom.exe", mapping.Interpreter);
        Assert.Equal("--flag \"{script}\"", mapping.Args);
        Assert.True(mapping.Enabled);
        Assert.Equal("mine.ico", mapping.Icon);

        var rewritten = File.ReadAllText(_configPath);
        Assert.Contains("\"version\": 2", rewritten, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"Run\"", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WhenFileIsCorruptJson_RenamesToBakAndReturnsDefaults()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_configPath, "{ this is not valid json ");

        var store = new ConfigStore(_configPath);
        var config = store.Load();

        Assert.Equal(SecurityMode.TrustOnFirstUse, config.SecurityMode);
        Assert.True(File.Exists(_configPath + ".bak"));
        Assert.Equal("{ this is not valid json ", File.ReadAllText(_configPath + ".bak"));
        // The corrupt file must not be left behind at the live path once it has been recovered.
        Assert.False(File.ReadAllText(_configPath).Contains("this is not valid json", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_DoesNotThrow_WhenFileIsEmpty()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_configPath, string.Empty);

        var store = new ConfigStore(_configPath);
        var config = store.Load();

        Assert.NotNull(config);
        Assert.True(File.Exists(_configPath + ".bak"));
    }

    [Fact]
    public void Save_WritesAtomically_NoTempFileLeftBehind()
    {
        var store = new ConfigStore(_configPath);

        store.Save(DefaultConfig.Create());

        var leftoverTempFiles = Directory.GetFiles(_tempDir, "*.tmp");
        Assert.Empty(leftoverTempFiles);
        Assert.True(File.Exists(_configPath));
    }

    [Fact]
    public void Load_SemanticallyInvalidButValidJson_NormalizesNullableMembersAndEnums()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_configPath, """
            { "version": 1, "securityMode": 999, "keepWindowOpen": 999,
              "editorCommand": null, "extensions": null }
            """);

        var config = new ConfigStore(_configPath).Load();

        Assert.Equal(SecurityMode.TrustOnFirstUse, config.SecurityMode);
        Assert.Equal(KeepWindowMode.OnError, config.KeepWindowOpen);
        Assert.Equal(string.Empty, config.EditorCommand);
        Assert.NotNull(config.Extensions);
        Assert.Empty(config.Extensions);
    }

    [Fact]
    public void Save_WhenAtomicReplaceFails_DoesNotLeaveTempFile()
    {
        var targetDirectory = Path.Combine(_tempDir, "target.json");
        Directory.CreateDirectory(targetDirectory);
        var store = new ConfigStore(targetDirectory);

        var error = Record.Exception(() => store.Save(DefaultConfig.Create()));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Empty(Directory.GetFiles(_tempDir, "*.tmp"));
    }

    [Fact]
    public void ConfigPath_DefaultsToRunlyPathsConfigPath_WhenNoOverrideGiven()
    {
        var store = new ConfigStore();

        Assert.EndsWith(Path.Combine("Runly", "config.json"), store.ConfigPath, StringComparison.OrdinalIgnoreCase);
    }
}
