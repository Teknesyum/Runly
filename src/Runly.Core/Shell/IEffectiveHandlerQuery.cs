using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Runly.Core.Shell;

/// <summary>
/// Asks Windows which executable actually opens a given extension right now. This is the second, independent
/// opinion required by decision K19: the registry keys Runly writes say what Runly <em>wants</em>, only the
/// shell's own association resolution says what a double click will really do.
/// </summary>
public interface IEffectiveHandlerQuery
{
    /// <summary>
    /// Full path of the executable Windows would launch for the extension, or <see langword="null"/> when the
    /// shell has no answer (no association at all, or the platform cannot be asked).
    /// </summary>
    string? GetExecutable(string extension);
}

/// <summary>
/// The "no second opinion available" implementation, used on non-Windows hosts and in unit tests. It never
/// contradicts the <c>UserChoice</c> reading, so the pessimistic rule in <see cref="ShellRegistrar"/> falls back
/// to <c>UserChoice</c> alone.
/// </summary>
public sealed class UnknownEffectiveHandlerQuery : IEffectiveHandlerQuery
{
    /// <summary>The shared instance; the class holds no state.</summary>
    public static UnknownEffectiveHandlerQuery Instance { get; } = new();

    /// <inheritdoc />
    public string? GetExecutable(string extension) => null;
}

/// <summary>
/// The real implementation, backed by <c>AssocQueryStringW(ASSOCSTR_EXECUTABLE)</c>. SPEC 3 forbids extra NuGet
/// packages, so the Win32 call is written by hand.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32EffectiveHandlerQuery : IEffectiveHandlerQuery
{
    private const int AssocfNone = 0x00000000;
    private const int AssocstrExecutable = 2;

    private const int SOk = 0;
    private const int SFalse = 1;

    /// <inheritdoc />
    public string? GetExecutable(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var normalized = RunlyRegistryLayout.NormalizeExtension(extension);

        uint length = 0;
        var probe = AssocQueryStringW(AssocfNone, AssocstrExecutable, normalized, null, null, ref length);

        if (probe is not (SOk or SFalse) || length == 0)
        {
            return null;
        }

        var buffer = new char[length];
        var status = AssocQueryStringW(AssocfNone, AssocstrExecutable, normalized, null, buffer, ref length);

        if (status != SOk)
        {
            return null;
        }

        // length now counts the terminating NUL.
        var text = new string(buffer, 0, (int)Math.Max(0, length - 1)).Trim();
        return text.Length == 0 ? null : text;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int AssocQueryStringW(
        int flags,
        int str,
        [MarshalAs(UnmanagedType.LPWStr)] string assoc,
        [MarshalAs(UnmanagedType.LPWStr)] string? extra,
        [Out] char[]? outBuffer,
        ref uint outLength);
}
