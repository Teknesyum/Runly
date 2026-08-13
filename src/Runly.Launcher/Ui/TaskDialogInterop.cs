using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Runly.Core.Abstractions;
using Runly.Core.Models;

namespace Runly.Launcher.Ui;

/// <summary>
/// <see cref="IDialogService"/> on top of <c>comctl32</c>'s <c>TaskDialogIndirect</c> (SPEC 6).
/// Native, AOT-safe and free of WinForms; the comctl32 v6 dependency lives in <c>app.manifest</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class TaskDialogInterop : IDialogService
{
    /// <summary>How long the run button stays disabled on a mark-of-the-web dialog (T3.md).</summary>
    private const int MotwDelayMilliseconds = 3000;

    private const int RunButtonId = 1001;
    private const int SettingsButtonId = 1002;
    private const int RadioOnlyOnceId = 2001;
    private const int RadioTrustFileId = 2002;
    private const int RadioTrustFolderId = 2003;

    private const int DialogWidthDialogUnits = 380;
    private const int MaxExpandedLineLength = 200;

    // The launcher shows one dialog at a time on one thread, so the countdown state the unmanaged
    // callback needs can live in statics; there is nowhere else to put it without pinning a delegate.
    private static int s_delayedButtonId;
    private static bool s_delayPending;

    private readonly ILogger _logger;

    /// <summary>Creates the dialog service; failures to show a dialog are reported through the logger.</summary>
    internal TaskDialogInterop(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public SecurityDecision? AskSecurity(ScriptInfo script, string commandLine, SecurityVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(script);

        var isMotw = verdict == SecurityVerdict.MotwBlocked;
        var offersTrust = verdict is SecurityVerdict.NeedsPrompt or SecurityVerdict.Changed;

        var mainInstruction = verdict switch
        {
            SecurityVerdict.MotwBlocked => $"\"{script.FileName}\" internetten indirilmiş",
            SecurityVerdict.Changed => $"\"{script.FileName}\" son onaydan sonra değişti",
            _ => $"\"{script.FileName}\" çalıştırılsın mı?",
        };

        var icon = verdict switch
        {
            SecurityVerdict.MotwBlocked => NativeMethods.TdErrorIcon,
            SecurityVerdict.Changed => NativeMethods.TdWarningIcon,
            _ => NativeMethods.TdShieldIcon,
        };

        var runButtonText = isMotw ? "Yine de çalıştır" : "Çalıştır";

        var allocations = new List<nint>();
        try
        {
            var config = new NativeMethods.TaskDialogConfig
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.TaskDialogConfig>(),
                Parent = NativeMethods.GetConsoleWindow(),
                Flags = NativeMethods.TdfAllowDialogCancellation | (isMotw ? NativeMethods.TdfCallbackTimer : 0),
                CommonButtons = NativeMethods.TdcbfCancelButton,
                WindowTitle = Alloc(allocations, "Runly — Script çalıştırılacak"),
                MainIcon = icon,
                MainInstruction = Alloc(allocations, mainInstruction),
                Content = Alloc(allocations, BuildContent(script, commandLine, verdict)),
                // The run button is deliberately not the default: Cancel is, and Esc cancels (SPEC 6).
                DefaultButton = NativeMethods.IdCancel,
                Width = DialogWidthDialogUnits,
            };

            SetCustomButtons(ref config, allocations, [(RunButtonId, runButtonText)]);

            if (offersTrust)
            {
                SetRadioButtons(ref config, allocations,
                [
                    (RadioOnlyOnceId, "Sadece bu sefer"),
                    (RadioTrustFileId, "Bu dosyaya her zaman güven"),
                    (RadioTrustFolderId, "Bu klasördeki her şeye güven"),
                ]);
                config.DefaultRadioButton = RadioOnlyOnceId;
            }

            if (isMotw)
            {
                config.VerificationText = Alloc(allocations, "İnternet işaretini kaldır");
            }

            var code = BuildCodePreview(script);
            if (code.Length != 0)
            {
                config.ExpandedInformation = Alloc(allocations, code);
                config.ExpandedControlText = Alloc(allocations, "Kodu gizle");
                config.CollapsedControlText = Alloc(allocations, "Önce kodu göster");
            }

            s_delayPending = isMotw;
            s_delayedButtonId = isMotw ? RunButtonId : 0;
            config.Callback = isMotw
                ? (nint)(delegate* unmanaged<nint, uint, nint, nint, nint, int>)&SecurityDialogCallback
                : 0;

            var hresult = NativeMethods.TaskDialogIndirect(in config, out var pressedButton, out var radio, out var verificationChecked);
            if (hresult != 0)
            {
                _logger.Error($"Güvenlik diyaloğu gösterilemedi (HRESULT 0x{hresult:X8}).");
                return null;
            }

            if (pressedButton != RunButtonId)
            {
                return SecurityDecision.Cancelled;
            }

            return new SecurityDecision
            {
                Allow = true,
                Reason = SecurityDecisionReason.UserApproved,
                RememberFile = offersTrust && radio == RadioTrustFileId,
                RememberFolder = offersTrust && radio == RadioTrustFolderId,
                StripMotw = isMotw && verificationChecked != 0,
            };
        }
        finally
        {
            s_delayPending = false;
            s_delayedButtonId = 0;
            Free(allocations);
        }
    }

    /// <inheritdoc />
    public string? AskArgs(ScriptInfo script)
    {
        ArgumentNullException.ThrowIfNull(script);
        return ArgumentPromptDialog.Show(script.FileName, _logger);
    }

    /// <inheritdoc />
    public void ShowError(string title, string body)
    {
        if (!ShowSimple(title, body, NativeMethods.TdErrorIcon, NativeMethods.TdcbfOkButton, null, out _))
        {
            // The dialog itself failed; the console is the only channel left.
            Console.Error.WriteLine($"{title}: {body}");
        }
    }

    /// <summary>Shows a question with an extra "Ayarları aç" button; returns whether that button was pressed.</summary>
    internal bool AskOpenSettings(string title, string body)
    {
        if (!ShowSimple(title, body, NativeMethods.TdWarningIcon, NativeMethods.TdcbfCancelButton,
                [(SettingsButtonId, "Ayarları aç")], out var pressedButton))
        {
            Console.Error.WriteLine($"{title}: {body}");
            return false;
        }

        return pressedButton == SettingsButtonId;
    }

    private bool ShowSimple(
        string title,
        string body,
        nint icon,
        int commonButtons,
        (int Id, string Text)[]? customButtons,
        out int pressedButton)
    {
        pressedButton = NativeMethods.IdCancel;
        var allocations = new List<nint>();
        try
        {
            var config = new NativeMethods.TaskDialogConfig
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.TaskDialogConfig>(),
                Parent = NativeMethods.GetConsoleWindow(),
                Flags = NativeMethods.TdfAllowDialogCancellation,
                CommonButtons = commonButtons,
                WindowTitle = Alloc(allocations, "Runly"),
                MainIcon = icon,
                MainInstruction = Alloc(allocations, title),
                Content = Alloc(allocations, body),
                Width = DialogWidthDialogUnits,
            };

            if (customButtons is { Length: > 0 })
            {
                SetCustomButtons(ref config, allocations, customButtons);
            }

            var hresult = NativeMethods.TaskDialogIndirect(in config, out pressedButton, out _, out _);
            if (hresult == 0)
            {
                return true;
            }

            _logger.Error($"Diyalog gösterilemedi (HRESULT 0x{hresult:X8}): {title}");
            return false;
        }
        finally
        {
            Free(allocations);
        }
    }

    // TDN_TIMER fires roughly every 200 ms with the elapsed milliseconds in lParam; the run button
    // starts disabled so a reflex click cannot approve a downloaded script (T3.md).
    [UnmanagedCallersOnly]
    private static int SecurityDialogCallback(nint hwnd, uint notification, nint wParam, nint lParam, nint refData)
    {
        switch (notification)
        {
            case NativeMethods.TdnCreated when s_delayPending:
                NativeMethods.SendMessageW(hwnd, NativeMethods.TdmEnableButton, s_delayedButtonId, 0);
                break;

            // TDN_TIMER carries the elapsed milliseconds in wParam; lParam is the (unused) reference data.
            case NativeMethods.TdnTimer when s_delayPending && (long)wParam >= MotwDelayMilliseconds:
                NativeMethods.SendMessageW(hwnd, NativeMethods.TdmEnableButton, s_delayedButtonId, 1);
                s_delayPending = false;
                break;
        }

        return 0;
    }

    private static string BuildContent(ScriptInfo script, string commandLine, SecurityVerdict verdict)
    {
        var builder = new StringBuilder();

        if (verdict == SecurityVerdict.MotwBlocked)
        {
            builder.Append("Bu dosya başka bir bilgisayardan geldi ve güvenli olmayabilir")
                   .Append(script.ZoneId is { } zone ? $" (bölge {zone})." : ".")
                   .Append('\n')
                   .Append('\n');
        }
        else if (verdict == SecurityVerdict.Changed)
        {
            builder.Append("İçeriği en son onayladığınız hâlinden farklı.\n\n");
        }

        builder.Append("Tam yol:\n").Append(script.Path).Append('\n').Append('\n');
        builder.Append("Uzantı: ").Append(script.Extension.Length == 0 ? "(yok)" : script.Extension).Append('\n');
        builder.Append("Boyut: ").Append(script.SizeBytes.ToString("N0", CultureInfo.InvariantCulture)).Append(" bayt\n");
        builder.Append("Değiştirilme: ")
               .Append(script.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
               .Append('\n').Append('\n');
        builder.Append("Çalışacak komut:\n").Append(commandLine);

        return builder.ToString();
    }

    private static string BuildCodePreview(ScriptInfo script)
    {
        if (script.FirstLines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var line in script.FirstLines)
        {
            var trimmed = line.Length > MaxExpandedLineLength ? line[..MaxExpandedLineLength] + "…" : line;
            builder.Append(trimmed).Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static void SetCustomButtons(ref NativeMethods.TaskDialogConfig config, List<nint> allocations, (int Id, string Text)[] buttons)
    {
        config.Buttons = AllocButtons(allocations, buttons);
        config.ButtonCount = (uint)buttons.Length;
    }

    private static void SetRadioButtons(ref NativeMethods.TaskDialogConfig config, List<nint> allocations, (int Id, string Text)[] buttons)
    {
        config.RadioButtons = AllocButtons(allocations, buttons);
        config.RadioButtonCount = (uint)buttons.Length;
    }

    private static nint AllocButtons(List<nint> allocations, (int Id, string Text)[] buttons)
    {
        var block = Marshal.AllocHGlobal(sizeof(NativeMethods.TaskDialogButton) * buttons.Length);
        allocations.Add(block);

        var typed = (NativeMethods.TaskDialogButton*)block;
        for (var i = 0; i < buttons.Length; i++)
        {
            typed[i] = new NativeMethods.TaskDialogButton
            {
                ButtonId = buttons[i].Id,
                ButtonText = Alloc(allocations, buttons[i].Text),
            };
        }

        return block;
    }

    private static nint Alloc(List<nint> allocations, string text)
    {
        var pointer = Marshal.StringToHGlobalUni(text);
        allocations.Add(pointer);
        return pointer;
    }

    private static void Free(List<nint> allocations)
    {
        foreach (var pointer in allocations)
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
