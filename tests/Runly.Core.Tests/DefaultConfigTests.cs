using System.Text.Json;
using Runly.Core.Defaults;
using Runly.Core.Json;
using Runly.Core.Models;

namespace Runly.Core.Tests;

/// <summary>
/// Proves the default configuration matches SPEC 5.1 and that the source-generated JSON context
/// round-trips it without loss, which is also the proof that the AOT-safe serializer is wired up.
/// </summary>
public sealed class DefaultConfigTests
{
    private static readonly string[] SpecExtensions =
    [
        ".js", ".mjs", ".cjs", ".ts", ".ps1", ".py", ".pyw",
        ".rb", ".pl", ".lua", ".php", ".sh", ".r", ".jar",
    ];

    private static readonly string[] SpecEnabledExtensions = [".js", ".mjs", ".cjs", ".ps1", ".py"];

    [Fact]
    public void Create_ContainsExactlyTheSpecExtensionTable()
    {
        var config = DefaultConfig.Create();

        Assert.Equal(SpecExtensions.Length, config.Extensions.Count);
        Assert.Equal(SpecExtensions.OrderBy(e => e, StringComparer.Ordinal),
                     config.Extensions.Keys.OrderBy(e => e, StringComparer.Ordinal));

        foreach (var mapping in config.Extensions.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(mapping.Interpreter));
            Assert.Contains("{script}", mapping.Args, StringComparison.Ordinal);
            Assert.Contains("{args}", mapping.Args, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Create_EnablesOnlyTheInterpretersPresentOnTheTargetMachine()
    {
        var config = DefaultConfig.Create();

        var enabled = config.Extensions
            .Where(pair => pair.Value.Enabled)
            .Select(pair => pair.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(SpecEnabledExtensions.OrderBy(e => e, StringComparer.Ordinal), enabled);
        Assert.Equal(SecurityMode.TrustOnFirstUse, config.SecurityMode);
        Assert.Equal(KeepWindowMode.OnError, config.KeepWindowOpen);
        Assert.Equal("code", config.EditorCommand);
        Assert.True(config.LogEnabled);
        Assert.Equal(2, config.Version);
        Assert.All(config.Extensions.Values, mapping => Assert.Equal(HandlerKind.Run, mapping.Kind));
        Assert.Equal("js.ico", config.Extensions[".js"].Icon);
    }

    [Fact]
    public void Create_SurvivesAJsonRoundTripWithoutLoss()
    {
        var original = DefaultConfig.Create();

        var firstPass = JsonSerializer.Serialize(original, RunlyJson.Config);
        var restored = JsonSerializer.Deserialize(firstPass, RunlyJson.Config);
        Assert.NotNull(restored);
        var secondPass = JsonSerializer.Serialize(restored, RunlyJson.Config);

        Assert.Equal(firstPass, secondPass, ignoreLineEndingDifferences: true);
        Assert.Equal(original.SecurityMode, restored.SecurityMode);
        Assert.Equal(original.KeepWindowOpen, restored.KeepWindowOpen);
        Assert.Equal(original.EditorCommand, restored.EditorCommand);
        Assert.Equal(original.Extensions.Count, restored.Extensions.Count);

        foreach (var (extension, mapping) in original.Extensions)
        {
            Assert.True(restored.TryGetMapping(extension, out var roundTripped));
            Assert.Equal(mapping, roundTripped);
        }
    }

    [Fact]
    public void Serialize_UsesTheSchemaNamesAndStringEnumsFromTheSpec()
    {
        var json = JsonSerializer.Serialize(DefaultConfig.Create(), RunlyJson.Config);

        Assert.Contains("\"securityMode\": \"TrustOnFirstUse\"", json, StringComparison.Ordinal);
        Assert.Contains("\"keepWindowOpen\": \"OnError\"", json, StringComparison.Ordinal);
        Assert.Contains("\"editorCommand\": \"code\"", json, StringComparison.Ordinal);
        Assert.Contains("\"logEnabled\": true", json, StringComparison.Ordinal);
        Assert.Contains("\".ps1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"interpreter\": \"powershell\"", json, StringComparison.Ordinal);

        // Only .js carries an icon in SPEC 5.1; a null icon must not be written at all.
        Assert.Equal(1, json.Split("\"icon\"", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Deserialize_ToleratesCommentsAndTrailingCommas()
    {
        const string json = """
        {
          // user edited this by hand
          "version": 1,
          "securityMode": "AlwaysAsk",
          "keepWindowOpen": "Always",
          "editorCommand": "notepad",
          "logEnabled": false,
          "extensions": {
            ".js": { "interpreter": "node", "args": "\"{script}\" {args}", "enabled": true, },
          },
        }
        """;

        var config = JsonSerializer.Deserialize(json, RunlyJson.Config);

        Assert.NotNull(config);
        Assert.Equal(SecurityMode.AlwaysAsk, config.SecurityMode);
        Assert.Equal(KeepWindowMode.Always, config.KeepWindowOpen);
        Assert.False(config.LogEnabled);
        Assert.True(config.TryGetMapping("JS", out var mapping));
        Assert.Equal("node", mapping.Interpreter);
    }

    [Fact]
    public void TrustStore_SurvivesAJsonRoundTripWithoutLoss()
    {
        var original = DefaultConfig.CreateTrustStore();
        original.TrustedFolders.Add(@"C:\Users\Administrator\Desktop\Projeler");
        original.TrustedFiles[@"C:\path\to\script.js"] = new TrustedFileEntry
        {
            Sha256 = "ab12",
            AddedUtc = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
        };

        var firstPass = JsonSerializer.Serialize(original, RunlyJson.TrustStore);
        var restored = JsonSerializer.Deserialize(firstPass, RunlyJson.TrustStore);
        Assert.NotNull(restored);
        var secondPass = JsonSerializer.Serialize(restored, RunlyJson.TrustStore);

        Assert.Equal(firstPass, secondPass, ignoreLineEndingDifferences: true);
        Assert.Equal(original.TrustedFolders, restored.TrustedFolders);
        Assert.Equal(original.TrustedFiles[@"C:\path\to\script.js"],
                     restored.TrustedFiles[@"C:\path\to\script.js"]);
        Assert.Contains("\"trustedFolders\"", firstPass, StringComparison.Ordinal);
        Assert.Contains("\"sha256\"", firstPass, StringComparison.Ordinal);
        Assert.Contains("\"addedUtc\"", firstPass, StringComparison.Ordinal);
    }
}
