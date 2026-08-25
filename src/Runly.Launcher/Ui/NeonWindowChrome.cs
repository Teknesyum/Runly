using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Runly.Launcher.Ui;

/// <summary>
/// The Teknesyum neon chrome shared by the launcher's own Win32 windows (R5). WinForms and WPF are not
/// usable in an AOT-published, trimmed executable, so every colour, radius and glyph below is drawn with
/// raw GDI; keeping them in one place is what stops two launcher windows from disagreeing on a token.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NeonWindowChrome
{
    // ---- Teknesyum neon palette, as Win32 COLORREF (0x00BBGGRR) ---------------------------------
    internal const uint ColorSurface = 0x0A0908; // #08090A panel/window background
    internal const uint ColorEditBg = 0x141210; // slightly lifted surface for the input field
    internal const uint ColorNeonBlue = 0xFFF300; // #00F3FF primary
    internal const uint ColorNeonPurple = 0xFF26B0; // #B026FF secondary button border/text
    internal const uint ColorNeonPink = 0xEA00FF; // #FF00EA close glyph
    internal const uint ColorTextDim = 0xAFA39C; // #9CA3AF label text

    internal const int DwmwaUseImmersiveDarkMode = 20;
    internal const int CornerRadius = 12; // token: buton/kart radius
    internal const int WindowCornerRadius = 16; // token: kutu radius
    internal const int CaptionHeight = 36;
    internal const int CaptionButtonWidth = 44;
    internal const int ResizeBorder = 7;

    internal const int HtClient = 1;
    internal const int HtCaption = 2;
    internal const int HtLeft = 10;
    internal const int HtRight = 11;
    internal const int HtTop = 12;
    internal const int HtTopLeft = 13;
    internal const int HtTopRight = 14;
    internal const int HtBottom = 15;
    internal const int HtBottomLeft = 16;
    internal const int HtBottomRight = 17;
    internal const int HtMinButton = 8;
    internal const int HtMaxButton = 9;
    internal const int HtClose = 20;

    private const int SwShow = 5;
    private const int IdcArrow = 32512;

    internal static int Scale(int value, uint dpi) => (int)(value * dpi / 96.0);

    internal static uint ReadDpi(nint window)
    {
        var dpi = NativeMethods.GetDpiForWindow(window);
        return dpi == 0 ? 96u : dpi;
    }

    internal static nint LoadArrowCursor() => NativeMethods.LoadCursorW(0, IdcArrow);

    /// <summary>
    /// Tries "Inter" first (Teknesyum's sans token), falls back to "Segoe UI" — Inter is not installed on
    /// this machine and R5 forbids installing fonts, so the fallback chain has to be verified at runtime.
    /// GDI silently substitutes an unavailable face, so the only reliable check is to select the font into
    /// a DC and read back what it actually picked.
    /// </summary>
    internal static nint ResolveSansFont(int height)
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

    internal static void RoundWindowCorners(nint window, int width, int height, uint dpi)
    {
        var radius = Scale(WindowCornerRadius, dpi);
        var region = NativeMethods.CreateRoundRectRgn(0, 0, Scale(width, dpi), Scale(height, dpi), radius, radius);
        NativeMethods.SetWindowRgn(window, region, redraw: true);
    }

    internal static void CenterAndShow(nint window, int width, int height, uint dpi)
    {
        var pixelWidth = Scale(width, dpi);
        var pixelHeight = Scale(height, dpi);

        var x = 100;
        var y = 100;
        if (NativeMethods.SystemParametersInfoW(NativeMethods.SpiGetWorkArea, 0, out var workArea, 0))
        {
            x = workArea.Left + ((workArea.Right - workArea.Left - pixelWidth) / 2);
            y = workArea.Top + ((workArea.Bottom - workArea.Top - pixelHeight) / 2);
        }

        NativeMethods.MoveWindow(window, x, y, pixelWidth, pixelHeight, repaint: false);
        NativeMethods.ShowWindow(window, SwShow);
        NativeMethods.SetForegroundWindow(window);
    }

    internal static void ApplyDarkFrame(nint window)
    {
        var useDark = 1;
        NativeMethods.DwmSetWindowAttribute(window, DwmwaUseImmersiveDarkMode, in useDark, sizeof(int));
    }

    /// <summary>Hit-tests a borderless neon window; <paramref name="captionButtons"/> is how many
    /// caption glyphs sit at the right edge (1 = close only, 3 = minimise/maximise/close).</summary>
    internal static nint HitTest(nint hwnd, nint lParam, int captionButtons)
    {
        NativeMethods.GetClientRect(hwnd, out var client);
        var x = unchecked((short)(long)lParam);
        var y = unchecked((short)((long)lParam >> 16));
        NativeMethods.GetWindowRect(hwnd, out var windowRect);
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
            if (x >= client.Right - CaptionButtonWidth) return HtClose;
            if (captionButtons >= 2 && x >= client.Right - (2 * CaptionButtonWidth)) return HtMaxButton;
            if (captionButtons >= 3 && x >= client.Right - (3 * CaptionButtonWidth)) return HtMinButton;
            return HtCaption;
        }

        return HtClient;
    }

    internal static void DrawNeonButton(nint hdc, in NativeMethods.Rect rect, string text, bool primary, bool focused)
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

    internal static void DrawCaption(nint hwnd, nint backgroundBrush, string title, int captionButtons, nint font)
    {
        var hdc = NativeMethods.BeginPaint(hwnd, out var paint);

        // The paint DC starts on the stock bitmap font, which has no em dash and renders it as .notdef.
        var previousFont = font == 0 ? 0 : NativeMethods.SelectObject(hdc, font);

        NativeMethods.GetClientRect(hwnd, out var client);
        var caption = new NativeMethods.Rect { Left = 0, Top = 0, Right = client.Right, Bottom = CaptionHeight };
        NativeMethods.FillRect(hdc, in caption, backgroundBrush);
        NativeMethods.SetBkMode(hdc, NativeMethods.TransparentBkMode);
        NativeMethods.SetTextColor(hdc, ColorNeonBlue);
        var titleRect = new NativeMethods.Rect
        {
            Left = CaptionButtonWidth - 8,
            Top = 0,
            Right = client.Right - (captionButtons * CaptionButtonWidth),
            Bottom = CaptionHeight,
        };
        NativeMethods.DrawTextW(hdc, title, title.Length, ref titleRect,
            NativeMethods.DtVCenter | NativeMethods.DtSingleLine);

        if (captionButtons >= 3)
        {
            DrawCaptionGlyph(hdc, client.Right - (3 * CaptionButtonWidth), "−", ColorNeonBlue);
        }

        if (captionButtons >= 2)
        {
            DrawCaptionGlyph(hdc, client.Right - (2 * CaptionButtonWidth), "□", ColorNeonBlue);
        }

        DrawCaptionGlyph(hdc, client.Right - CaptionButtonWidth, "×", ColorNeonPink);

        if (previousFont != 0)
        {
            NativeMethods.SelectObject(hdc, previousFont);
        }

        NativeMethods.EndPaint(hwnd, in paint);
    }

    private static void DrawCaptionGlyph(nint hdc, int left, string glyph, uint color)
    {
        NativeMethods.SetTextColor(hdc, color);
        var rect = new NativeMethods.Rect { Left = left, Top = 0, Right = left + CaptionButtonWidth, Bottom = CaptionHeight };
        NativeMethods.DrawTextW(hdc, glyph, glyph.Length, ref rect,
            NativeMethods.DtCenter | NativeMethods.DtVCenter | NativeMethods.DtSingleLine);
    }
}
