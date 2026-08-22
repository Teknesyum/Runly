using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Runly.Core.Tests")]

// K29: the two shipped launcher binaries are thin entry points over this assembly, so they need to see
// LauncherHost. Keeping it internal stops anything else from taking a dependency on the launcher internals.
[assembly: InternalsVisibleTo("Runly")]
[assembly: InternalsVisibleTo("RunlyConsole")]
