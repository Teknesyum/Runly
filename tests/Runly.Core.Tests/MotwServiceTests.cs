using System.Runtime.Versioning;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>Covers MOTW absence, presence with a trusted/internet zone id, and stripping — using real ADS streams on this NTFS volume.</summary>
[SupportedOSPlatform("windows")]
public sealed class MotwServiceTests : IDisposable
{
    private readonly string _tempDir;

    public MotwServiceTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("runly-motw-tests-").FullName;
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateFile(string name)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "content");
        return path;
    }

    private static void WriteZoneIdentifier(string path, int zoneId) =>
        File.WriteAllText(path + ":Zone.Identifier", $"[ZoneTransfer]\r\nZoneId={zoneId}\r\n");

    [Fact]
    public void HasMotw_NoAdsStream_ReturnsFalse()
    {
        var path = CreateFile("plain.js");
        var motw = new MotwService();

        Assert.False(motw.HasMotw(path));
        Assert.Null(motw.GetZoneId(path));
    }

    [Fact]
    public void HasMotw_ZoneId3Internet_ReturnsTrue()
    {
        var path = CreateFile("downloaded.js");
        WriteZoneIdentifier(path, 3);
        var motw = new MotwService();

        Assert.True(motw.HasMotw(path));
        Assert.Equal(3, motw.GetZoneId(path));
    }

    [Fact]
    public void HasMotw_ZoneId1Intranet_ReturnsFalse()
    {
        var path = CreateFile("intranet.js");
        WriteZoneIdentifier(path, 1);
        var motw = new MotwService();

        Assert.False(motw.HasMotw(path));
        Assert.Equal(1, motw.GetZoneId(path));
    }

    [Fact]
    public void Strip_RemovesZoneIdentifier()
    {
        var path = CreateFile("downloaded.js");
        WriteZoneIdentifier(path, 3);
        var motw = new MotwService();

        motw.Strip(path);

        Assert.False(motw.HasMotw(path));
    }

    [Fact]
    public void Strip_NoAdsStream_DoesNotThrow()
    {
        var path = CreateFile("plain.js");
        var motw = new MotwService();

        var exception = Record.Exception(() => motw.Strip(path));

        Assert.Null(exception);
    }
}
