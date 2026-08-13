using Runly.Core.Abstractions;
using Runly.Core.Models;

namespace Runly.Core.Services;

/// <summary>
/// Pure decision core of Runly (SPEC 6). Applies the four-step order — mark-of-the-web, trusted folder,
/// trusted file fingerprint, security mode — without touching disk or the registry itself; it only reads the
/// <see cref="ScriptInfo"/>, <see cref="RunlyConfig"/> and <see cref="ITrustStore.Data"/> it is handed.
/// </summary>
public sealed class SecurityGate : ISecurityGate
{
    /// <inheritdoc />
    public SecurityVerdict Evaluate(ScriptInfo script, RunlyConfig config, ITrustStore trustStore)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(trustStore);

        // Step 1: mark-of-the-web always wins, regardless of securityMode.
        if (script.HasMotw)
        {
            return SecurityVerdict.MotwBlocked;
        }

        var data = trustStore.Data;

        // Step 2: a trusted folder silences the gate for every script beneath it.
        if (TrustMatching.IsWithinAnyTrustedFolder(script.Path, data.TrustedFolders))
        {
            return SecurityVerdict.Trusted;
        }

        // Step 3: an individually trusted file must still match its remembered hash.
        if (TrustMatching.TryGetTrustedFile(script.Path, data.TrustedFiles, out var entry))
        {
            var hashMatches = script.Sha256 is not null &&
                               string.Equals(entry.Sha256, script.Sha256, StringComparison.OrdinalIgnoreCase);
            return hashMatches ? SecurityVerdict.Trusted : SecurityVerdict.Changed;
        }

        // Step 4: nothing trusted it yet — NeverAsk silently allows, everything else prompts.
        return config.SecurityMode == SecurityMode.NeverAsk
            ? SecurityVerdict.Trusted
            : SecurityVerdict.NeedsPrompt;
    }
}
