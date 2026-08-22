using Runly.Core.Models;

namespace Runly.Core.Abstractions;

/// <summary>Writes, inspects and removes Runly's HKCU file associations (SPEC 9).</summary>
public interface IShellRegistrar
{
    /// <summary>
    /// Backs up the affected keys, then registers the enabled extensions. Both launcher paths are required:
    /// <paramref name="exePath"/> is the GUI <c>Runly.exe</c> that carries Runly's user-visible identity and
    /// serves <see cref="HandlerKind.Open"/> mappings, <paramref name="consoleExePath"/> is the
    /// <c>RunlyConsole.exe</c> that runs scripts (K29).
    /// </summary>
    InstallResult Install(RunlyConfig config, string exePath, string consoleExePath);

    /// <summary>Removes every key Runly wrote; pass options to also replay a registry backup.</summary>
    UninstallResult Uninstall(UninstallOptions? options = null);

    /// <summary>Reports the current interpreter and binding state for each configured extension.</summary>
    IReadOnlyList<ExtensionStatus> GetStatus(RunlyConfig config);
}
