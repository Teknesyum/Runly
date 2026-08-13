using System.Diagnostics.CodeAnalysis;

namespace Runly.Core.Models;

/// <summary>The interpreter chosen for a script plus the fully expanded argument line (SPEC 8).</summary>
public sealed record ResolvedInterpreter
{
    /// <summary>The result returned when no interpreter could be determined.</summary>
    public static ResolvedInterpreter NotFound { get; } = new();

    /// <summary>Full path of the interpreter executable, or <see langword="null"/> when unresolved.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>Argument line with <c>{script}</c>, <c>{args}</c> and <c>{dir}</c> already expanded and quoted.</summary>
    public string ArgumentLine { get; init; } = string.Empty;

    /// <summary>Where the interpreter choice came from.</summary>
    public InterpreterSource Source { get; init; } = InterpreterSource.None;

    /// <summary>Whether an executable was actually found.</summary>
    [MemberNotNullWhen(true, nameof(ExecutablePath))]
    public bool IsResolved => Source != InterpreterSource.None && !string.IsNullOrEmpty(ExecutablePath);

    /// <summary>The exact command line shown to the user in the security dialog (SPEC 6).</summary>
    public string CommandLine =>
        IsResolved ? $"\"{ExecutablePath}\" {ArgumentLine}".TrimEnd() : string.Empty;
}
