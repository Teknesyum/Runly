using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Runly.Core.Abstractions;

namespace Runly.Launcher.Ui;

/// <summary>
/// The "no handler for this extension" question, drawn as a neon Win32 window instead of a TaskDialog:
/// <c>TaskDialogIndirect</c> renders in the shell's own light chrome and cannot be themed (R5).
/// Returns <see langword="null"/> when no GUI surface is available, so callers can fall back to stderr.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe class MissingHandlerDialog
{
    private const string ClassName = "RunlyMissingHandler";

    private const int WindowWidth = 440;
    private const int WindowHeight = 186;
    private const int CaptionButtons = 1;

    private static bool s_classRegistered;
    private static nint s_settingsHandle;
    private static nint s_cancelHandle;
    private static nint s_backgroundBrush;
    private static nint s_sansFont;
    private static bool s_openSettings;
    private static bool s_closing;
    private static string s_caption = "Runly";

    /// <summary>Shows the question; <see langword="true"/> means the user chose "Ayarları aç".</summary>
    internal static bool? Show(string title, string message, string fileName, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var instance = NativeMethods.GetModuleHandleW(0);
        if (!EnsureClassRegistered(instance, logger))
        {
            return null;
        }

        s_settingsHandle = 0;
        s_cancelHandle = 0;
        s_openSettings = false;
        s_closing = false;
        s_caption = $"Runly — {title}";

        s_backgroundBrush = NativeMethods.CreateSolidBrush(NeonWindowChrome.ColorSurface);

        var window = NativeMethods.CreateWindowExW(
            NativeMethods.WsExDlgModalFrame | NativeMethods.WsExControlParent,
            ClassName,
            s_caption,
            NativeMethods.WsPopup | NativeMethods.WsSysMenu,
            0, 0, 100, 100,
            0, 0, instance, 0);

        if (window == 0)
        {
            logger.Error($"Yorumlayıcı penceresi oluşturulamadı (Win32 hata kodu {Marshal.GetLastPInvokeError()}).");
            CleanupGdiObjects();
            return null;
        }

        try
        {
            var dpi = NeonWindowChrome.ReadDpi(window);

            s_sansFont = NeonWindowChrome.ResolveSansFont((int)(9 * dpi / 72.0));
            BuildControls(window, instance, s_sansFont, dpi, message, fileName);
            NeonWindowChrome.RoundWindowCorners(window, WindowWidth, WindowHeight, dpi);

            // Must be set after the window has a DWM-composited frame; calling it immediately after
            // CreateWindowExW is silently ignored on this machine.
            NeonWindowChrome.ApplyDarkFrame(window);

            NeonWindowChrome.CenterAndShow(window, WindowWidth, WindowHeight, dpi);
            NativeMethods.SetFocus(s_settingsHandle);

            NeonWindowChrome.ApplyDarkFrame(window);

            RunMessageLoop(window);

            return s_openSettings;
        }
        finally
        {
            CleanupGdiObjects();
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
                logger.Error($"Yorumlayıcı penceresi sınıfı kaydedilemedi (Win32 hata kodu {Marshal.GetLastPInvokeError()}).");
                return false;
            }

            s_classRegistered = true;
            return true;
        }
        finally
        {
            if (!s_classRegistered)
            {
                Marshal.FreeHGlobal(classNamePointer);
            }
        }
    }

    private static void BuildControls(nint window, nint instance, nint font, uint dpi, string message, string fileName)
    {
        int Scale(int value) => NeonWindowChrome.Scale(value, dpi);

        var messageLabel = NativeMethods.CreateWindowExW(
            0, "STATIC", message,
            NativeMethods.WsChild | NativeMethods.WsVisible,
            Scale(12), Scale(48), Scale(400), Scale(20),
            window, 0, instance, 0);

        var fileLabel = NativeMethods.CreateWindowExW(
            0, "STATIC", $"Dosya: {fileName}",
            NativeMethods.WsChild | NativeMethods.WsVisible,
            Scale(12), Scale(72), Scale(400), Scale(20),
            window, 0, instance, 0);

        s_settingsHandle = NativeMethods.CreateWindowExW(
            0, "BUTTON", "Ayarları aç",
            NativeMethods.WsChild | NativeMethods.WsVisible | NativeMethods.WsTabStop |
            NativeMethods.BsDefPushButton | NativeMethods.BsOwnerDraw,
            Scale(220), Scale(108), Scale(96), Scale(28),
            window, NativeMethods.IdOk, instance, 0);

        s_cancelHandle = NativeMethods.CreateWindowExW(
            0, "BUTTON", "Vazgeç",
            NativeMethods.WsChild | NativeMethods.WsVisible | NativeMethods.WsTabStop | NativeMethods.BsOwnerDraw,
            Scale(328), Scale(108), Scale(84), Scale(28),
            window, NativeMethods.IdCancel, instance, 0);

        foreach (var control in new[] { messageLabel, fileLabel })
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

    [UnmanagedCallersOnly]
    private static nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case NativeMethods.WmNcHitTest:
                return NeonWindowChrome.HitTest(hwnd, lParam, CaptionButtons);

            case NativeMethods.WmPaint:
                NeonWindowChrome.DrawCaption(hwnd, s_backgroundBrush, s_caption, CaptionButtons, s_sansFont);
                return 0;

            case NativeMethods.WmActivate:
                NativeMethods.InvalidateRect(hwnd, 0, false);
                break;

            case NativeMethods.WmCtlColorStatic:
                NativeMethods.SetBkMode(wParam, NativeMethods.TransparentBkMode);
                NativeMethods.SetTextColor(wParam, NeonWindowChrome.ColorTextDim);
                return s_backgroundBrush;

            case NativeMethods.WmDrawItem:
                var item = *(NativeMethods.DrawItemStruct*)lParam;
                if (item.HwndItem == s_settingsHandle || item.HwndItem == s_cancelHandle)
                {
                    var focused = (item.ItemState & NativeMethods.OdsFocus) != 0;
                    var primary = item.HwndItem == s_settingsHandle;
                    NeonWindowChrome.DrawNeonButton(item.Hdc, item.RcItem, primary ? "Ayarları aç" : "Vazgeç", primary, focused);
                    return 1;
                }

                break;

            case NativeMethods.WmCommand:
                var controlId = (int)(wParam & 0xFFFF);
                if (controlId is NativeMethods.IdOk or NativeMethods.IdCancel)
                {
                    s_openSettings = controlId == NativeMethods.IdOk;
                    s_closing = true;
                    NativeMethods.DestroyWindow(hwnd);
                    return 0;
                }

                break;

            case NativeMethods.WmClose:
                s_openSettings = false;
                s_closing = true;
                NativeMethods.DestroyWindow(hwnd);
                return 0;

            case NativeMethods.WmDestroy:
                if (!s_closing)
                {
                    s_openSettings = false;
                }

                NativeMethods.PostQuitMessage(0);
                return 0;
        }

        return NativeMethods.DefWindowProcW(hwnd, message, wParam, lParam);
    }
}
