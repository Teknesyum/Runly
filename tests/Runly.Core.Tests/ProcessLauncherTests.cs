using System.Runtime.Versioning;
using Runly.Core.Models;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>Covers exit-code passthrough for a real child process and the not-resolved/not-startable failure paths.</summary>
[SupportedOSPlatform("windows")]
public sealed class ProcessLauncherTests
{
    private static readonly string CmdExePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

    [Fact]
    public void Launch_UnresolvedInterpreter_ReturnsNoInterpreter_WithoutStartingAnything()
    {
        var launcher = new ProcessLauncher();

        var exitCode = launcher.Launch(ResolvedInterpreter.NotFound, Path.GetTempPath(), elevated: false);

        Assert.Equal(ExitCode.NoInterpreter, exitCode);
    }

    [Fact]
    public void Launch_RealProcess_PassesThroughExitCode()
    {
        var launcher = new ProcessLauncher();
        var interpreter = new ResolvedInterpreter
        {
            ExecutablePath = CmdExePath,
            ArgumentLine = "/c exit 5",
            Source = InterpreterSource.Config,
        };

        var exitCode = launcher.Launch(interpreter, Path.GetTempPath(), elevated: false);

        Assert.Equal(5, exitCode);
    }

    [Fact]
    public void Launch_SuccessfulProcess_ReturnsZero()
    {
        var launcher = new ProcessLauncher();
        var interpreter = new ResolvedInterpreter
        {
            ExecutablePath = CmdExePath,
            ArgumentLine = "/c exit 0",
            Source = InterpreterSource.Config,
        };

        var exitCode = launcher.Launch(interpreter, Path.GetTempPath(), elevated: false);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Launch_ExecutableDoesNotExist_ReturnsNoInterpreter_DoesNotThrow()
    {
        var launcher = new ProcessLauncher();
        var interpreter = new ResolvedInterpreter
        {
            ExecutablePath = Path.Combine(Path.GetTempPath(), "runly-does-not-exist-" + Guid.NewGuid() + ".exe"),
            ArgumentLine = string.Empty,
            Source = InterpreterSource.Config,
        };

        var exitCode = launcher.Launch(interpreter, Path.GetTempPath(), elevated: false);

        Assert.Equal(ExitCode.NoInterpreter, exitCode);
    }

    [Fact]
    public void Launch_CmdTarget_RunsThroughCommandProcessor_AndPassesThroughExitCode()
    {
        var dir = CreateTempDir();
        try
        {
            var batch = Path.Combine(dir, "shim.cmd");
            File.WriteAllText(batch, "@echo off\r\nexit /b 7\r\n");

            var exitCode = new ProcessLauncher().Launch(
                new ResolvedInterpreter { ExecutablePath = batch, ArgumentLine = string.Empty, Source = InterpreterSource.Config },
                dir,
                elevated: false);

            Assert.Equal(7, exitCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Launch_BatTarget_ReceivesArgumentsAndWorkingDirectory()
    {
        var dir = CreateTempDir();
        try
        {
            var batch = Path.Combine(dir, "echo args.bat");
            var outFile = Path.Combine(dir, "out.txt");
            File.WriteAllText(batch, "@echo off\r\n> \"%~dp0out.txt\" echo %1 %2\r\n");

            var exitCode = new ProcessLauncher().Launch(
                new ResolvedInterpreter { ExecutablePath = batch, ArgumentLine = "alpha beta", Source = InterpreterSource.Config },
                dir,
                elevated: false);

            Assert.Equal(0, exitCode);
            Assert.Equal("alpha beta", File.ReadAllText(outFile).Trim());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Launch_CmdTarget_ShellMetacharactersInArgument_AreNotInterpreted()
    {
        var dir = CreateTempDir();
        try
        {
            var batch = Path.Combine(dir, "capture.cmd");
            var outFile = Path.Combine(dir, "out.txt");
            var marker = Path.Combine(dir, "injected.txt");
            File.WriteAllText(batch, "@echo off\r\n> \"%~dp0out.txt\" echo %1\r\n");

            var exitCode = new ProcessLauncher().Launch(
                new ResolvedInterpreter
                {
                    ExecutablePath = batch,
                    ArgumentLine = $"\"a&b|c<d>e^f\" & echo pwned > \"{marker}\"",
                    Source = InterpreterSource.Config,
                },
                dir,
                elevated: false);

            Assert.Equal(0, exitCode);
            Assert.Equal("\"a&b|c<d>e^f\"", File.ReadAllText(outFile).Trim());
            Assert.False(File.Exists(marker));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Launch_ExeTarget_IsNotWrappedInCommandProcessor()
    {
        Assert.False(ProcessLauncher.IsBatchTarget(CmdExePath));
        Assert.True(ProcessLauncher.IsBatchTarget(@"C:\tools\tsx.CMD"));
        Assert.True(ProcessLauncher.IsBatchTarget(@"C:\tools\x.bat"));

        var launcher = new ProcessLauncher();
        var interpreter = new ResolvedInterpreter
        {
            ExecutablePath = CmdExePath,
            ArgumentLine = "/c exit 3",
            Source = InterpreterSource.Config,
        };

        Assert.Equal(3, launcher.Launch(interpreter, Path.GetTempPath(), elevated: false));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "runly-t7-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Open_RejectsRelativeNonExeAndRunlyTargets()
    {
        var launcher = new ProcessLauncher();
        var file = new ScriptInfo { Path = @"C:\docs\readme.md" };

        Assert.Equal(OpenLaunchResult.NotSelected, launcher.Open(null, "\"{script}\"", file, []));
        Assert.Equal(OpenLaunchResult.InvalidExecutable, launcher.Open("notepad.exe", "\"{script}\"", file, []));
        Assert.Equal(OpenLaunchResult.InvalidExecutable, launcher.Open(@"C:\tools\handler.cmd", "\"{script}\"", file, []));
        Assert.Equal(OpenLaunchResult.Recursive, launcher.Open(@"C:\elsewhere\Runly.exe", "\"{script}\"", file, [], @"C:\installed\Runly.exe"));
        Assert.Equal(OpenLaunchResult.NotFound, launcher.Open(@"C:\missing\viewer.exe", "\"{script}\"", file, []));
    }
}
