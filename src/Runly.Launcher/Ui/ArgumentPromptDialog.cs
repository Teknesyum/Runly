using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Runly.Core.Abstractions;

namespace Runly.Launcher.Ui;

/// <summary>
/// A small native input window for the <c>prompt-args</c> verb (SPEC 7). TaskDialog has no text field,
/// so this is a plain Win32 window; going through <c>RunlySettings.exe</c> instead would make the
/// launcher depend on a package that does not exist yet (T3.md).
/// Teknesyum neon styling (R5) comes from <see cref="NeonWindowChrome"/> — WinForms/WPF are not usable
/// in an AOT-published, trimmed executable.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe class ArgumentPromptDialog
{
    private const string ClassName = "RunlyArgumentPrompt";

    private const int WindowWidth = 440;
    private const int WindowHeight = 186;
    private const int CaptionButtons = 3;

    private static bool s_classRegistered;
    private static nint s_editHandle;
    private static nint s_okHandle;
    private static nint s_cancelHandle;
    private static nint s_backgroundBrush;
    private static nint s_editBrush;
    private static nint s_sansFont;
    private static string s_acceptedText = string.Empty;
    private static bool s_accepted;
    private static bool s_closing;

    /// <summary>Asks for a raw argument string; returns <see langword="null"/> when the user cancels.</summary>
    internal static string? Show(string scriptFileName, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var instance = NativeMethods.GetModuleHandleW(0);
        if (!EnsureClassRegistered(instance, logger))
        {
            return null;
        }

        s_editHandle = 0;
        s_okHandle = 0;
        s_cancelHandle = 0;
        s_acceptedText = string.Empty;
        s_accepted = false;
        s_closing = false;

        s_backgroundBrush = NativeMethods.CreateSolidBrush(NeonWindowChrome.ColorSurface);
        s_editBrush = NativeMethods.CreateSolidBrush(NeonWindowChrome.ColorEditBg);

        var window = NativeMethods.CreateWindowExW(
            NativeMethods.WsExDlgModalFrame | NativeMethods.WsExControlParent,
            ClassName,
            "Runly — Argümanlarla çalıştır",
            NativeMethods.WsPopup | NativeMethods.WsThickFrame | NativeMethods.WsSysMenu |
            NativeMethods.WsMinimizeBox | NativeMethods.WsMaximizeBox,
            0, 0, 100, 100,
            0, 0, instance, 0);

        if (window == 0)
        {
            logger.Error($"Argüman penceresi oluşturulamadı (Win32 hata kodu {Marshal.GetLastPInvokeError()}).");
            CleanupGdiObjects();
            return null;
        }

        try
        {
            var dpi = NeonWindowChrome.ReadDpi(window);

            s_sansFont = NeonWindowChrome.ResolveSansFont((int)(9 * dpi / 72.0));
            BuildControls(window, instance, s_sansFont, dpi, scriptFileName);
            NeonWindowChrome.RoundWindowCorners(window, WindowWidth, WindowHeight, dpi);

            // Must be set after the window has a DWM-composited frame (i.e. after it is shown at least
            // once); calling it immediately after CreateWindowExW is silently ignored on this machine.
            NeonWindowChrome.ApplyDarkFrame(window);

            NeonWindowChrome.CenterAndShow(window, WindowWidth, WindowHeight, dpi);
            NativeMethods.SetFocus(s_editHandle);

            // Re-applied post-show: some builds only honour the attribute once the window has an actual
            // DWM-composited frame on screen, and silently ignore it beforehand.
            NeonWindowChrome.ApplyDarkFrame(window);

            RunMessageLoop(window);

            return s_accepted ? s_acceptedText : null;
        }
        finally
        {
            CleanupGdiObjects();
            s_editHandle = 0;
        }
    }

    private static void CleanupGdiObjects()
    {
        if (s_sansFont != 0)
        {
            NativeMethods.DeleteObject(s_sansFont);
            s_sansFont = 0;
        }

        if (s_backgroundBrush != 0)
        {
            NativeMethods.DeleteObject(s_backgroundBrush);
            s_backgroundBrush = 0;
        }

        if (s_editBrush != 0)
        {
            NativeMethods.DeleteObject(s_editBrush);
            s_editBrush = 0;
        }
    }

    private static bool EnsureClassRegistered(nint instance, ILogger logger)
    {
        if (s_classRegistered)
        {
            return true;
        }

        var classNamePointer = Marshal.StringToHGlobalUni(ClassName);
        try
        {
            var windowClass = new NativeMethods.WndClassEx
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
                WndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WindowProc,
                Instance = instance,
                Cursor = NeonWindowChrome.LoadArrowCursor(),
                Background = NativeMethods.CreateSolidBrush(NeonWindowChrome.ColorSurface),
                ClassName = classNamePointer,
            };

            if (NativeMethods.RegisterClassExW(in windowClass) == 0)
            {
                logger.Error($"Argüman penceresi sınıfı kaydedilemedi (Win32 hata kodu {Marshal.GetLastPInvokeError()}).");
                return false;
            }

            s_classRegistered = true;
            return true;
        }
        finally
        {
            // The class keeps a pointer to this string for the process lifetime only when registration
            // succeeded; it is released either way because the class is never unregistered before exit.
            if (!s_classRegistered)
            {
                Marshal.FreeHGlobal(classNamePointer);
            }
        }
    }

    private static void BuildControls(nint window, nint instance, nint font, uint dpi, string scriptFileName)
    {
        int Scale(int value) => NeonWindowChrome.Scale(value, dpi);

        var label = NativeMethods.CreateWindowExW(
            0, "STATIC", $"\"{scriptFileName}\" dosyasına verilecek argümanlar:",
            NativeMethods.WsChild | NativeMethods.WsVisible,
            Scale(12), Scale(48), Scale(400), Scale(20),
            window, 0, instance, 0);

        s_editHandle = NativeMethods.CreateWindowExW(
            0, "EDIT", string.Empty,
            NativeMethods.WsChild | NativeMethods.WsVisible | NativeMethods.WsBorder |
            NativeMethods.WsTabStop | NativeMethods.EsAutoHScroll,
            Scale(12), Scale(72), Scale(400), Scale(24),
            window, 0, instance, 0);

        s_okHandle = NativeMethods.CreateWindowExW(
            0, "BUTTON", "Çalıştır",
            NativeMethods.WsChild | NativeMethods.WsVisible | NativeMethods.WsTabStop |
            NativeMethods.BsDefPushButton | NativeMethods.BsOwnerDraw,
            Scale(232), Scale(108), Scale(88), Scale(28),
            window, NativeMethods.IdOk, instance, 0);

        s_cancelHandle = NativeMethods.CreateWindowExW(
            0, "BUTTON", "İptal",
            NativeMethods.WsChild | NativeMethods.WsVisible | NativeMethods.WsTabStop | NativeMethods.BsOwnerDraw,
            Scale(328), Scale(108), Scale(84), Scale(28),
            window, NativeMethods.IdCancel, instance, 0);

        foreach (var control in new[] { label, s_editHandle })
        {
            if (control != 0)
            {
                NativeMethods.SendMessageW(control, NativeMethods.WmSetFont, font, 1);
            }
        }
    }

    private static void RunMessageLoop(nint window)
    {
        while (NativeMethods.GetMessageW(out var message, 0, 0, 0) > 0)
        {
            if (NativeMethods.IsDialogMessageW(window, ref message))
            {
                continue;
            }

            NativeMethods.TranslateMessage(in message);
            NativeMethods.DispatchMessageW(in message);
        }
    }

    private static string ReadEditText(nint edit)
    {
        if (edit == 0)
        {
            return string.Empty;
        }

        var length = NativeMethods.GetWindowTextLengthW(edit);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = Marshal.AllocHGlobal((length + 1) * sizeof(char));
        try
        {
            var copied = NativeMethods.GetWindowTextW(edit, buffer, length + 1);
            return copied <= 0 ? string.Empty : Marshal.PtrToStringUni(buffer, copied);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [UnmanagedCallersOnly]
    private static nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case NativeMethods.WmNcHitTest:
                return NeonWindowChrome.HitTest(hwnd, lParam, CaptionButtons);

            case NativeMethods.WmPaint:
                NeonWindowChrome.DrawCaption(hwnd, s_backgroundBrush, "Runly", CaptionButtons, s_sansFont);
                return 0;

            case NativeMethods.WmActivate:
                NativeMethods.InvalidateRect(hwnd, 0, false);
                break;

            case NativeMethods.WmCtlColorStatic:
                NativeMethods.SetBkMode(wParam, NativeMethods.TransparentBkMode);
                NativeMethods.SetTextColor(wParam, NeonWindowChrome.ColorText);
                return s_backgroundBrush;

            case NativeMethods.WmCtlColorEdit:
                NativeMethods.SetBkMode(wParam, NativeMethods.OpaqueBkMode);
                NativeMethods.SetBkColor(wParam, NeonWindowChrome.ColorEditBg);
                NativeMethods.SetTextColor(wParam, NeonWindowChrome.ColorNeonBlue);
                return s_editBrush;

            case NativeMethods.WmDrawItem:
                var item = *(NativeMethods.DrawItemStruct*)lParam;
                if (item.HwndItem == s_okHandle || item.HwndItem == s_cancelHandle)
                {
                    var focused = (item.ItemState & NativeMethods.OdsFocus) != 0;
                    var text = item.HwndItem == s_okHandle ? "Çalıştır" : "İptal";
                    NeonWindowChrome.DrawNeonButton(item.Hdc, item.RcItem, text, primary: item.HwndItem == s_okHandle, focused);
                    return 1;
                }

                break;

            case NativeMethods.WmCommand:
                var controlId = (int)(wParam & 0xFFFF);
                if (controlId is NativeMethods.IdOk or NativeMethods.IdCancel)
                {
                    // The text is read before the window dies, since the edit control dies with it.
                    s_accepted = controlId == NativeMethods.IdOk;
                    if (s_accepted)
                    {
                        s_acceptedText = ReadEditText(s_editHandle);
                    }

                    s_closing = true;
                    NativeMethods.DestroyWindow(hwnd);
                    return 0;
                }

                break;

            case NativeMethods.WmClose:
                s_accepted = false;
                s_closing = true;
                NativeMethods.DestroyWindow(hwnd);
                return 0;

            case NativeMethods.WmDestroy:
                if (!s_closing)
                {
                    s_accepted = false;
                }

                NativeMethods.PostQuitMessage(0);
                return 0;
        }

        return NativeMethods.DefWindowProcW(hwnd, message, wParam, lParam);
    }
}
