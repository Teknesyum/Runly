using Runly.Core.Defaults;
using Runly.Core.Models;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>
/// Covers every branch of the SPEC 6 decision order: MOTW, trusted folder, trusted file fingerprint,
/// security mode — including the rule that NeverAsk never skips the MOTW check.
/// </summary>
public sealed class SecurityGateTests : IDisposable
{
    private readonly SecurityGate _gate = new();
    private readonly string _tempDir = Directory.CreateTempSubdirectory("runly-securitygate-tests-").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string MakeRealFile(params string[] relativeParts)
    {
        var path = Path.Combine(_tempDir, Path.Combine(relativeParts));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    private string MakeRealFolder(params string[] relativeParts)
    {
        var path = Path.Combine(_tempDir, Path.Combine(relativeParts));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ScriptInfo MakeScript(
        string path = @"C:\scripts\run.js",
        bool hasMotw = false,
        string? sha256 = "hash-current") =>
        new() { Path = path, Sha256 = sha256, HasMotw = hasMotw };

    private static InMemoryTrustStore EmptyTrustStore() => new();

    [Fact]
    public void Evaluate_MotwPresent_ReturnsMotwBlocked_RegardlessOfEverythingElse()
    {
        var script = MakeScript(hasMotw: true);
        var trustStore = EmptyTrustStore();
        trustStore.Data.TrustedFolders.Add(@"C:\scripts");
        var config = DefaultConfig.Create() with { SecurityMode = SecurityMode.NeverAsk };

        var verdict = _gate.Evaluate(script, config, trustStore);

        Assert.Equal(SecurityVerdict.MotwBlocked, verdict);
    }

    [Fact]
    public void Evaluate_NeverAsk_DoesNotSkipMotwCheck()
    {
        var script = MakeScript(hasMotw: true);
        var config = DefaultConfig.Create() with { SecurityMode = SecurityMode.NeverAsk };

        var verdict = _gate.Evaluate(script, config, EmptyTrustStore());

        Assert.Equal(SecurityVerdict.MotwBlocked, verdict);
    }

    [Fact]
    public void Evaluate_OpenHandler_SkipsTrustPromptButNeverMotw()
    {
        var config = DefaultConfig.Create() with { SecurityMode = SecurityMode.AlwaysAsk };
        Assert.Equal(SecurityVerdict.Trusted, _gate.Evaluate(MakeScript(), config, EmptyTrustStore(), HandlerKind.Open));
        Assert.Equal(SecurityVerdict.MotwBlocked, _gate.Evaluate(MakeScript(hasMotw: true), config, EmptyTrustStore(), HandlerKind.Open));
    }

    [Fact]
    public void Evaluate_ScriptUnderTrustedFolder_ReturnsTrusted()
    {
        var scriptPath = MakeRealFile("scripts", "sub", "run.js");
        var trustedFolder = MakeRealFolder("scripts");
        var script = MakeScript(scriptPath);
        var trustStore = EmptyTrustStore();
        trustStore.Data.TrustedFolders.Add(trustedFolder);
        var config = DefaultConfig.Create() with { SecurityMode = SecurityMode.AlwaysAsk };

        var verdict = _gate.Evaluate(script, config, trustStore);

        Assert.Equal(SecurityVerdict.Trusted, verdict);
    }

    [Fact]
    public void Evaluate_TrustedFileWithMatchingHash_ReturnsTrusted()
    {
        var script = MakeScript(sha256: "hash-current");
        var trustStore = EmptyTrustStore();
        trustStore.Data.TrustedFiles[script.Path] = new TrustedFileEntry { Sha256 = "hash-current" };
        var config = DefaultConfig.Create() with { SecurityMode = SecurityMode.AlwaysAsk };

        var verdict = _gate.Evaluate(script, config, trustStore);

        Assert.Equal(SecurityVerdict.Trusted, verdict);
    }

    [Fact]
    public void Evaluate_TrustedFileWithChangedHash_ReturnsChanged()
    {
        var script = MakeScript(sha256: "hash-new");
        var trustStore = EmptyTrustStore();
        trustStore.Data.TrustedFiles[script.Path] = new TrustedFileEntry { Sha256 = "hash-old" };
        var config = DefaultConfig.Create() with { SecurityMode = SecurityMode.AlwaysAsk };

        var verdict = _gate.Evaluate(script, config, trustStore);

        Assert.Equal(SecurityVerdict.Changed, verdict);
    }

    [Theory]
    [InlineData(SecurityMode.AlwaysAsk)]
    [InlineData(SecurityMode.TrustOnFirstUse)]
    public void Evaluate_NothingTrusted_AskingModes_ReturnNeedsPrompt(SecurityMode mode)
    {
        var script = MakeScript();
        var config = DefaultConfig.Create() with { SecurityMode = mode };

        var verdict = _gate.Evaluate(script, config, EmptyTrustStore());

        Assert.Equal(SecurityVerdict.NeedsPrompt, verdict);
    }

    [Fact]
    public void Evaluate_NothingTrusted_NeverAsk_ReturnsTrusted()
    {
        var script = MakeScript();
        var config = DefaultConfig.Create() with { SecurityMode = SecurityMode.NeverAsk };

        var verdict = _gate.Evaluate(script, config, EmptyTrustStore());

        Assert.Equal(SecurityVerdict.Trusted, verdict);
    }

    [Fact]
    public void Evaluate_PrefixLookalikeFolder_IsNotTreatedAsTrusted()
    {
        // C:\AB must not be considered trusted just because C:\A is a trusted folder.
        var script = MakeScript(@"C:\AB\x.js");
        var trustStore = EmptyTrustStore();
        trustStore.Data.TrustedFolders.Add(@"C:\A");
        var config = DefaultConfig.Create() with { SecurityMode = SecurityMode.AlwaysAsk };

        var verdict = _gate.Evaluate(script, config, trustStore);

        Assert.Equal(SecurityVerdict.NeedsPrompt, verdict);
    }
}
