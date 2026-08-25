using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Runly.Settings;

internal enum CaptionItemStyle
{
    Text,
    Link,
    Outline,
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

    public Color Accent { get; init; } = Palette.NeonBlue;

    /// <summary>Status marker drawn ahead of the text, or null for no marker.</summary>
    public Color? Dot { get; set; }

    public Action? Click { get; init; }

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
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        using var background = new SolidBrush(Palette.Surface);
        g.FillRectangle(background, 0, 0, ClientSize.Width, CaptionHeight);
        using var divider = new Pen(Color.FromArgb(80, Palette.NeonBlue));
        g.DrawLine(divider, 0, CaptionHeight - 1, ClientSize.Width, CaptionHeight - 1);

        var iconInset = Metrics.Px(12);
        var iconSize = Metrics.CaptionIconSize;
        var icon = Icon;
        if (icon is not null)
        {
            g.DrawIcon(icon, new Rectangle(iconInset, (CaptionHeight - iconSize) / 2, iconSize, iconSize));
        }

        var titleLeft = iconInset + iconSize + Metrics.Px(8);
        var titleColor = _active ? Palette.NeonBlue : Palette.TextLabel;
        TextRenderer.DrawText(g, Text, Palette.Body,
            new Rectangle(titleLeft, 0, Math.Max(0, _captionItemsLeft - Metrics.Px(16) - titleLeft), CaptionHeight),
            titleColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var item in _captionItems)
        {
            DrawCaptionItem(g, item);
        }

        g.SmoothingMode = SmoothingMode.Default;

        DrawCaptionButton(g, MinimizeBounds, _minimizeHover, "─", Palette.NeonBlue);
        DrawCaptionButton(g, MaximizeBounds, _maximizeHover, WindowState == FormWindowState.Maximized ? "❐" : "□", Palette.NeonBlue);
        DrawCaptionButton(g, CloseBounds, _closeHover, "×", Palette.NeonPink);

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
        using (var pen = new Pen(Color.FromArgb(77, Palette.NeonBlue)))
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
            if (item.Bounds.Contains(point))
            {
                return item;
            }
        }

        return null;
    }

    private void LayoutCaptionItems()
    {
        var height = Metrics.CaptionItemHeight;
        var top = (CaptionHeight - height) / 2;
        var gap = Metrics.Px(16);
        var right = ClientSize.Width - (CaptionButtonWidth * 3) - Metrics.Px(8);
        _captionItemsLeft = right;

        foreach (var item in _captionItems)
        {
            var width = MeasureCaptionItem(item);
            right -= width;
            item.Bounds = new Rectangle(right, top, width, height);
            _captionItemsLeft = right;
            right -= gap;
        }
    }

    private static int MeasureCaptionItem(CaptionItem item)
    {
        var width = TextRenderer.MeasureText(item.Text, item.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        if (item.Dot is not null)
        {
            width += CaptionDotSize + Metrics.Px(6);
        }

        if (item.Icon != CaptionItemIcon.None)
        {
            width += CaptionSponsorIconSize + Metrics.Px(6);
        }

        var padding = item.Style == CaptionItemStyle.Outline ? Metrics.Px(12) : Metrics.Px(8);
        return Math.Max(Metrics.Px(24), width + (padding * 2));
    }

    private static int CaptionDotSize => Metrics.Px(8);

    private static int CaptionSponsorIconSize => Metrics.Px(12);

    private void DrawCaptionItem(Graphics g, CaptionItem item)
    {
        var hover = ReferenceEquals(item, _hoverItem);
        var bounds = item.Bounds;
        Rectangle content;

        if (item.Style == CaptionItemStyle.Outline)
        {
            var frame = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using var path = NeonTheme.RoundedRect(frame, Metrics.Px(12));

            // Outline button, R5 §4: no fill, ever. Hover only takes the border to full opacity and
            // opens the outer glow, which is drawn as widening low-alpha strokes because GDI+ has no
            // shadow primitive.
            if (hover)
            {
                for (var ring = 3; ring >= 1; ring--)
                {
                    using var glow = new Pen(Color.FromArgb(26, item.Accent), ((ring * 2) + 1) * Metrics.Scale);
                    g.DrawPath(glow, path);
                }
            }

            using var border = new Pen(hover ? item.Accent : Color.FromArgb(128, item.Accent), 1.5f * Metrics.Scale);
            g.DrawPath(border, path);
            content = Rectangle.Inflate(bounds, -Metrics.Px(12), 0);
        }
        else
        {
            content = Rectangle.Inflate(bounds, -Metrics.Px(8), 0);
            if (hover && item.Clickable)
            {
                using var underline = new Pen(item.Accent, Metrics.Scale);
                var baseline = content.Bottom - Metrics.Px(4);
                g.DrawLine(underline, content.Left, baseline, content.Right, baseline);
            }
        }

        var left = content.Left;
        if (item.Dot is Color dot)
        {
            var diameter = CaptionDotSize;
            using var marker = new SolidBrush(dot);
            g.FillEllipse(marker, left, content.Top + ((content.Height - diameter) / 2), diameter, diameter);
            left += diameter + Metrics.Px(6);
        }

        if (item.Icon == CaptionItemIcon.Coffee)
        {
            var size = CaptionSponsorIconSize;
            DrawCoffeeIcon(g, new Rectangle(left, content.Top + ((content.Height - size) / 2), size, size), item.Accent);
            left += size + Metrics.Px(6);
        }

        var color = item.Style switch
        {
            CaptionItemStyle.Outline => item.Accent,
            CaptionItemStyle.Link when hover => item.Accent,
            _ => item.Color,
        };

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
            using var fill = new SolidBrush(Color.FromArgb(35, accent));
            g.FillRectangle(fill, bounds);
        }
        TextRenderer.DrawText(g, glyph, Palette.CaptionGlyph, bounds, hover ? accent : Palette.TextStrong,
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
