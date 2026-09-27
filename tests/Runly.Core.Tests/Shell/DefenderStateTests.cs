using Runly.Core.Shell;

namespace Runly.Core.Tests.Shell;

public class DefenderStateTests
{
    private const string DefenderKey = @"SOFTWARE\Microsoft\Windows Defender";
    private const string RealtimeKey = DefenderKey + @"\Real-Time Protection";
    private const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\WinDefend";

    private readonly FakeRegistryAccessor _registry = new();

    [Fact]
    public void A_silent_registry_counts_as_active() => Assert.True(DefenderState.IsActive(_registry));

    [Fact]
    public void Disabled_antispyware_turns_it_off()
    {
        _registry.Seed(RegistryRoot.LocalMachine, DefenderKey, RegistryValueEntry.FromDWord("DisableAntiSpyware", 1u));
        Assert.False(DefenderState.IsActive(_registry));
    }

    [Fact]
    public void Realtime_monitoring_off_turns_it_off()
    {
        _registry.Seed(RegistryRoot.LocalMachine, RealtimeKey, RegistryValueEntry.FromDWord("DisableRealtimeMonitoring", 1u));
        Assert.False(DefenderState.IsActive(_registry));
    }

    [Fact]
    public void A_disabled_service_turns_it_off()
    {
        _registry.Seed(RegistryRoot.LocalMachine, ServiceKey, RegistryValueEntry.FromDWord("Start", 4u));
        Assert.False(DefenderState.IsActive(_registry));
    }

    [Fact]
    public void An_automatic_service_leaves_it_on()
    {
        _registry.Seed(RegistryRoot.LocalMachine, ServiceKey, RegistryValueEntry.FromDWord("Start", 2u));
        Assert.True(DefenderState.IsActive(_registry));
    }
}
