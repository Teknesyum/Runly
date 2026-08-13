using Runly.Core.Models;

namespace Runly.Core.Abstractions;

/// <summary>Gathers size, timestamp, hash, mark-of-the-web, leading lines and shebang for a script file.</summary>
public interface IScriptInspector
{
    /// <summary>Inspects the file at the given path; throws when the file does not exist or cannot be read.</summary>
    ScriptInfo Inspect(string path);
}
