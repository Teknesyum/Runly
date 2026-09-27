using System.Security.Cryptography;
using Runly.Core.Services;
using Xunit;

namespace Runly.Core.Tests;

public class UpdateServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "runly-update-" + Guid.NewGuid().ToString("N"));

    public UpdateServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("v0.2.1", "0.2.1.0")]
    [InlineData("0.3", "0.3.0.0")]
    [InlineData("v1.0.0-beta", "1.0.0.0")]
    [InlineData("0.2.1+abc", "0.2.1.0")]
    public void ParseVersion_ReadsTags(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateService.ParseVersion(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void ParseVersion_RejectsJunk(string? tag) => Assert.Null(UpdateService.ParseVersion(tag));

    [Fact]
    public void IsNewer_ComparesNormalised()
    {
        Assert.True(UpdateService.IsNewer(new Version(0, 2, 2), new Version(0, 2, 1, 0)));
        Assert.False(UpdateService.IsNewer(new Version(0, 2, 1), new Version(0, 2, 1, 0)));
        Assert.False(UpdateService.IsNewer(new Version(0, 2), new Version(0, 2, 1)));
    }

    [Fact]
    public void ParseChecksum_TakesFirstToken()
    {
        var hash = new string('A', 64);
        Assert.Equal(new string('a', 64), UpdateService.ParseChecksum(hash + "  Runly-v0.2.1-win-x64.zip\n"));
        Assert.Null(UpdateService.ParseChecksum("abc Runly.zip"));
        Assert.Null(UpdateService.ParseChecksum(null));
    }

    [Fact]
    public void ParseRelease_PicksPackageAndChecksum()
    {
        const string json = """
        {"tag_name":"v0.3.0","assets":[
          {"name":"notes.txt","browser_download_url":"https://example.test/notes.txt"},
          {"name":"Runly-v0.3.0-win-x64.zip","browser_download_url":"https://example.test/r.zip"},
          {"name":"Runly-v0.3.0-win-x64.zip.sha256","browser_download_url":"https://example.test/r.zip.sha256"}]}
        """;
        var release = UpdateService.ParseRelease(json);
        Assert.NotNull(release);
        Assert.Equal(new Version(0, 3, 0, 0), release!.Version);
        Assert.Equal("Runly-v0.3.0-win-x64.zip", release.AssetName);
        Assert.Equal("https://example.test/r.zip.sha256", release.ChecksumUrl!.ToString());
    }

    [Fact]
    public void ParseRelease_NullWithoutPackage() =>
        Assert.Null(UpdateService.ParseRelease("""{"tag_name":"v0.3.0","assets":[]}"""));

    [Fact]
    public void VerifyChecksum_RejectsMismatch()
    {
        var file = Path.Combine(_root, "a.bin");
        File.WriteAllText(file, "runly");
        var good = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        UpdateService.VerifyChecksum(file, good);
        Assert.Throws<InvalidDataException>(() => UpdateService.VerifyChecksum(file, new string('0', 64)));
    }

    [Fact]
    public void Apply_RenamesToOldThenCleanupRemoves()
    {
        var package = Path.Combine(_root, "paket");
        var install = Path.Combine(_root, "kurulu");
        Directory.CreateDirectory(Path.Combine(package, "locale"));
        Directory.CreateDirectory(install);
        foreach (var name in UpdateService.RequiredFiles)
        {
            File.WriteAllText(Path.Combine(package, name), "yeni");
            File.WriteAllText(Path.Combine(install, name), "eski");
        }

        File.WriteAllText(Path.Combine(package, "locale", "tr.json"), "{}");

        UpdateService.Apply(package, install);

        Assert.Equal("yeni", File.ReadAllText(Path.Combine(install, "Runly.exe")));
        Assert.Equal("eski", File.ReadAllText(Path.Combine(install, "Runly.exe.old")));
        Assert.True(File.Exists(Path.Combine(install, "locale", "tr.json")));
        Assert.Equal(3, UpdateService.CleanupOld(install));
        Assert.False(File.Exists(Path.Combine(install, "Runly.exe.old")));
    }

    [Fact]
    public void Apply_RefusesIncompletePackage()
    {
        var package = Path.Combine(_root, "eksik");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "Runly.exe"), "x");
        Assert.Throws<InvalidDataException>(() => UpdateService.Apply(package, _root));
    }
}
