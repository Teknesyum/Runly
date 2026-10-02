using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Runly.Settings;

internal enum CaptionItemStyle
{
    Text,
    Link,
}

internal enum CaptionItemIcon
{
    None,
    Coffee,
}

/// <summary>One element carried by <see cref="NeonForm"/>'s own caption band.
///
/// Deliberately not a <see cref="Control"/>. A child control dropped over the band would be laid out
/// against <see cref="NeonForm.DisplayRectangle"/>, which starts below the caption, and every one of
/// them would have to be carved out of the drag hit test by hand. Owner-drawn items keep the band a
/// single painted surface with one hit-test rule.</summary>
internal sealed class CaptionItem
{
    public string Text { get; set; } = string.Empty;

    public CaptionItemStyle Style { get; init; } = CaptionItemStyle.Text;

    public CaptionItemIcon Icon { get; init; } = CaptionItemIcon.None;

    public Font Font { get; init; } = Palette.Body;

    public Color Color { get; init; } = Palette.TextStrong;

    public Color Accent { get; init; } = Palette.Renk1;

    /// <summary>Status marker drawn ahead of the text, or null for no marker.</summary>
    public Color? Dot { get; set; }

    public Action? Click { get; init; }

    public bool Visible { get; set; } = true;

    /// <summary>Placed on the left, right after the window title (the version button). Every other item
    /// runs right to left from the window buttons.</summary>
    public bool Leading { get; init; }

    internal Rectangle Bounds { get; set; }

    internal bool Clickable => Click is not null;
}

/// <summary>Borderless neon window retaining native move, resize, Snap, system-menu and maximize semantics.</summary>
internal class NeonForm : Form
{
    // The caption band, its buttons and the grab gutter all carry text or a pointer target, so they are
    // derived rather than typed: at 150% a 36px band holds a 33px glyph and the close cross is clipped.
    private static int CaptionHeight => Metrics.CaptionHeight;
    private static int CornerRadius => Metrics.WindowCornerRadius;
    private static int CaptionButtonWidth => Metrics.CaptionButtonWidth;
    private static int ResizeBorder => Metrics.ResizeBorder;

    private const int WmNcHitTest = 0x0084;
    private const int WmNcCalcSize = 0x0083;
    private const int WsThickFrame = 0x00040000;
    private const int WsMaximizeBox = 0x00010000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmStyleChanged = 0x007D;
    private const int GwlStyle = -16;
    private const int WsBorder = 0x00800000;
    private const int WsDlgFrame = 0x00400000;
    private const int WsCaption = WsBorder | WsDlgFrame;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoActivate = 0x0010;
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

    private readonly List<CaptionItem> _captionItems = [];

    private bool _active;
    private bool _closeHover;
    private bool _maximizeHover;
    private bool _minimizeHover;
    private CaptionItem? _hoverItem;
    private int _captionItemsLeft;

    protected NeonForm()
    {
        using var iconStream = typeof(NeonForm).Assembly.GetManifestResourceStream("Runly.Settings.runly.ico");
        if (iconStream is not null)
        {
            using var embeddedIcon = new Icon(iconStream);
            Icon = (Icon)embeddedIcon.Clone();
        }

        FormBorderStyle = FormBorderStyle.None;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;
        Activated += (_, _) => { _active = true; InvalidateCaption(); };
        Deactivate += (_, _) => { _active = false; InvalidateCaption(); };
        MouseMove += OnCaptionMouseMove;
        MouseLeave += (_, _) => ClearCaptionHover();
        MouseDown += OnCaptionMouseDown;
        Resize += (_, _) => { EnforceBorderlessStyle(); ApplyCornerRegion(); LayoutCaptionItems(); };
    }

    /// <summary>Restores the window styles that <see cref="FormBorderStyle.None"/> strips. The hit test
    /// below already reports HTLEFT/HTCAPTION, but Windows only acts on those codes when the window
    /// actually carries a sizing frame and a maximize box — without them there is no edge resizing, no
    /// double-click-to-maximize and no Aero Snap. The frame is non-visual here; the caption and border
    /// are still drawn by us.</summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style |= WsThickFrame | WsMaximizeBox | WsMinimizeBox;
            return parameters;
        }
    }

    /// <summary>Fills the caption band, right to left, starting next to the minimise button. Items are
    /// given in the order the standard lists them: signature, support link, then everything else.</summary>
    protected void SetCaptionItems(params CaptionItem[] items)
    {
        _captionItems.Clear();
        _captionItems.AddRange(items);
        RefreshCaptionItems();
    }

    /// <summary>The leading part of the title drawn in the accent colour; the rest is drawn in pink text.
    /// Null keeps a one-colour title.</summary>
    protected string? TitleAccent { get; set; } = Runly.Core.Shell.RunlyRegistryLayout.ApplicationName;

    /// <summary>Re-measures the band. Item text is not fixed — the version, the status and the language
    /// switch all change width — so the layout has to be redone whenever one of them is rewritten.</summary>
    protected void RefreshCaptionItems()
    {
        LayoutCaptionItems();
        InvalidateCaption();
    }

    /// Rounds the window corners. FormBorderStyle.None windows get no DWM rounding, so the shape is
    /// clipped by Region instead. Maximized windows stay square: rounded corners there leave the
    /// desktop showing through at the screen edge.
    private void ApplyCornerRegion()
    {
        var previous = Region;
        if (WindowState == FormWindowState.Maximized || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            Region = null;
        }
        else
        {
            using var path = NeonTheme.RoundedRect(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), CornerRadius);
            Region = new Region(path);
        }

        previous?.Dispose();
    }

    public override Rectangle DisplayRectangle
    {
        get
        {
            var display = base.DisplayRectangle;

            // The caption is reserved at the top, and a resize gutter on the other three sides. Child
            // controls are hit-tested before the form is, so an edge covered by a docked child can
            // never start a resize however correct HitTest is — the gutter is what keeps it reachable.
            // A maximized window is not resizable, so it gets the full area.
            var gutter = WindowState == FormWindowState.Maximized ? 0 : ResizeBorder;
            return new Rectangle(
                display.X + gutter,
                display.Y + CaptionHeight,
                Math.Max(0, display.Width - (gutter * 2)),
                Math.Max(0, display.Height - CaptionHeight - gutter));
        }
    }

    /// <summary>The window ground. One gradient covers the whole client area, caption band included —
    /// giving the band a surface fill of its own is the seam the standard forbids, so the only thing
    /// separating it from the content below is the divider drawn in <see cref="OnPaint"/>.</summary>
    protected override void OnPaintBackground(PaintEventArgs e) => NeonBackground.Paint(e.Graphics, this);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        using var divider = new Pen(Color.FromArgb(NeonTheme.BorderAlpha, Palette.Renk1));
        g.DrawLine(divider, 0, CaptionHeight - 1, ClientSize.Width, CaptionHeight - 1);

        var iconInset = Metrics.Px(TeknesyumTokens.Space3);
        var iconSize = Metrics.CaptionIconSize;
        var icon = Icon;
        if (icon is not null)
        {
            g.DrawIcon(icon, new Rectangle(iconInset, (CaptionHeight - iconSize) / 2, iconSize, iconSize));
        }

        var titleLeft = TitleLeft;
        var titleRight = Math.Max(titleLeft, _captionItemsLeft - Metrics.Px(TeknesyumTokens.Space4));
        var titleFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        var accent = TitleAccent;
        var split = !string.IsNullOrEmpty(accent) && Text.StartsWith(accent, StringComparison.Ordinal) && Text.Length > accent.Length;
        if (split)
        {
            var first = TextRenderer.MeasureText(g, accent, Palette.Body, Size.Empty, TextFormatFlags.NoPadding).Width;
            TextRenderer.DrawText(g, accent, Palette.Body,
                new Rectangle(titleLeft, 0, Math.Min(first, titleRight - titleLeft), CaptionHeight),
                _active ? Palette.Renk1 : Palette.TextLabel, titleFlags);
            var restLeft = titleLeft + first;
            TextRenderer.DrawText(g, Text[accent!.Length..], Palette.Body,
                new Rectangle(restLeft, 0, Math.Max(0, titleRight - restLeft), CaptionHeight),
                _active ? Palette.Renk2Text : Palette.TextLabel, titleFlags);
        }
        else
        {
            TextRenderer.DrawText(g, Text, Palette.Body,
                new Rectangle(titleLeft, 0, titleRight - titleLeft, CaptionHeight),
                _active ? Palette.Renk1 : Palette.TextLabel, titleFlags);
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var item in _captionItems)
        {
            if (item.Visible)
            {
                DrawCaptionItem(g, item);
            }
        }

        g.SmoothingMode = SmoothingMode.Default;

        DrawCaptionButton(g, MinimizeBounds, _minimizeHover, "─", Palette.Renk1);
        DrawCaptionButton(g, MaximizeBounds, _maximizeHover, WindowState == FormWindowState.Maximized ? "❐" : "□", Palette.Renk1);
        DrawCaptionButton(g, CloseBounds, _closeHover, "×", Palette.Renk2);

        DrawWindowOutline(g);
    }

    /// <summary>Our own edge, drawn inside the corner region. The system border is switched off in
    /// <see cref="OnHandleCreated"/>, and without a replacement a black window has no boundary at all
    /// on a dark desktop. A maximized window gets none: it has no visible edge to draw, and the docked
    /// child fills the gutter the outline would need.</summary>
    private void DrawWindowOutline(Graphics g)
    {
        if (WindowState == FormWindowState.Maximized || ClientSize.Width <= 1 || ClientSize.Height <= 1)
        {
            return;
        }

        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = NeonTheme.RoundedRect(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), CornerRadius))
        using (var pen = new Pen(Color.FromArgb(NeonTheme.BorderAlpha, Palette.Renk1)))
        {
            g.DrawPath(pen, path);
        }

        g.SmoothingMode = previous;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NeonTheme.RemoveSystemBorder(this);

        // Second line of defence: if the guard below is ever bypassed, the caption that surfaces is at
        // least dark instead of a white strip across the neon band.
        NeonTheme.ApplyDarkTitleBar(this);
        EnforceBorderlessStyle();
    }

    /// <summary>Strips WS_CAPTION, WS_DLGFRAME and WS_BORDER back off the window. FormBorderStyle.None
    /// never sets them, but shell extensions that redraw window frames (StartAllBack and the like) and
    /// injected hooks do add them from outside the process, and the result is a classic light title bar
    /// painted over our own band. WS_THICKFRAME, WS_MAXIMIZEBOX and WS_MINIMIZEBOX are left alone —
    /// <see cref="CreateParams"/> adds them on purpose for edge resizing, Snap and double-click maximize.
    /// The write only happens when the style is actually dirty, so the WM_STYLECHANGED that SetWindowLong
    /// raises cannot feed back into another write.</summary>
    private void EnforceBorderlessStyle()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var style = GetWindowLong(Handle, GwlStyle);
        var cleaned = style & ~(WsCaption | WsDlgFrame | WsBorder);
        if (cleaned == style)
        {
            return;
        }

        SetWindowLong(Handle, GwlStyle, cleaned);
        SetWindowPos(Handle, nint.Zero, 0, 0, 0, 0,
            SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint hwnd, int index, int value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    protected override void OnShown(EventArgs e)
    {
        Strings.Apply(this);
        ApplyCornerRegion();
        LayoutCaptionItems();

        // Every window, not just the main one: native scroll bars and combo popups render light by default
        // and a dialog that skips this opens with white bars inside a black theme.
        NeonTheme.ApplyDarkScrollBars(this);
        base.OnShown(e);
    }

    protected override void WndProc(ref Message m)
    {
        // The sizing frame added in CreateParams would otherwise eat a 7px non-client border on every
        // side, insetting the drawn surface and putting the corner region on the wrong rectangle.
        // Reporting no non-client area gives the frame's behaviour without its pixels.
        if (m.Msg == WmNcCalcSize && m.WParam != IntPtr.Zero)
        {
            m.Result = IntPtr.Zero;
            return;
        }

        if (m.Msg == WmStyleChanged)
        {
            base.WndProc(ref m);
            EnforceBorderlessStyle();
            return;
        }

        if (m.Msg == WmNcHitTest)
        {
            base.WndProc(ref m);
            if ((int)m.Result == HtClient)
            {
                var screen = new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16)));
                var point = PointToClient(screen);
                m.Result = HitTest(point);
            }
            return;
        }

        if (m.Msg == WmGetMinMaxInfo)
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
            var screen = Screen.FromHandle(Handle);
            var work = screen.WorkingArea;
            var bounds = screen.Bounds;
            info.MaxPosition = new NativePoint(work.Left - bounds.Left, work.Top - bounds.Top);
            info.MaxSize = new NativePoint(work.Width, work.Height);
            Marshal.StructureToPtr(info, m.LParam, false);
        }

        base.WndProc(ref m);
    }

    private nint HitTest(Point point)
    {
        if (WindowState != FormWindowState.Maximized)
        {
            var left = point.X < ResizeBorder;
            var right = point.X >= ClientSize.Width - ResizeBorder;
            var top = point.Y < ResizeBorder;
            var bottom = point.Y >= ClientSize.Height - ResizeBorder;
            if (left && top) return HtTopLeft;
            if (right && top) return HtTopRight;
            if (left && bottom) return HtBottomLeft;
            if (right && bottom) return HtBottomRight;
            if (left) return HtLeft;
            if (right) return HtRight;
            if (top) return HtTop;
            if (bottom) return HtBottom;
        }

        // HTCAPTION is what gives the band drag, double-click-to-maximize and Snap, so anything that is
        // not an item or a window button has to keep reporting it. The items report HTCLIENT instead,
        // which is what routes the click to the mouse handlers below.
        if (point.Y >= CaptionHeight)
        {
            return HtClient;
        }

        return !CaptionButtonsBounds.Contains(point) && CaptionItemAt(point) is null ? HtCaption : HtClient;
    }

    private CaptionItem? CaptionItemAt(Point point)
    {
        foreach (var item in _captionItems)
        {
            if (item.Visible && item.Bounds.Contains(point))
            {
                return item;
            }
        }

        return null;
    }

    private static int TitleLeft => Metrics.Px(TeknesyumTokens.Space3) + Metrics.CaptionIconSize + Metrics.Px(TeknesyumTokens.Space2);

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        RefreshCaptionItems();
    }

    private void LayoutCaptionItems()
    {
        var height = Metrics.CaptionItemHeight;
        var top = (CaptionHeight - height) / 2;
        var gap = Metrics.Px(TeknesyumTokens.Space4);
        var right = ClientSize.Width - (CaptionButtonWidth * 3) - Metrics.Px(TeknesyumTokens.Space2);
        _captionItemsLeft = right;

        foreach (var item in _captionItems)
        {
            if (!item.Visible || item.Leading)
            {
                item.Bounds = Rectangle.Empty;
                continue;
            }

            var width = MeasureCaptionItem(item);
            right -= width;
            item.Bounds = new Rectangle(right, top, width, height);
            _captionItemsLeft = right;
            right -= gap;
        }

        var left = TitleLeft + TextRenderer.MeasureText(Text, Palette.Body, Size.Empty, TextFormatFlags.NoPadding).Width + Metrics.Px(TeknesyumTokens.Space3);
        foreach (var item in _captionItems)
        {
            if (!item.Visible || !item.Leading)
            {
                continue;
            }

            var width = MeasureCaptionItem(item);
            item.Bounds = left + width <= _captionItemsLeft - gap ? new Rectangle(left, top, width, height) : Rectangle.Empty;
            left += width + gap;
        }
    }

    private static int MeasureCaptionItem(CaptionItem item)
    {
        var width = TextRenderer.MeasureText(item.Text, item.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        if (item.Dot is not null)
        {
            width += CaptionDotSize + Metrics.Px(TeknesyumTokens.Space2);
        }

        if (item.Icon != CaptionItemIcon.None)
        {
            width += CaptionSponsorIconSize + Metrics.Px(TeknesyumTokens.Space2);
        }

        var padding = Metrics.Px(TeknesyumTokens.Space2);
        return Math.Max(Metrics.Px(TeknesyumTokens.Space5), width + (padding * 2));
    }

    private static int CaptionDotSize => Metrics.Px(TeknesyumTokens.Space2);

    private static int CaptionSponsorIconSize => Metrics.Px(TeknesyumTokens.Space3);

    private void DrawCaptionItem(Graphics g, CaptionItem item)
    {
        var hover = ReferenceEquals(item, _hoverItem);
        var bounds = item.Bounds;

        var content = Rectangle.Inflate(bounds, -Metrics.Px(TeknesyumTokens.Space2), 0);
        if (hover && item.Clickable)
        {
            using var underline = new Pen(item.Accent, Metrics.Scale);
            var baseline = content.Bottom - Metrics.Px(TeknesyumTokens.Space1);
            g.DrawLine(underline, content.Left, baseline, content.Right, baseline);
        }

        var color = item.Style == CaptionItemStyle.Link && hover ? item.Accent : item.Color;
        var left = content.Left;
        if (item.Dot is Color dot)
        {
            var diameter = CaptionDotSize;
            using var marker = new SolidBrush(dot);
            g.FillEllipse(marker, left, content.Top + ((content.Height - diameter) / 2), diameter, diameter);
            left += diameter + Metrics.Px(TeknesyumTokens.Space2);
        }

        if (item.Icon == CaptionItemIcon.Coffee)
        {
            var size = CaptionSponsorIconSize;
            DrawCoffeeIcon(g, new Rectangle(left, content.Top + ((content.Height - size) / 2), size, size), color);
            left += size + Metrics.Px(TeknesyumTokens.Space2);
        }

        TextRenderer.DrawText(g, item.Text, item.Font,
            new Rectangle(left, content.Top, Math.Max(0, content.Right - left), content.Height), color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    /// <summary>Support-link mark, R5 §4: a stroked 12 DIP shape, not the ☕ emoji — the emoji renders
    /// in the system colour font and cannot take the accent colour.</summary>
    private static void DrawCoffeeIcon(Graphics g, Rectangle box, Color color)
    {
        var unit = box.Width / 12f;
        using var pen = new Pen(color, Math.Max(1f, unit * 1.2f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        float X(float u) => box.X + (u * unit);
        float Y(float u) => box.Y + (u * unit);

        using (var cup = new GraphicsPath())
        {
            cup.AddLine(X(1.6f), Y(4.6f), X(2.7f), Y(10.4f));
            cup.AddLine(X(2.7f), Y(10.4f), X(7.3f), Y(10.4f));
            cup.AddLine(X(7.3f), Y(10.4f), X(8.4f), Y(4.6f));
            cup.CloseFigure();
            g.DrawPath(pen, cup);
        }

        g.DrawArc(pen, X(8.0f), Y(5.4f), unit * 3.4f, unit * 3.4f, -70f, 150f);
        g.DrawLine(pen, X(3.9f), Y(2.8f), X(3.9f), Y(1.0f));
        g.DrawLine(pen, X(6.2f), Y(2.8f), X(6.2f), Y(1.0f));
    }

    private void OnCaptionMouseMove(object? sender, MouseEventArgs e)
    {
        var close = CloseBounds.Contains(e.Location);
        var maximize = MaximizeBox && MaximizeBounds.Contains(e.Location);
        var minimize = MinimizeBox && MinimizeBounds.Contains(e.Location);
        var item = CaptionItemAt(e.Location);
        if (item is not null && !item.Clickable)
        {
            item = null;
        }

        if (close == _closeHover && maximize == _maximizeHover && minimize == _minimizeHover &&
            ReferenceEquals(item, _hoverItem))
        {
            return;
        }

        _closeHover = close;
        _maximizeHover = maximize;
        _minimizeHover = minimize;
        _hoverItem = item;
        Cursor = item is null ? Cursors.Default : Cursors.Hand;
        InvalidateCaption();
    }

    private void OnCaptionMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var item = CaptionItemAt(e.Location);
        if (item?.Click is not null)
        {
            item.Click();
        }
        else if (CloseBounds.Contains(e.Location)) Close();
        else if (MaximizeBox && MaximizeBounds.Contains(e.Location))
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        else if (MinimizeBox && MinimizeBounds.Contains(e.Location)) WindowState = FormWindowState.Minimized;
    }

    private void DrawCaptionButton(Graphics g, Rectangle bounds, bool hover, string glyph, Color accent)
    {
        if ((bounds == MaximizeBounds && !MaximizeBox) || (bounds == MinimizeBounds && !MinimizeBox)) return;
        if (hover)
        {
            using var fill = new SolidBrush(Color.FromArgb(TeknesyumTokens.Tone20Alpha, accent));
            g.FillRectangle(fill, bounds);
        }
        TextRenderer.DrawText(g, glyph, Palette.CaptionGlyph, bounds, Palette.TextStrong,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void ClearCaptionHover()
    {
        if (!_closeHover && !_maximizeHover && !_minimizeHover && _hoverItem is null) return;
        _closeHover = _maximizeHover = _minimizeHover = false;
        _hoverItem = null;
        Cursor = Cursors.Default;
        InvalidateCaption();
    }

    private void InvalidateCaption() => Invalidate(new Rectangle(0, 0, ClientSize.Width, CaptionHeight));
    private Rectangle CloseBounds => new(ClientSize.Width - CaptionButtonWidth, 0, CaptionButtonWidth, CaptionHeight);
    private Rectangle MaximizeBounds => new(ClientSize.Width - (CaptionButtonWidth * 2), 0, CaptionButtonWidth, CaptionHeight);
    private Rectangle MinimizeBounds => new(ClientSize.Width - (CaptionButtonWidth * 3), 0, CaptionButtonWidth, CaptionHeight);
    private Rectangle CaptionButtonsBounds => new(ClientSize.Width - (CaptionButtonWidth * 3), 0, CaptionButtonWidth * 3, CaptionHeight);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; public NativePoint(int x, int y) { X = x; Y = y; } }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }
}
