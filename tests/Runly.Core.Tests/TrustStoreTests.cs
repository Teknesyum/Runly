using Runly.Core.Models;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>Proves <see cref="TrustStoreService"/>'s folder-prefix matching, hash tracking and corruption recovery.</summary>
public sealed class TrustStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _trustPath;

    public TrustStoreTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("runly-trust-tests-").FullName;
        _trustPath = Path.Combine(_tempDir, "trust.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static ScriptInfo MakeScript(string path, string? sha256 = "abc123") =>
        new() { Path = path, Sha256 = sha256 };

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

    [Fact]
    public void IsTrusted_ReturnsFalse_WhenNothingTrustedYet()
    {
        var store = new TrustStoreService(_trustPath);
        store.Load();

        Assert.False(store.IsTrusted(MakeScript(MakeRealFile("A", "x.js"))));
    }

    [Fact]
    public void IsTrusted_TrueForSubfolder_ButFalseForPrefixLookalike()
    {
        var store = new TrustStoreService(_trustPath);
        store.Load();
        var trustedFolder = MakeRealFolder("A");
        store.TrustFolder(trustedFolder);

        Assert.True(store.IsTrusted(MakeScript(MakeRealFile("A", "x.js"))));
        Assert.True(store.IsTrusted(MakeScript(MakeRealFile("A", "B", "x.js"))));
        // C:\AB is not a subfolder of C:\A even though it shares the string prefix "C:\A".
        Assert.False(store.IsTrusted(MakeScript(MakeRealFile("AB", "x.js"))));
    }

    [Fact]
    public void IsTrusted_TrustedFile_TrueWhenHashMatches_FalseWhenHashChanged()
    {
        var store = new TrustStoreService(_trustPath);
        store.Load();
        var script = MakeScript(@"C:\scripts\run.js", sha256: "hash-v1");
        store.TrustFile(script);

        Assert.True(store.IsTrusted(script));
        Assert.False(store.IsTrusted(MakeScript(script.Path, sha256: "hash-v2")));
    }

    [Fact]
    public void TrustFile_AddedTwice_ReplacesEarlierHash()
    {
        var store = new TrustStoreService(_trustPath);
        store.Load();
        var path = @"C:\scripts\run.js";

        store.TrustFile(MakeScript(path, sha256: "hash-v1"));
        store.TrustFile(MakeScript(path, sha256: "hash-v2"));

        Assert.Single(store.Data.TrustedFiles);
        Assert.True(store.IsTrusted(MakeScript(path, sha256: "hash-v2")));
        Assert.False(store.IsTrusted(MakeScript(path, sha256: "hash-v1")));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsFoldersAndFiles()
    {
        var store = new TrustStoreService(_trustPath);
        store.Load();
        var projectsFolder = MakeRealFolder("Projects");
        var scriptFile = MakeRealFile("scripts", "run.js");
        store.TrustFolder(projectsFolder);
        store.TrustFile(MakeScript(scriptFile, sha256: "hash-v1"));
        store.Save();

        var reloaded = new TrustStoreService(_trustPath);
        reloaded.Load();

        Assert.True(reloaded.IsTrusted(MakeScript(MakeRealFile("Projects", "sub", "a.ps1"))));
        Assert.True(reloaded.IsTrusted(MakeScript(scriptFile, sha256: "hash-v1")));
    }

    [Fact]
    public void UntrustFolder_RemovesOnlyTheMatchingFolder()
    {
        var store = new TrustStoreService(_trustPath);
        store.Load();
        var folderA = MakeRealFolder("A");
        var folderB = MakeRealFolder("B");
        store.TrustFolder(folderA);
        store.TrustFolder(folderB);

        store.UntrustFolder(folderA);

        Assert.False(store.IsTrusted(MakeScript(MakeRealFile("A", "x.js"))));
        Assert.True(store.IsTrusted(MakeScript(MakeRealFile("B", "x.js"))));
    }

    [Fact]
    public void ClearTrustedFiles_RemovesAllFileEntries_ButKeepsFolders()
    {
        var store = new TrustStoreService(_trustPath);
        store.Load();
        var folderA = MakeRealFolder("A");
        store.TrustFolder(folderA);
        store.TrustFile(MakeScript(MakeRealFile("scripts", "run.js")));

        store.ClearTrustedFiles();

        Assert.Empty(store.Data.TrustedFiles);
        Assert.True(store.IsTrusted(MakeScript(MakeRealFile("A", "x.js"))));
    }

    [Fact]
    public void Load_WhenFileIsCorruptJson_RenamesToBakAndYieldsEmptyStore()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_trustPath, "not json at all {{{");

        var store = new TrustStoreService(_trustPath);
        store.Load();

        Assert.Empty(store.Data.TrustedFolders);
        Assert.True(File.Exists(_trustPath + ".bak"));
    }

    [Fact]
    public void Load_ValidJsonWithNullAndMalformedPaths_NormalizesToSafeEmptyCollections()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_trustPath, """
            { "version": 1, "trustedFolders": [null, "\u0000bad"], "trustedFiles": null }
            """);

        var store = new TrustStoreService(_trustPath);
        store.Load();

        Assert.NotNull(store.Data.TrustedFolders);
        Assert.NotNull(store.Data.TrustedFiles);
        Assert.Empty(store.Data.TrustedFolders);
        Assert.Empty(store.Data.TrustedFiles);
    }
}
