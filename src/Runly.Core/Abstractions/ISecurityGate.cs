using Runly.Core.Models;

namespace Runly.Core.Abstractions;

/// <summary>Decides whether a script may run silently, needs confirmation, or is blocked (SPEC 6).</summary>
public interface ISecurityGate
{
    /// <summary>
    /// Applies the SPEC 6 order — mark-of-the-web, trusted folder, fingerprint, security mode — as a pure
    /// function over the supplied state, performing no file or registry access.
    /// </summary>
    SecurityVerdict Evaluate(ScriptInfo script, RunlyConfig config, ITrustStore trustStore, HandlerKind kind = HandlerKind.Run);
}
