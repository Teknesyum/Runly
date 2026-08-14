using System.Text.Json.Serialization;

namespace Runly.Core.Models;

/// <summary>How Runly handles a file mapping.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HandlerKind>))]
public enum HandlerKind
{
    /// <summary>Execute the file through an interpreter and the security gate.</summary>
    Run,

    /// <summary>Open the file in an explicitly selected desktop application.</summary>
    Open,
}

/// <summary>How often the security gate asks the user before a script is allowed to run (SPEC 6).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SecurityMode>))]
public enum SecurityMode
{
    /// <summary>Prompt on every single run.</summary>
    AlwaysAsk,

    /// <summary>Prompt once per file or folder, then remember the answer. Default.</summary>
    TrustOnFirstUse,

    /// <summary>Never prompt, except for the mark-of-the-web check which can never be skipped.</summary>
    NeverAsk,
}

/// <summary>Whether the console window stays open after the child process exits (SPEC 7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<KeepWindowMode>))]
public enum KeepWindowMode
{
    /// <summary>Always wait for a key press.</summary>
    Always,

    /// <summary>Wait for a key press only when the exit code is non-zero. Default.</summary>
    OnError,

    /// <summary>Never wait; close immediately.</summary>
    Never,
}

/// <summary>The action the launcher was asked to perform on a script (SPEC 7).</summary>
public enum LaunchVerb
{
    /// <summary>Run the script normally.</summary>
    Run,

    /// <summary>Run the script elevated through ShellExecute with the "runas" verb.</summary>
    RunAs,

    /// <summary>Open the script in the configured editor without running it.</summary>
    Edit,

    /// <summary>Ask the user for arguments first, then run the script.</summary>
    PromptArgs,
}

/// <summary>Where a resolved interpreter came from (SPEC 8 resolution order).</summary>
public enum InterpreterSource
{
    /// <summary>No interpreter could be determined; the launcher must exit with <see cref="ExitCode.NoInterpreter"/>.</summary>
    None,

    /// <summary>Taken from the script's <c>#!</c> shebang line.</summary>
    Shebang,

    /// <summary>Taken from the extension mapping in <c>config.json</c>.</summary>
    Config,
}

/// <summary>The security gate's ruling for a single script (SPEC 6).</summary>
public enum SecurityVerdict
{
    /// <summary>The script may run without any prompt.</summary>
    Trusted,

    /// <summary>The user must confirm before the script runs.</summary>
    NeedsPrompt,

    /// <summary>The file carries a mark-of-the-web; a red warning dialog is mandatory in every security mode.</summary>
    MotwBlocked,

    /// <summary>The file was trusted before but its hash no longer matches.</summary>
    Changed,
}

/// <summary>Why the security dialog returned the answer it did.</summary>
public enum SecurityDecisionReason
{
    /// <summary>The user pressed the run button.</summary>
    UserApproved,

    /// <summary>The user cancelled, pressed Esc, or closed the dialog.</summary>
    UserCancelled,

    /// <summary>The user asked to see the script source before deciding; the caller should show it and ask again.</summary>
    CodeRequested,

    /// <summary>The dialog could not be shown at all; the caller must treat this as a refusal.</summary>
    DialogUnavailable,
}

/// <summary>Whether an extension is actually wired to Runly in the shell (SPEC 9).</summary>
public enum BindingState
{
    /// <summary>Double-clicking a file of this extension launches Runly.</summary>
    Bound,

    /// <summary>A Windows <c>UserChoice</c> key owned by another application blocks the binding; the user must pick Runly in the "Open with" dialog.</summary>
    NeedsUserChoice,

    /// <summary>The extension is not associated with Runly.</summary>
    NotBound,
}
