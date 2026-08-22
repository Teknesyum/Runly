using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

namespace Runly.Launcher;

/// <summary>Entry point of <c>RunlyConsole.exe</c>, the console-subsystem binary that handles <see cref="Core.Models.HandlerKind.Run"/> mappings (K29).</summary>
internal static class Program
{
    private static int Main(string[] args) => LauncherHost.Main(args, LauncherSurface.Console);
}
