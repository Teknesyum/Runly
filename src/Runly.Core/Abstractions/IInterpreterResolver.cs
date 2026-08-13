using Runly.Core.Models;

namespace Runly.Core.Abstractions;

/// <summary>Chooses the interpreter for a script and builds its argument line (SPEC 8).</summary>
public interface IInterpreterResolver
{
    /// <summary>
    /// Resolves shebang first, then the configuration mapping; returns
    /// <see cref="ResolvedInterpreter.NotFound"/> when neither applies.
    /// </summary>
    ResolvedInterpreter Resolve(ScriptInfo script, RunlyConfig config, string[] scriptArgs);
}
