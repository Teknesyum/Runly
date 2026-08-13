namespace Runly.Core.Models;

/// <summary>Process exit codes returned by <c>Runly.exe</c> (SPEC 7).</summary>
public static class ExitCode
{
    /// <summary>The script ran and reported success.</summary>
    public const int Success = 0;

    /// <summary>The script reported a failure; any other non-zero child exit code is passed through verbatim.</summary>
    public const int ScriptFailed = 1;

    /// <summary>Runly was invoked incorrectly, or the target file does not exist.</summary>
    public const int UsageError = 2;

    /// <summary>No interpreter could be resolved for the script.</summary>
    public const int NoInterpreter = 3;

    /// <summary>The user cancelled at the security gate.</summary>
    public const int UserCancelled = 4;
}
