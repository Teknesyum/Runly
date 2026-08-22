using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

namespace Runly.Launcher;

/// <summary>Entry point of <c>Runly.exe</c>, the GUI-subsystem binary that handles <see cref="Core.Models.HandlerKind.Open"/> mappings (K29).</summary>
internal static class Program
{
    private static int Main(string[] args) => LauncherHost.Main(args, LauncherSurface.Gui);
}
