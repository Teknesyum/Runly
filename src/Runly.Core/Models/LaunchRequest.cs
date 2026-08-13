namespace Runly.Core.Models;

/// <summary>One parsed <c>Runly.exe</c> command line (SPEC 7).</summary>
public sealed record LaunchRequest
{
    /// <summary>Full path of the script to act on.</summary>
    public string ScriptPath { get; init; } = string.Empty;

    /// <summary>The requested action.</summary>
    public LaunchVerb Verb { get; init; } = LaunchVerb.Run;

    /// <summary>Arguments forwarded to the script, already split into individual tokens.</summary>
    public string[] ScriptArgs { get; init; } = [];

    /// <summary>When set, this run behaves as if <c>keepWindowOpen</c> were <see cref="KeepWindowMode.Never"/>.</summary>
    public bool NoWait { get; init; }
}
