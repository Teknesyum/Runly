using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Runly.Core.Shell;

/// <summary>Tells Explorer that file associations changed, behind an interface so tests stay side effect free.</summary>
public interface IShellNotifier
{
    /// <summary>Raises the shell's "associations changed" notification so icons and menus refresh.</summary>
    void AssociationsChanged();
}

/// <summary>The real notification, via <c>SHChangeNotify(SHCNE_ASSOCCHANGED, …)</c> (SPEC 9 step 4).</summary>
[SupportedOSPlatform("windows")]
public sealed class Win32ShellNotifier : IShellNotifier
{
    private const int ShcneAssocChanged = 0x08000000;
    private const uint ShcnfIdList = 0x0000;

    /// <inheritdoc />
    public void AssociationsChanged() =>
        SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}

/// <summary>A notifier that does nothing; used by tests and by callers that refresh Explorer themselves.</summary>
public sealed class NullShellNotifier : IShellNotifier
{
    /// <summary>The shared instance.</summary>
    public static NullShellNotifier Instance { get; } = new();

    /// <inheritdoc />
    public void AssociationsChanged()
    {
        // Nothing to do.
    }
}
