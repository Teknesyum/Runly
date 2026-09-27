namespace Runly.Core.Shell;

/// <summary>
/// Reads whether Microsoft Defender's real-time protection is doing anything. The scan verb stays in
/// the menu even when Defender is switched off, and asking a stopped service to scan a file is the
/// kind of entry the menu can lose without losing function.
/// </summary>
public static class DefenderState
{
    private const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\WinDefend";
    private const string PolicyKey = @"SOFTWARE\Microsoft\Windows Defender";
    private const string RealTimeKey = PolicyKey + @"\Real-Time Protection";

    /// <summary>
    /// Whether Defender is in a state where "Scan with Microsoft Defender" still does something.
    /// WMI is not used on purpose: the launcher and the scanner are NativeAOT.
    /// </summary>
    public static bool IsActive(IRegistryAccessor registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (registry.GetValue(RegistryRoot.LocalMachine, PolicyKey, "DisableAntiSpyware")?.AsDWord() == 1)
        {
            return false;
        }

        if (registry.GetValue(RegistryRoot.LocalMachine, RealTimeKey, "DisableRealtimeMonitoring")?.AsDWord() == 1)
        {
            return false;
        }

        // 4 is "disabled"; anything else (2 automatic, 3 manual) leaves the service startable.
        var start = registry.GetValue(RegistryRoot.LocalMachine, ServiceKey, "Start")?.AsDWord();
        return start != 4;
    }
}
