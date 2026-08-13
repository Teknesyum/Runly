using Runly.Core.Models;

namespace Runly.Core.Abstractions;

/// <summary>Writes, inspects and removes Runly's HKCU file associations (SPEC 9).</summary>
public interface IShellRegistrar
{
    /// <summary>Backs up the affected keys, then registers the enabled extensions for the given <c>Runly.exe</c> path.</summary>
    InstallResult Install(RunlyConfig config, string exePath);

    /// <summary>Removes every key Runly wrote; pass options to also replay a registry backup.</summary>
    UninstallResult Uninstall(UninstallOptions? options = null);

    /// <summary>Reports the current interpreter and binding state for each configured extension.</summary>
    IReadOnlyList<ExtensionStatus> GetStatus(RunlyConfig config);
}
