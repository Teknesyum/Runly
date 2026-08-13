using Runly.Core.Models;

namespace Runly.Core.Abstractions;

/// <summary>Starts the interpreter and waits for it, without redirecting the child's console (SPEC 7).</summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Runs the interpreter in the given working directory and returns the child's exit code;
    /// a refused elevation prompt yields <see cref="ExitCode.UserCancelled"/> rather than an exception.
    /// </summary>
    int Launch(ResolvedInterpreter interpreter, string workingDirectory, bool elevated);
}
