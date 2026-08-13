namespace Runly.Core.Models;

/// <summary>What the user answered in the security dialog (SPEC 6).</summary>
public sealed record SecurityDecision
{
    /// <summary>The decision produced when the user cancels or the dialog cannot be shown.</summary>
    public static SecurityDecision Cancelled { get; } =
        new() { Allow = false, Reason = SecurityDecisionReason.UserCancelled };

    /// <summary>Whether the script is allowed to run.</summary>
    public bool Allow { get; init; }

    /// <summary>Why the dialog closed the way it did.</summary>
    public SecurityDecisionReason Reason { get; init; } = SecurityDecisionReason.UserCancelled;

    /// <summary>Whether this exact file should be remembered as trusted.</summary>
    public bool RememberFile { get; init; }

    /// <summary>Whether the containing folder should be remembered as trusted.</summary>
    public bool RememberFolder { get; init; }

    /// <summary>Whether the <c>:Zone.Identifier</c> stream should be deleted before running.</summary>
    public bool StripMotw { get; init; }
}
