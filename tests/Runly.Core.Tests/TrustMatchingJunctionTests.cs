using System.Diagnostics;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>Covers SPEC 11.1 K21: junction/reparse-point resolution in <see cref="TrustMatching"/>.</summary>
public sealed class TrustMatchingJunctionTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("runly-junction-tests-").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static void CreateJunction(string link, string target)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{link}\" \"{target}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"mklink /J başarısız: {process.StandardError.ReadToEnd()}");
        }
    }

    [Fact]
    public void IsWithinAnyTrustedFolder_JunctionTargetOutsideTrustedFolder_ReturnsFalse()
    {
        var trustedFolder = Path.Combine(_tempDir, "trusted");
        var outsideFolder = Path.Combine(_tempDir, "outside");
        Directory.CreateDirectory(trustedFolder);
        Directory.CreateDirectory(outsideFolder);
        File.WriteAllText(Path.Combine(outsideFolder, "gizli.js"), "x");

        var junction = Path.Combine(trustedFolder, "baglanti");
        CreateJunction(junction, outsideFolder);
        try
        {
            var scriptPath = Path.Combine(junction, "gizli.js");

            var result = TrustMatching.IsWithinAnyTrustedFolder(scriptPath, [trustedFolder]);

            Assert.False(result);
        }
        finally
        {
            Directory.Delete(junction);
            Assert.False(Directory.Exists(junction));
        }
    }

    [Fact]
    public void IsWithinAnyTrustedFolder_JunctionTargetInsideTrustedFolder_ReturnsTrue()
    {
        var trustedFolder = Path.Combine(_tempDir, "trusted2");
        var actualFolder = Path.Combine(trustedFolder, "actual");
        Directory.CreateDirectory(actualFolder);
        File.WriteAllText(Path.Combine(actualFolder, "x.js"), "x");

        var junction = Path.Combine(trustedFolder, "baglanti");
        CreateJunction(junction, actualFolder);
        try
        {
            var scriptPath = Path.Combine(junction, "x.js");

            var result = TrustMatching.IsWithinAnyTrustedFolder(scriptPath, [trustedFolder]);

            Assert.True(result);
        }
        finally
        {
            Directory.Delete(junction);
            Assert.False(Directory.Exists(junction));
        }
    }

    [Fact]
    public void IsWithinAnyTrustedFolder_NoJunctionInvolved_BehavesAsBefore()
    {
        var trustedFolder = Path.Combine(_tempDir, "trusted3");
        var subFolder = Path.Combine(trustedFolder, "sub");
        Directory.CreateDirectory(subFolder);
        var scriptPath = Path.Combine(subFolder, "x.js");
        File.WriteAllText(scriptPath, "x");

        Assert.True(TrustMatching.IsWithinAnyTrustedFolder(scriptPath, [trustedFolder]));
    }

    [Fact]
    public void IsWithinAnyTrustedFolder_PrefixLookalikeFolder_StillNotTrusted()
    {
        var trustedFolder = Path.Combine(_tempDir, "A");
        var lookalikeFolder = Path.Combine(_tempDir, "AB");
        Directory.CreateDirectory(trustedFolder);
        Directory.CreateDirectory(lookalikeFolder);
        var scriptPath = Path.Combine(lookalikeFolder, "x.js");
        File.WriteAllText(scriptPath, "x");

        Assert.False(TrustMatching.IsWithinAnyTrustedFolder(scriptPath, [trustedFolder]));
    }
}
