using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Runly.Core.Shell;

/// <summary>
/// Shows the Windows "Open with" dialog through <c>SHOpenWithDialog</c> so a file can be run with Runly once.
/// It cannot bind an extension: since Windows 10 this dialog is documented as unable to change the default
/// program, whatever flags it is given (see <see cref="OaifExec"/>). Binding is the user's to make, through
/// Explorer's "Her zaman" or the Settings page — <c>UserChoice</c> is hash protected and SPEC 2 forbids forging it.
/// </summary>
[SupportedOSPlatform("windows")]
public static class OpenWithDialog
{
    /// <summary>
    /// Run the chosen application on the sample file once the user confirms. The only flag Windows still acts on
    /// here, so it is the only one sent.
    /// </summary>
    /// <remarks>
    /// The registration flags — <c>OAIF_ALLOW_REGISTRATION</c> (0x1), <c>OAIF_REGISTER_EXT</c> (0x2),
    /// <c>OAIF_FORCE_REGISTRATION</c> (0x8), <c>OAIF_HIDE_REGISTRATION</c> (0x20) — are not merely weak here, they
    /// are ignored: Microsoft's own <c>OPENASINFO</c> reference states that as of Windows 10 "the Open With dialog
    /// box can no longer be used to change the default program". R1 measured this on Windows 11 and recorded it as
    /// a Windows 11 trait (decision K23); it is in fact documented behaviour two releases older, so no future
    /// Windows version is going to give the flags back. Sending them only invited the reader to try them again.
    /// Binding therefore goes through Explorer's "Birlikte aç → Başka bir uygulama seç → Her zaman" or the Settings
    /// page; the settings GUI offers both. This dialog is kept because it is still the fastest way to run a file
    /// with Runly once.
    /// </remarks>
    private const int OaifExec = 0x00000004;

    /// <summary><c>HRESULT_FROM_WIN32(ERROR_CANCELLED)</c> — the user closed the dialog without choosing.</summary>
    private const int ErrorCancelled = unchecked((int)0x800704C7);

    /// <summary>
    /// <c>RPC_S_CALL_FAILED</c> — the out-of-process dialog host went away before the user chose. From Runly's
    /// point of view that is indistinguishable from a cancellation, so it is reported as one rather than as a
    /// crash the settings GUI would have to special-case.
    /// </summary>
    private const int RpcCallFailed = unchecked((int)0x800706BE);

    /// <summary>
    /// Shows the dialog for the given file. Windows always asks about a concrete file, so when
    /// <paramref name="sampleFilePath"/> does not exist a temporary file with the same extension is created
    /// under <c>%TEMP%</c> and removed afterwards.
    /// </summary>
    /// <returns><see langword="true"/> when the user completed the dialog, <see langword="false"/> when cancelled.</returns>
    public static bool ShowForExtension(IntPtr owner, string sampleFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sampleFilePath);

        var target = sampleFilePath;
        string? temporary = null;

        if (!File.Exists(target))
        {
            var extension = Path.GetExtension(sampleFilePath);
            if (string.IsNullOrEmpty(extension))
            {
                throw new ArgumentException(
                    "Örnek dosyanın bir uzantısı olmalı.", nameof(sampleFilePath));
            }

            temporary = Path.Combine(
                Path.GetTempPath(),
                "Runly-ornek-" + Guid.NewGuid().ToString("N") + extension);

            File.WriteAllText(temporary, string.Empty);
            target = temporary;
        }

        try
        {
            var info = new OpenAsInfo
            {
                FileName = target,
                ClassName = null,
                Flags = OaifExec,
            };

            var hr = SHOpenWithDialog(owner, ref info);

            if (hr == 0)
            {
                return true;
            }

            if (hr is ErrorCancelled or RpcCallFailed)
            {
                return false;
            }

            // Anything else is a genuine failure; SPEC 11 says Core throws and the caller reports it.
            Marshal.ThrowExceptionForHR(hr);
            return false;
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary))
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (IOException)
                {
                    // The chosen application may still hold the sample file open; leaving it in %TEMP% is harmless.
                }
                catch (UnauthorizedAccessException)
                {
                    // Same as above.
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenAsInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FileName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ClassName;

        public int Flags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHOpenWithDialog(IntPtr parent, ref OpenAsInfo info);
}
