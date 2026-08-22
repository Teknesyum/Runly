namespace Runly.Launcher;

/// <summary>Which of the two shipped binaries is hosting <see cref="LauncherHost"/> (K29).</summary>
internal enum LauncherSurface
{
    /// <summary>Console subsystem (<c>RunlyConsole.exe</c>): the child's output is visible and may be waited on.</summary>
    Console,

    /// <summary>GUI subsystem (<c>Runly.exe</c>): there is no console window, so everything is reported through dialogs.</summary>
    Gui,
}
