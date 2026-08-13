using System.Runtime.InteropServices;

namespace Runly.Settings;

/// <summary>Borderless neon window retaining native move, resize, Snap, system-menu and maximize semantics.</summary>
internal class NeonForm : Form
{
    private const int CaptionHeight = 36;
    private const int CornerRadius = 12;
    private const int CaptionButtonWidth = 52;
    private const int ResizeBorder = 7;
    private const int WmNcHitTest = 0x0084;
    private const int WmGetMinMaxInfo = 0x0024;
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

    private bool _active;
    private bool _closeHover;
    private bool _maximizeHover;
    private bool _minimizeHover;

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
        Resize += (_, _) => ApplyCornerRegion();
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
            return new Rectangle(display.X, display.Y + CaptionHeight, display.Width, Math.Max(0, display.Height - CaptionHeight));
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

        var icon = Icon;
        if (icon is not null)
        {
            g.DrawIcon(icon, new Rectangle(12, 10, 16, 16));
        }

        var titleColor = _active ? Palette.NeonBlue : Palette.TextLabel;
        TextRenderer.DrawText(g, Text, Palette.Body, new Rectangle(36, 0, Math.Max(0, Width - (CaptionButtonWidth * 3) - 44), CaptionHeight),
            titleColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        DrawCaptionButton(g, MinimizeBounds, _minimizeHover, "─", Palette.NeonBlue);
        DrawCaptionButton(g, MaximizeBounds, _maximizeHover, WindowState == FormWindowState.Maximized ? "❐" : "□", Palette.NeonBlue);
        DrawCaptionButton(g, CloseBounds, _closeHover, "×", Palette.NeonPink);
    }

    protected override void OnShown(EventArgs e)
    {
        Strings.Apply(this);
        ApplyCornerRegion();
        base.OnShown(e);
    }

    protected override void WndProc(ref Message m)
    {
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

        return point.Y < CaptionHeight && !CaptionButtonsBounds.Contains(point) ? HtCaption : HtClient;
    }

    private void OnCaptionMouseMove(object? sender, MouseEventArgs e)
    {
        var close = CloseBounds.Contains(e.Location);
        var maximize = MaximizeBox && MaximizeBounds.Contains(e.Location);
        var minimize = MinimizeBox && MinimizeBounds.Contains(e.Location);
        if (close == _closeHover && maximize == _maximizeHover && minimize == _minimizeHover) return;
        _closeHover = close;
        _maximizeHover = maximize;
        _minimizeHover = minimize;
        InvalidateCaption();
    }

    private void OnCaptionMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (CloseBounds.Contains(e.Location)) Close();
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
        if (!_closeHover && !_maximizeHover && !_minimizeHover) return;
        _closeHover = _maximizeHover = _minimizeHover = false;
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
