using Runly.Core.Abstractions;
using Runly.Core.Models;
using Runly.Launcher;

namespace Runly.Core.Tests;

/// <summary>
/// Guards the exit code against the "keep the window open" step (gsudo#421): whatever the wait does,
/// the code the caller sees is the child's.
/// </summary>
public sealed class ScriptRunnerTests
{
    private static readonly ResolvedInterpreter Interpreter = new()
    {
        ExecutablePath = @"C:\nodejs\node.exe",
        ArgumentLine = "\"C:\\scripts\\build.js\"",
        Source = InterpreterSource.Config,
    };

    private sealed class StubLauncher(int exitCode) : IProcessLauncher
    {
        internal bool Launched { get; private set; }

        public int Launch(ResolvedInterpreter interpreter, string workingDirectory, bool elevated)
        {
            Launched = true;
            return exitCode;
        }
    }

    private sealed class SilentLogger : ILogger
    {
        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }

    [Fact]
    public void The_child_exit_code_survives_the_wait()
    {
        var launcher = new StubLauncher(42);
        var waited = false;

        var exitCode = ScriptRunner.RunAndWait(
            launcher, Interpreter, @"C:\scripts", elevated: false,
            (_, _) => waited = true,
            new SilentLogger());

        Assert.True(launcher.Launched);
        Assert.True(waited);
        Assert.Equal(42, exitCode);
    }

    [Fact]
    public void The_wait_runs_before_the_return_and_sees_the_child_code()
    {
        int? seen = null;

        var exitCode = ScriptRunner.RunAndWait(
            new StubLauncher(3), Interpreter, @"C:\scripts", elevated: false,
            (code, _) => seen = code,
            new SilentLogger());

        Assert.Equal(3, seen);
        Assert.Equal(3, exitCode);
    }

    [Fact]
    public void A_wait_that_fails_does_not_swallow_the_child_exit_code()
    {
        // Reading a key needs a console input handle. When there is none the wait throws, and an
        // unguarded throw here would turn the script's failure into Runly's own exit code.
        var exitCode = ScriptRunner.RunAndWait(
            new StubLauncher(7), Interpreter, @"C:\scripts", elevated: false,
            (_, _) => throw new InvalidOperationException("no console input handle"),
            new SilentLogger());

        Assert.Equal(7, exitCode);
    }

    [Fact]
    public void A_successful_run_is_still_reported_as_success()
    {
        var exitCode = ScriptRunner.RunAndWait(
            new StubLauncher(ExitCode.Success), Interpreter, @"C:\scripts", elevated: false,
            (_, _) => { },
            new SilentLogger());

        Assert.Equal(ExitCode.Success, exitCode);
    }
}
