namespace Runly.Core.Models;

/// <summary>
/// One extension whose Windows <c>UserChoice</c> key still pointed at a Runly ProgID when Runly was removed
/// (decision K20). Windows protects that key with an ACL, so removal is attempted but may well fail; either way
/// the user has to be told, and "temiz kaldırıldı" must not be claimed while <see cref="Removed"/> is false.
/// </summary>
public sealed record OrphanedUserChoice
{
    /// <summary>Extension including the leading dot.</summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>The Runly ProgID the key was pointing at.</summary>
    public string ProgId { get; init; } = string.Empty;

    /// <summary>Whether the <c>UserChoice</c> key is gone after the attempt.</summary>
    public bool Removed { get; init; }

    /// <summary>Why removal failed, in Turkish, when <see cref="Removed"/> is <see langword="false"/>.</summary>
    public string? FailureReason { get; init; }
}
