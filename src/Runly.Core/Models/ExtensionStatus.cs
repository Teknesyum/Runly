namespace Runly.Core.Models;

/// <summary>One row of the extension table in the settings GUI (SPEC 10.2).</summary>
public sealed record ExtensionStatus
{
    /// <summary>Extension including the leading dot.</summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>Whether the configured interpreter was found on PATH or at its absolute path.</summary>
    public bool InterpreterFound { get; init; }

    /// <summary>Full path of the interpreter when it was found, otherwise <see langword="null"/>.</summary>
    public string? InterpreterPath { get; init; }

    /// <summary>Whether the extension is actually wired to Runly in the shell.</summary>
    public BindingState Bound { get; init; } = BindingState.NotBound;

    /// <summary>
    /// Readable name of the application holding the Windows <c>UserChoice</c> key when
    /// <see cref="Bound"/> is <see cref="BindingState.NeedsUserChoice"/>, otherwise <see langword="null"/>.
    /// Packaged applications report "bir Microsoft Store uygulaması" (decision K6, SPEC 11.1).
    /// </summary>
    public string? UserChoiceOwnerName { get; init; }
}
