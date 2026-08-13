using Runly.Core.Models;

namespace Runly.Core.Abstractions;

/// <summary>The user-facing dialogs the launcher needs; the only route from core logic to the screen (SPEC 6).</summary>
public interface IDialogService
{
    /// <summary>
    /// Shows the security dialog for the given verdict and the exact command line that would run.
    /// Returns <see langword="null"/> when the dialog could not be shown, which the caller must treat as a refusal.
    /// </summary>
    SecurityDecision? AskSecurity(ScriptInfo script, string commandLine, SecurityVerdict verdict);

    /// <summary>Asks for script arguments; returns <see langword="null"/> when the user cancels.</summary>
    string? AskArgs(ScriptInfo script);

    /// <summary>Shows a Turkish error message to the user.</summary>
    void ShowError(string title, string body);
}
