using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Runly.Core.Shell;

/// <summary>
/// Shows the Windows "Open with" dialog through <c>SHOpenWithDialog</c>. For an extension that already carries a
/// <c>UserChoice</c> key — <c>.ps1</c> on this machine — this is the only legitimate way to bind it to Runly:
/// the key is hash protected and SPEC 2 forbids forging it. The user picks Runly and ticks
/// "Her zaman bu uygulamayı kullan".
/// </summary>
[SupportedOSPlatform("windows")]
public static class OpenWithDialog
{
    /// <summary>Let the user register a new application from the dialog.</summary>
    private const int OaifAllowRegistration = 0x00000001;

    /// <summary>Persist the choice for the file type, which is what writes <c>UserChoice</c>.</summary>
    private const int OaifRegisterExt = 0x00000002;

    /// <summary>
    /// Run the chosen application on the sample file once the user confirms.
    /// </summary>
    /// <remarks>
    /// Measured on Windows 11 during R1: this dialog can no longer set the default handler at all. With these
    /// flags it offers only "Yalnızca bir kez"; adding <c>OAIF_FORCE_REGISTRATION</c> (0x8) makes Windows refuse
    /// outright with "Varsayılan uygulamalarınızı değiştirmek için Ayarlar'a gidin". Binding therefore has to go
    /// through Explorer's "Birlikte aç → Başka bir uygulama seç → Her zaman" or the Settings page; the settings
    /// GUI offers both. The dialog is kept because it is still the fastest way to run a file with Runly once.
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
                Flags = OaifAllowRegistration | OaifRegisterExt | OaifExec,
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
