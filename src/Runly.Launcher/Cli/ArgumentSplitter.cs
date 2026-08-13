using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Runly.Launcher.Ui;

namespace Runly.Launcher.Cli;

/// <summary>Splits the free-text argument string typed in the <c>prompt-args</c> box into tokens.</summary>
[SupportedOSPlatform("windows")]
internal static class ArgumentSplitter
{
    /// <summary>
    /// Uses <c>CommandLineToArgvW</c> so the user's quoting behaves exactly as it would in a real
    /// command line; a dummy program name is prepended because the first token follows different rules.
    /// </summary>
    internal static string[] Split(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var block = NativeMethods.CommandLineToArgvW("runly " + commandLine, out var count);
        if (block == 0 || count <= 1)
        {
            return [];
        }

        try
        {
            var result = new List<string>(count - 1);
            for (var i = 1; i < count; i++)
            {
                var pointer = Marshal.ReadIntPtr(block, i * nint.Size);
                var value = Marshal.PtrToStringUni(pointer);
                if (value is not null)
                {
                    result.Add(value);
                }
            }

            return [.. result];
        }
        finally
        {
            NativeMethods.LocalFree(block);
        }
    }
}
