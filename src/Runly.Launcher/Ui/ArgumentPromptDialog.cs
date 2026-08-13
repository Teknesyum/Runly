using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Runly.Core.Abstractions;

namespace Runly.Launcher.Ui;

/// <summary>
/// A small native input window for the <c>prompt-args</c> verb (SPEC 7). TaskDialog has no text field,
/// so this is a plain Win32 window; going through <c>RunlySettings.exe</c> instead would make the
/// launcher depend on a package that does not exist yet (T3.md).
/// Teknesyum neon styling (R5): everything below is drawn with raw GDI — WinForms/WPF are not usable
/// in an AOT-published, trimmed executable.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe class ArgumentPromptDialog
{
    private const string ClassName = "RunlyArgumentPrompt";

    // ---- Teknesyum neon palette, as Win32 COLORREF (0x00BBGGRR) ---------------------------------
    private const uint ColorSurface = 0x0A0908; // #08090A panel/window background
    private const uint ColorEditBg = 0x141210; // slightly lifted surface for the input field
    private const uint ColorNeonBlue = 0xFFF300; // #00F3FF primary
    private const uint ColorNeonPurple = 0xFF26B0; // #B026FF secondary button border/text
    private const uint ColorTextDim = 0xAFA39C; // #9CA3AF label text

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int CornerRadius = 12; // token: buton/kart radius
    private const int WindowCornerRadius = 16; // token: kutu radius
    private const int CaptionHeight = 36;
    private const int ResizeBorder = 7;
    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int HtMinButton = 8;
    private const int HtMaxButton = 9;
    private const int HtClose = 20;

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

        s_backgroundBrush = NativeMethods.CreateSolidBrush(ColorSurface);
        s_editBrush = NativeMethods.CreateSolidBrush(ColorEditBg);

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
            var dpi = NativeMethods.GetDpiForWindow(window);
            if (dpi == 0)
            {
                dpi = 96;
            }

            s_sansFont = ResolveSansFont((int)(9 * dpi / 72.0));
            BuildControls(window, instance, s_sansFont, dpi, scriptFileName);
            RoundWindowCorners(window, dpi);

            // Must be set after the window has a DWM-composited frame (i.e. after it is shown at least
            // once); calling it immediately after CreateWindowExW is silently ignored on this machine.
            var useDark = 1;
            NativeMethods.DwmSetWindowAttribute(window, DwmwaUseImmersiveDarkMode, in useDark, sizeof(int));

            CenterAndShow(window, dpi);

            // Re-applied post-show: some builds only honour the attribute once the window has an actual
            // DWM-composited frame on screen, and silently ignore it beforehand.
            NativeMethods.DwmSetWindowAttribute(window, DwmwaUseImmersiveDarkMode, in useDark, sizeof(int));

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
            const int idcArrow = 32512;

            var windowClass = new NativeMethods.WndClassEx
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
                WndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WindowProc,
                Instance = instance,
                Cursor = NativeMethods.LoadCursorW(0, idcArrow),
                Background = NativeMethods.CreateSolidBrush(ColorSurface),
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

    /// <summary>
    /// Tries "Inter" first (Teknesyum's sans token), falls back to "Segoe UI" — Inter is not installed on
    /// this machine and R5 forbids installing fonts, so the fallback chain has to be verified at runtime.
    /// GDI silently substitutes an unavailable face, so the only reliable check is to select the font into
    /// a DC and read back what it actually picked.
    /// </summary>
    private static nint ResolveSansFont(int height)
    {
        return TryCreateFont("Inter", height) is { } inter and not 0
            ? inter
            : TryCreateFont("Segoe UI", height) is { } segoe and not 0
                ? segoe
                : NativeMethods.CreateFontW(-height, 0, 0, 0, NativeMethods.FwNormal, 0, 0, 0,
                    NativeMethods.DefaultCharset, 0, 0, NativeMethods.ClearTypeQuality, 0, "Segoe UI");
    }

    private static nint TryCreateFont(string family, int height)
    {
        var font = NativeMethods.CreateFontW(-height, 0, 0, 0, NativeMethods.FwNormal, 0, 0, 0,
            NativeMethods.DefaultCharset, 0, 0, NativeMethods.ClearTypeQuality, 0, family);
        if (font == 0)
        {
            return 0;
        }

        var hdc = NativeMethods.GetDC(0);
        var previous = NativeMethods.SelectObject(hdc, font);
        var buffer = Marshal.AllocHGlobal(64 * sizeof(char));
        try
        {
            NativeMethods.GetTextFaceW(hdc, 64, buffer);
            var actual = Marshal.PtrToStringUni(buffer) ?? string.Empty;
            NativeMethods.SelectObject(hdc, previous);
            NativeMethods.ReleaseDC(0, hdc);

            if (!string.Equals(actual, family, StringComparison.OrdinalIgnoreCase))
            {
                NativeMethods.DeleteObject(font);
                return 0;
            }

            return font;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void RoundWindowCorners(nint window, uint dpi)
    {
        int Scale(int value) => (int)(value * dpi / 96.0);

        var width = Scale(440);
        var height = Scale(186);
        var region = NativeMethods.CreateRoundRectRgn(0, 0, width, height, Scale(WindowCornerRadius), Scale(WindowCornerRadius));
        NativeMethods.SetWindowRgn(window, region, redraw: true);
    }

    private static void BuildControls(nint window, nint instance, nint font, uint dpi, string scriptFileName)
    {
        int Scale(int value) => (int)(value * dpi / 96.0);

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

    private static void CenterAndShow(nint window, uint dpi)
    {
        const int swShow = 5;

        var width = (int)(440 * dpi / 96.0);
        var height = (int)(186 * dpi / 96.0);

        var x = 100;
        var y = 100;
        if (NativeMethods.SystemParametersInfoW(NativeMethods.SpiGetWorkArea, 0, out var workArea, 0))
        {
            x = workArea.Left + ((workArea.Right - workArea.Left - width) / 2);
            y = workArea.Top + ((workArea.Bottom - workArea.Top - height) / 2);
        }

        NativeMethods.MoveWindow(window, x, y, width, height, repaint: false);
        NativeMethods.ShowWindow(window, swShow);
        NativeMethods.SetForegroundWindow(window);
        NativeMethods.SetFocus(s_editHandle);
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

    private static void DrawNeonButton(nint hdc, in NativeMethods.Rect rect, string text, bool primary, bool focused)
    {
        var accent = primary ? ColorNeonBlue : ColorNeonPurple;

        // Glow approximation: a dimmer, wider ring drawn first so the accent-coloured edge appears to bleed.
        var glowPen = NativeMethods.CreatePen(0, focused ? 3 : 2, accent);
        var previousPen = NativeMethods.SelectObject(hdc, glowPen);
        var previousBrush = NativeMethods.SelectObject(hdc, primary
            ? NativeMethods.CreateSolidBrush(accent)
            : NativeMethods.CreateSolidBrush(ColorSurface));

        NativeMethods.RoundRect(hdc, rect.Left, rect.Top, rect.Right, rect.Bottom, CornerRadius, CornerRadius);

        var fillBrush = NativeMethods.SelectObject(hdc, previousBrush);
        NativeMethods.DeleteObject(fillBrush);
        var pen = NativeMethods.SelectObject(hdc, previousPen);
        NativeMethods.DeleteObject(pen);

        NativeMethods.SetBkMode(hdc, NativeMethods.TransparentBkMode);
        NativeMethods.SetTextColor(hdc, primary ? ColorSurface : accent);

        var textRect = rect;
        NativeMethods.DrawTextW(hdc, text, text.Length, ref textRect,
            NativeMethods.DtCenter | NativeMethods.DtVCenter | NativeMethods.DtSingleLine);
    }

    [UnmanagedCallersOnly]
    private static nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case NativeMethods.WmNcHitTest:
                NativeMethods.GetClientRect(hwnd, out var client);
                var x = unchecked((short)(long)lParam);
                var y = unchecked((short)((long)lParam >> 16));
                var windowRect = client;
                NativeMethods.GetWindowRect(hwnd, out windowRect);
                x -= (short)windowRect.Left;
                y -= (short)windowRect.Top;
                var left = x < ResizeBorder;
                var right = x >= client.Right - ResizeBorder;
                var top = y < ResizeBorder;
                var bottom = y >= client.Bottom - ResizeBorder;
                if (left && top) return HtTopLeft;
                if (right && top) return HtTopRight;
                if (left && bottom) return HtBottomLeft;
                if (right && bottom) return HtBottomRight;
                if (left) return HtLeft;
                if (right) return HtRight;
                if (top) return HtTop;
                if (bottom) return HtBottom;
                if (y < CaptionHeight)
                {
                    if (x >= client.Right - 44) return HtClose;
                    if (x >= client.Right - 88) return HtMaxButton;
                    if (x >= client.Right - 132) return HtMinButton;
                    return HtCaption;
                }
                return HtClient;

            case NativeMethods.WmPaint:
                DrawCaption(hwnd);
                return 0;

            case NativeMethods.WmActivate:
                NativeMethods.InvalidateRect(hwnd, 0, false);
                break;

            case NativeMethods.WmCtlColorStatic:
                NativeMethods.SetBkMode(wParam, NativeMethods.TransparentBkMode);
                NativeMethods.SetTextColor(wParam, ColorTextDim);
                return s_backgroundBrush;

            case NativeMethods.WmCtlColorEdit:
                NativeMethods.SetBkMode(wParam, NativeMethods.OpaqueBkMode);
                NativeMethods.SetBkColor(wParam, ColorEditBg);
                NativeMethods.SetTextColor(wParam, ColorNeonBlue);
                return s_editBrush;

            case NativeMethods.WmDrawItem:
                var item = *(NativeMethods.DrawItemStruct*)lParam;
                if (item.HwndItem == s_okHandle || item.HwndItem == s_cancelHandle)
                {
                    var focused = (item.ItemState & NativeMethods.OdsFocus) != 0;
                    var text = item.HwndItem == s_okHandle ? "Çalıştır" : "İptal";
                    DrawNeonButton(item.Hdc, item.RcItem, text, primary: item.HwndItem == s_okHandle, focused);
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

    private static void DrawCaption(nint hwnd)
    {
        var hdc = NativeMethods.BeginPaint(hwnd, out var paint);
        NativeMethods.GetClientRect(hwnd, out var client);
        var caption = new NativeMethods.Rect { Left = 0, Top = 0, Right = client.Right, Bottom = CaptionHeight };
        NativeMethods.FillRect(hdc, in caption, s_backgroundBrush);
        NativeMethods.SetBkMode(hdc, NativeMethods.TransparentBkMode);
        NativeMethods.SetTextColor(hdc, ColorNeonBlue);
        var title = new NativeMethods.Rect { Left = 36, Top = 0, Right = client.Right - 132, Bottom = CaptionHeight };
        NativeMethods.DrawTextW(hdc, "Runly", 5, ref title, NativeMethods.DtVCenter | NativeMethods.DtSingleLine);
        DrawCaptionGlyph(hdc, client.Right - 132, "−", ColorNeonBlue);
        DrawCaptionGlyph(hdc, client.Right - 88, "□", ColorNeonBlue);
        DrawCaptionGlyph(hdc, client.Right - 44, "×", 0xEA00FF);
        NativeMethods.EndPaint(hwnd, in paint);
    }

    private static void DrawCaptionGlyph(nint hdc, int left, string glyph, uint color)
    {
        NativeMethods.SetTextColor(hdc, color);
        var rect = new NativeMethods.Rect { Left = left, Top = 0, Right = left + 44, Bottom = CaptionHeight };
        NativeMethods.DrawTextW(hdc, glyph, glyph.Length, ref rect,
            NativeMethods.DtCenter | NativeMethods.DtVCenter | NativeMethods.DtSingleLine);
    }
}
