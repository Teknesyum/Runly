using System.Diagnostics;
using Runly.Core.Shell;

namespace Runly.Core.Tests.Shell;

public sealed class BulkAssociationCommandTests
{
    private static readonly string[] Extensions = ["py", ".JS", ".ps1", ".sh"];

    private static string Command() => BulkAssociationCommand.Build(Extensions);

    [Fact]
    public void Every_extension_carries_its_own_progid()
    {
        var command = Command();
        foreach (var extension in new[] { ".py", ".js", ".ps1", ".sh" })
        {
            Assert.Contains($"@('{extension}','{RunlyRegistryLayout.ProgIdFor(extension)}')", command);
        }
    }

    [Fact]
    public void Blocked_extensions_are_left_out()
    {
        var command = BulkAssociationCommand.Build([".py", ".bat", ".exe", ".lnk", ".cmd"]);
        Assert.Contains("'.py'", command);
        foreach (var blocked in new[] { ".bat", ".exe", ".lnk", ".cmd" })
        {
            Assert.DoesNotContain($"'{blocked}'", command);
        }
    }

    [Fact]
    public void Ucpd_protected_and_risky_types_are_left_out()
    {
        Assert.Equal([".py"], BulkAssociationCommand.Assignable([".py", ".pdf", ".html", ".docx", ".reg", ".hta"]));
    }

    [Fact]
    public void Skips_extensions_under_userchoicelatest()
    {
        var command = Command();
        Assert.Contains(@"'\UserChoiceLatest'", command);
        Assert.Contains("continue", command);
    }

    [Fact]
    public void Duplicates_collapse_after_normalising()
    {
        Assert.Equal([".py"], BulkAssociationCommand.Assignable(["py", ".PY", " .py "]));
    }

    [Fact]
    public void Pinned_url_and_sha256_are_present()
    {
        var command = Command();
        Assert.Contains(BulkAssociationCommand.ScriptUrl, command);
        Assert.Contains(BulkAssociationCommand.ScriptSha256, command);
    }

    [Fact]
    public void Integrity_check_is_mandatory()
    {
        var command = Command();
        Assert.Contains("Unblock-File", command);
        Assert.Contains("Get-FileHash -Algorithm SHA256", command);
        Assert.Contains("-ne $h", command);
        Assert.Contains("return", command);
    }

    [Fact]
    public void Shows_progress_and_verifies_each_assignment()
    {
        var command = Command();
        Assert.Contains("Write-Progress", command);
        Assert.Contains("-PercentComplete", command);
        Assert.Contains("Get-FTA $e[$i][0]", command);
    }

    [Fact]
    public void Chains_for_powershell_5_1()
    {
        var command = Command();
        Assert.DoesNotContain("&&", command);
        Assert.DoesNotContain("||", command);
        Assert.StartsWith("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force", command);
        Assert.Contains("SecurityProtocol -bor 3072", command);
        Assert.Contains("Invoke-WebRequest -UseBasicParsing", command);
    }

    [Fact]
    public void Command_stays_ascii()
    {
        Assert.All(Command(), character => Assert.True(character < 128, $"ASCII disi: {character}"));
    }

    [Fact]
    public void Windows_powershell_5_1_parses_without_errors()
    {
        if (!OperatingSystem.IsWindows()) return;

        var file = WriteTemp(Command());
        var output = RunPowerShell("$e=$null; [void][System.Management.Automation.Language.Parser]::ParseFile('" + file + "', [ref]$null, [ref]$e); $e.Count");
        Assert.Equal("0", output);
    }

    [Theory]
    [InlineData(new[] { ".py" }, "1/1")]
    [InlineData(new[] { ".py", ".js", ".rb" }, "3/3")]
    [InlineData(new[] { ".py", ".sh" }, "1/2")]
    public void Loop_assigns_each_pair_with_stubbed_sfta(string[] extensions, string expected)
    {
        if (!OperatingSystem.IsWindows()) return;

        var command = BulkAssociationCommand.Build(extensions);
        var loop = command[(command.IndexOf(". $p; ", StringComparison.Ordinal) + 6)..];
        var stubs = @"function Test-Path($p){ $p -like '*.sh\UserChoiceLatest' }; " + "$global:set=@{};function Set-FTA($ProgId,$Extension){ $global:set[$Extension]=$ProgId }; function Get-FTA($Extension){ $global:set[$Extension] }; ";
        var output = RunPowerShell(stubs + loop);

        Assert.Contains($"Runly: {expected} uzanti atandi.", output);
    }

    private static string WriteTemp(string content)
    {
        var folder = Path.Combine(Path.GetTempPath(), "runly-tests", "bulk", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "command.ps1");
        File.WriteAllText(file, content);
        return file;
    }

    private static string RunPowerShell(string script)
    {
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", script }) info.ArgumentList.Add(argument);

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, error);
        return output.Trim();
    }
}
