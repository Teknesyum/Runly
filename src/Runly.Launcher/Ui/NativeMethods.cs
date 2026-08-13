using System.Runtime.InteropServices;

namespace Runly.Launcher.Ui;

/// <summary>Win32 entry points and constants used by the launcher's dialogs (SPEC 6, SPEC 7).</summary>
internal static partial class NativeMethods
{
    internal const int WmUser = 0x0400;

    // ---- TaskDialog notifications (TDN_*) -------------------------------------------------
    internal const int TdnCreated = 0;
    internal const int TdnTimer = 4;

    // ---- TaskDialog messages (TDM_*) ------------------------------------------------------
    internal const int TdmEnableButton = WmUser + 111;

    // ---- TaskDialog flags (TDF_*) ---------------------------------------------------------
    internal const int TdfAllowDialogCancellation = 0x0008;
    internal const int TdfExpandFooterArea = 0x0040;
    internal const int TdfVerificationFlagChecked = 0x0100;
    internal const int TdfCallbackTimer = 0x0800;
    internal const int TdfNoDefaultRadioButton = 0x4000;

    // ---- TaskDialog common buttons (TDCBF_*) ----------------------------------------------
    internal const int TdcbfOkButton = 0x0001;
    internal const int TdcbfCancelButton = 0x0008;

    // ---- Stock icons: MAKEINTRESOURCEW(-n) ------------------------------------------------
    internal static readonly nint TdWarningIcon = 0xFFFF;
    internal static readonly nint TdErrorIcon = 0xFFFE;
    internal static readonly nint TdInformationIcon = 0xFFFD;
    internal static readonly nint TdShieldIcon = 0xFFFC;

    // ---- Common control ids ----------------------------------------------------------------
    internal const int IdOk = 1;
    internal const int IdCancel = 2;

    // ---- Window styles ---------------------------------------------------------------------
    internal const int WsChild = unchecked((int)0x40000000);
    internal const int WsVisible = unchecked((int)0x10000000);
    internal const int WsCaption = unchecked((int)0x00C00000);
    internal const int WsPopup = unchecked((int)0x80000000);
    internal const int WsThickFrame = 0x00040000;
    internal const int WsMinimizeBox = 0x00020000;
    internal const int WsMaximizeBox = 0x00010000;
    internal const int WsSysMenu = 0x00080000;
    internal const int WsTabStop = 0x00010000;
    internal const int WsBorder = 0x00800000;
    internal const int WsGroup = 0x00020000;
    internal const int WsExDlgModalFrame = 0x00000001;
    internal const int WsExControlParent = 0x00010000;
    internal const int WsExTopMost = 0x00000008;
    internal const int EsAutoHScroll = 0x0080;
    internal const int BsDefPushButton = 0x0001;
    internal const int BsPushButton = 0x0000;

    // ---- Messages ---------------------------------------------------------------------------
    internal const int WmDestroy = 0x0002;
    internal const int WmPaint = 0x000F;
    internal const int WmNcHitTest = 0x0084;
    internal const int WmActivate = 0x0006;
    internal const int WmClose = 0x0010;
    internal const int WmSetFont = 0x0030;
    internal const int WmCommand = 0x0111;

    // ---- SystemParametersInfo ----------------------------------------------------------------
    internal const uint SpiGetWorkArea = 0x0030;

    // ---- Font -------------------------------------------------------------------------------
    internal const int FwNormal = 400;
    internal const int FwBold = 700;
    internal const int DefaultCharset = 1;
    internal const int ClearTypeQuality = 5;

    // ---- Owner-draw / colour messages -------------------------------------------------------
    internal const int WmCtlColorEdit = 0x0133;
    internal const int WmCtlColorStatic = 0x0138;
    internal const int WmDrawItem = 0x002B;
    internal const int OdsSelected = 0x0001;
    internal const int OdsFocus = 0x0010;
    internal const int TransparentBkMode = 1;
    internal const int OpaqueBkMode = 2;
    internal const int DtCenter = 0x0001;
    internal const int DtVCenter = 0x0004;
    internal const int DtSingleLine = 0x0020;
    internal const int BsOwnerDraw = 0x000B;

    /// <summary>Win32 <c>DRAWITEMSTRUCT</c>; laid out to be read with a raw pointer cast (AOT-safe, no reflection).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DrawItemStruct
    {
        public uint CtlType;
        public uint CtlID;
        public uint ItemID;
        public uint ItemAction;
        public uint ItemState;
        public nint HwndItem;
        public nint Hdc;
        public Rect RcItem;
        public nuint ItemData;
    }

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateSolidBrush(uint colorref);

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreatePen(int style, int width, uint colorref);

    [LibraryImport("gdi32.dll")]
    internal static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    internal static partial uint SetBkColor(nint hdc, uint colorref);

    [LibraryImport("gdi32.dll")]
    internal static partial uint SetTextColor(nint hdc, uint colorref);

    [LibraryImport("gdi32.dll")]
    internal static partial int SetBkMode(nint hdc, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RoundRect(nint hdc, int left, int top, int right, int bottom, int width, int height);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int DrawTextW(nint hdc, string text, int count, ref Rect rect, uint format);

    [LibraryImport("gdi32.dll")]
    internal static partial int GetTextFaceW(nint hdc, int count, nint faceName);

    [LibraryImport("user32.dll")]
    internal static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(nint hwnd, int attribute, in int value, int size);

    /// <summary>The <c>TASKDIALOG_BUTTON</c> struct; <c>commctrl.h</c> packs it to 1 byte.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct TaskDialogButton
    {
        public int ButtonId;
        public nint ButtonText;
    }

    /// <summary>The <c>TASKDIALOGCONFIG</c> struct; <c>commctrl.h</c> packs it to 1 byte.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct TaskDialogConfig
    {
        public uint Size;
        public nint Parent;
        public nint Instance;
        public int Flags;
        public int CommonButtons;
        public nint WindowTitle;
        public nint MainIcon;
        public nint MainInstruction;
        public nint Content;
        public uint ButtonCount;
        public nint Buttons;
        public int DefaultButton;
        public uint RadioButtonCount;
        public nint RadioButtons;
        public int DefaultRadioButton;
        public nint VerificationText;
        public nint ExpandedInformation;
        public nint ExpandedControlText;
        public nint CollapsedControlText;
        public nint FooterIcon;
        public nint Footer;
        public nint Callback;
        public nint CallbackData;
        public uint Width;
    }

    /// <summary>The <c>WNDCLASSEXW</c> struct used to register the argument prompt's window class.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public nint WndProc;
        public int ClsExtra;
        public int WndExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
        public nint IconSm;
    }

    /// <summary>The Win32 <c>MSG</c> struct.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int PointX;
        public int PointY;
    }

    /// <summary>The Win32 <c>RECT</c> struct.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PaintStruct
    {
        public nint Hdc;
        public int Erase;
        public Rect Paint;
        public int Restore;
        public int IncUpdate;
        public fixed byte Reserved[32];
    }

    [LibraryImport("comctl32.dll", SetLastError = false)]
    internal static partial int TaskDialogIndirect(
        in TaskDialogConfig config,
        out int button,
        out int radioButton,
        out int verificationFlagChecked);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GetConsoleWindow();

    [LibraryImport("kernel32.dll")]
    internal static partial nint GetModuleHandleW(nint moduleName);

    [LibraryImport("user32.dll")]
    internal static partial nint SendMessageW(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial ushort RegisterClassExW(in WndClassEx windowClass);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint CreateWindowExW(
        int exStyle,
        string className,
        string? windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint param);

    [LibraryImport("user32.dll")]
    internal static partial nint DefWindowProcW(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    internal static partial nint BeginPaint(nint hwnd, out PaintStruct paint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EndPaint(nint hwnd, in PaintStruct paint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FillRect(nint hdc, in Rect rect, nint brush);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InvalidateRect(nint hwnd, nint rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll")]
    internal static partial int GetMessageW(out Msg message, nint hwnd, uint filterMin, uint filterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateMessage(in Msg message);

    [LibraryImport("user32.dll")]
    internal static partial nint DispatchMessageW(in Msg message);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsDialogMessageW(nint dialog, ref Msg message);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial nint SetFocus(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool MoveWindow(nint hwnd, int x, int y, int width, int height, [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll")]
    internal static partial int GetWindowTextLengthW(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial int GetWindowTextW(nint hwnd, nint text, int maxCount);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfoW(uint action, uint param, out Rect result, uint winIni);

    [LibraryImport("user32.dll")]
    internal static partial nint LoadCursorW(nint instance, nint cursorName);

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateFontW(
        int height,
        int width,
        int escapement,
        int orientation,
        int weight,
        uint italic,
        uint underline,
        uint strikeOut,
        uint charSet,
        uint outputPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string faceName);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint handle);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CommandLineToArgvW(string commandLine, out int argumentCount);

    [LibraryImport("kernel32.dll")]
    internal static partial nint LocalFree(nint memory);
}
