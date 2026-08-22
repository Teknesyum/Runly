using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Runly.Settings;

/// Teknesyum Neon — sahip-çizim WinForms bileşenleri (R5). WinForms varsayılan kontrolleri
/// temasız olduğu için panel/buton/radyo/onay kutusu burada elle çiziliyor.
internal static class NeonTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    // DWMWA_USE_IMMERSIVE_DARK_MODE was attribute 19 before Windows 10 build 19041 and 20 from that
    // build on. Sending only 20 leaves the title bar white on older builds.
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    private const int DwmwaBorderColor = 34;

    /// DWMWA_COLOR_NONE: suppresses the border entirely rather than tinting it.
    private const uint DwmColorNone = 0xFFFFFFFE;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>Turns the title bar dark (SPEC/R5: otherwise a white strip sits on top of the neon window).</summary>
    public static void ApplyDarkTitleBar(Form form)
    {
        var value = 1;

        // DwmSetWindowAttribute reports an unknown attribute through its HRESULT instead of throwing,
        // so the pre-19041 fallback only happens if the result is actually read. Both failing is left
        // silent on purpose: a light title bar must not stop the window from opening.
        if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) < 0)
        {
            DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeLegacy, ref value, sizeof(int));
        }
    }

    /// <summary>Drops the frame Windows 11 draws around every top-level window. Against a black
    /// surface that frame reads as a light grey hairline, and because <see cref="NeonForm"/> clips its
    /// corners with a <see cref="Region"/> the system border falls outside the clip on a restored
    /// window and vanishes when the region is dropped on maximize — which is why it looks intermittent
    /// rather than constant. Returns false on builds before Windows 11 22000, where the attribute does
    /// not exist: the border stays, and that is preferable to refusing to show the window.</summary>
    public static bool RemoveSystemBorder(Form form)
    {
        var value = unchecked((int)DwmColorNone);
        return DwmSetWindowAttribute(form.Handle, DwmwaBorderColor, ref value, sizeof(int)) >= 0;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(nint hwnd, string? subAppName, string? subIdList);

    /// <summary>Outline of a control that is interactive but currently off. Deliberately not
    /// <see cref="Palette.TextLabel"/>: grey means "cannot be clicked" everywhere else in this theme, so
    /// an unchecked box drawn grey reads as a disabled one.</summary>
    public static readonly Color IdleOutline = Color.FromArgb(120, Palette.NeonBlue);

    /// <summary>The one grey in the theme, and the only thing allowed to use it.</summary>
    public static readonly Color DisabledOutline = Palette.TextLabel;

    // The opacity ladder every neon surface uses: rest, hover, pressed, outline. Values outside it make
    // two controls that should look identical drift apart.
    public const int FillAlpha = 26;
    public const int HoverAlpha = 51;
    public const int PressedAlpha = 77;
    public const int OutlineAlpha = 77;

    // uxtheme.dll ordinal 135 = SetPreferredAppMode. Undocumented but the only way to make Win32
    // scrollbars dark; without it a white scrollbar sits inside every grid and list box and breaks
    // the theme. Wrapped in try/catch: if a future Windows build drops the ordinal, the scrollbars
    // stay light instead of the app failing to start.
    [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
    private static extern int SetPreferredAppMode(int mode);

    private const int PreferredAppModeForceDark = 2;

    /// <summary>Opts the process into dark mode so native scrollbars are drawn dark. Best effort.</summary>
    public static void EnableDarkMode()
    {
        try
        {
            SetPreferredAppMode(PreferredAppModeForceDark);
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows build without the ordinal — scrollbars stay light, everything else works.
        }
        catch (DllNotFoundException)
        {
        }
    }

    /// <summary>Applies the dark scrollbar/explorer theme to a control and every child that has its own.</summary>
    public static void ApplyDarkScrollBars(Control root)
    {
        try
        {
            ApplyDarkScrollBarsCore(root);
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }
    }

    private static void ApplyDarkScrollBarsCore(Control control)
    {
        if (control.IsHandleCreated)
        {
            SetWindowTheme(control.Handle, ThemeClassFor(control), null);
        }

        foreach (Control child in control.Controls)
        {
            ApplyDarkScrollBarsCore(child);
        }
    }

    /// <summary>A combo box owns a second, separate window for its drop-down list, and
    /// <c>DarkMode_Explorer</c> does not reach it — the list keeps the white system frame the rest of this
    /// class exists to remove. <c>DarkMode_CFD</c> is the class that covers both.</summary>
    private static string ThemeClassFor(Control control) => control is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer";

    /// <summary>Applies the dark theme class to one control, for callers that theme themselves on handle
    /// creation instead of waiting for the window-wide sweep.</summary>
    public static void ApplyDarkTheme(Control control)
    {
        if (!control.IsHandleCreated)
        {
            return;
        }

        try
        {
            SetWindowTheme(control.Handle, ThemeClassFor(control), null);
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetComboBoxInfo(nint hwnd, ref ComboBoxInfo info);

    /// <summary>Themes the drop-down list of a combo box. The list is a window of its own with a scroll bar
    /// of its own, and the class set on the combo makes the list dark without reaching that bar — which then
    /// stands in the open popup as a white column.</summary>
    public static void ApplyDarkDropDown(ComboBox combo)
    {
        if (!combo.IsHandleCreated)
        {
            return;
        }

        try
        {
            var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
            if (GetComboBoxInfo(combo.Handle, ref info) && info.List != 0)
            {
                SetWindowTheme(info.List, "DarkMode_Explorer", null);
            }
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }
    }

    /// <summary>The check-box glyph, shared by the stand-alone control and the grid cell so the two cannot
    /// drift apart. Every offset is a fraction of <paramref name="box"/>, so it draws at any size.</summary>
    public static void DrawCheckGlyph(Graphics g, Rectangle box, bool isChecked, Color accent, Color outline)
    {
        using var path = RoundedRect(box, Metrics.Px(3));

        if (isChecked)
        {
            using var fill = new SolidBrush(accent);
            g.FillPath(fill, path);
        }

        using (var ring = new Pen(outline, 1.5f * Metrics.Scale))
        {
            g.DrawPath(ring, path);
        }

        if (!isChecked)
        {
            return;
        }

        const int grid = NeonRadioButton.GlyphGrid;
        var d = box.Width;
        using var check = new Pen(Palette.Surface, 2f * Metrics.Scale);
        Point[] points =
        [
            new Point(box.X + (d * 3 / grid), box.Y + (d * 7 / grid)),
            new Point(box.X + (d * 6 / grid), box.Y + (d * 10 / grid)),
            new Point(box.X + (d * 11 / grid), box.Y + (d * 4 / grid)),
        ];
        g.DrawLines(check, points);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ComboBoxInfo
    {
        public int Size;
        public NativeRect Item;
        public NativeRect Button;
        public int ButtonState;
        public nint ComboBox;
        public nint Edit;
        public nint List;
    }

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Filled (primary) or outlined (secondary) rounded neon button, replacing the system flat button.</summary>
internal sealed class NeonButton : Button
{
    public bool Primary { get; set; } = true;

    private bool _hover;

    public NeonButton()
    {
        // No true transparency: an owner-drawn control that doesn't erase its own background leaves stale
        // pixels behind on every repaint (ghosting/overlap). BackColor here is the *parent's* solid colour,
        // painted first every frame, and callers set it to whatever the real parent background is.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Palette.Surface;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;
        Cursor = Cursors.Hand;
        Height = Metrics.ButtonHeight;
        MinimumSize = new Size(0, Metrics.ButtonMinHeight);
        Padding = new Padding(Metrics.Px(14), Metrics.Px(4), Metrics.Px(14), Metrics.Px(4));
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        return new Size(
            text.Width + Padding.Horizontal + Metrics.Px(6),
            Math.Max(Metrics.ButtonHeight, text.Height + Padding.Vertical + Metrics.Px(4)));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var accent = Primary ? Palette.NeonBlue : Palette.NeonPurple;
        var bounds = new Rectangle(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));

        using var path = NeonTheme.RoundedRect(bounds, Metrics.Px(12));

        if (Primary)
        {
            var fillAlpha = _hover ? 230 : 190;
            using var fill = new SolidBrush(Color.FromArgb(fillAlpha, accent));
            g.FillPath(fill, path);
        }
        else if (_hover)
        {
            using var fill = new SolidBrush(Color.FromArgb(30, accent));
            g.FillPath(fill, path);
        }

        using (var glow = new Pen(Color.FromArgb(_hover ? 200 : 130, accent), (_hover ? 2f : 1.5f) * Metrics.Scale))
        {
            g.DrawPath(glow, path);
        }

        var textColor = Primary ? Palette.Surface : accent;
        TextRenderer.DrawText(g, Text, Font, bounds, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>Replaces <see cref="GroupBox"/>: a rounded, glow-bordered panel with a neon title.</summary>
internal sealed class NeonGroupPanel : Panel
{
    public string Title { get; set; }

    public NeonGroupPanel(string title)
    {
        // Uppercase is banned by the standard: it slows reading and destroys the Turkish İ/I pair.
        Title = title;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Palette.Surface;
        ForeColor = Palette.TextBody;
        Padding = new Padding(Metrics.Px(24), Metrics.GroupTitleBand, Metrics.Px(24), Metrics.Px(24));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Palette.AppBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

        using var path = NeonTheme.RoundedRect(bounds, Metrics.Px(16));
        using (var fill = new SolidBrush(Palette.Surface))
        {
            g.FillPath(fill, path);
        }

        using (var border = new Pen(Color.FromArgb(80, Palette.NeonBlue), Metrics.Scale))
        {
            g.DrawPath(border, path);
        }

        var inset = Metrics.Px(24);
        TextRenderer.DrawText(g, Title, Palette.H3,
            new Rectangle(inset, Metrics.Px(10), Width - (inset * 2), Metrics.Line(Palette.H3)),
            Palette.NeonBlue, TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }
}

/// <summary>Owner-drawn radio button: system glyphs cannot be recoloured without full custom paint.</summary>
internal sealed class NeonRadioButton : RadioButton
{
    /// The ring and the tick are drawn on this grid; every offset below is a fraction of it, so one
    /// change to <see cref="GlyphGrid"/> moves the whole glyph instead of leaving half of it behind.
    internal const int GlyphGrid = 14;

    public NeonRadioButton()
    {
        // See NeonButton: BackColor is the real parent colour, cleared every frame — no true transparency.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Palette.Surface;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;
        AutoSize = true;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var textSize = TextRenderer.MeasureText(Text, Font);
        var glyph = Metrics.Px(GlyphGrid) + Metrics.Px(8);
        return new Size(glyph + textSize.Width + Metrics.Px(2), Math.Max(Metrics.Line(Font), textSize.Height));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var d = Metrics.Px(GlyphGrid);
        var circle = new Rectangle(0, (Height - d) / 2, d, d);

        using (var ring = new Pen(Checked ? Palette.NeonBlue : NeonTheme.IdleOutline, 1.5f * Metrics.Scale))
        {
            g.DrawEllipse(ring, circle);
        }

        if (Checked)
        {
            var inset = d * 3 / GlyphGrid;
            using var dot = new SolidBrush(Palette.NeonBlue);
            g.FillEllipse(dot, circle.X + inset, circle.Y + inset, d - (inset * 2), d - (inset * 2));
        }

        var gap = Metrics.Px(8);
        var textBounds = new Rectangle(d + gap, 0, Width - d - gap, Height);
        TextRenderer.DrawText(g, Text, Font, textBounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Owner-drawn checkbox counterpart to <see cref="NeonRadioButton"/>.</summary>
internal sealed class NeonCheckBox : CheckBox
{
    public NeonCheckBox()
    {
        // See NeonButton: BackColor is the real parent colour, cleared every frame — no true transparency.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Palette.Surface;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;
        AutoSize = true;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var textSize = TextRenderer.MeasureText(Text, Font);
        var glyph = Metrics.Px(NeonRadioButton.GlyphGrid) + Metrics.Px(8);
        return new Size(glyph + textSize.Width + Metrics.Px(2), Math.Max(Metrics.Line(Font), textSize.Height));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var d = Metrics.Px(NeonRadioButton.GlyphGrid);
        var box = new Rectangle(0, (Height - d) / 2, d, d);
        NeonTheme.DrawCheckGlyph(g, box, Checked, Palette.NeonBlue, Checked ? Palette.NeonBlue : NeonTheme.IdleOutline);

        var gap = Metrics.Px(8);
        var textBounds = new Rectangle(d + gap, 0, Width - d - gap, Height);
        TextRenderer.DrawText(g, Text, Font, textBounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Owner-drawn dark combo box. A native ComboBox paints its list and its closed field with
/// system colours, which reads as a white hole in this theme once the control is actually visible.</summary>
internal sealed class NeonComboBox : ComboBox
{
    private const int WmPaint = 0x000F;

    public NeonComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        BackColor = Palette.FieldBg;
        ForeColor = Palette.TextBody;
        ApplyItemHeight();
    }

    /// <summary>The drop-down list is a window of its own, created the first time the list opens and not
    /// covered by the window-wide sweep in <see cref="NeonTheme.ApplyDarkScrollBars"/> that runs on Shown.
    /// Theming the combo here is what makes the popup open dark instead of white.</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NeonTheme.ApplyDarkTheme(this);
        NeonTheme.ApplyDarkDropDown(this);
    }

    protected override void OnDropDown(EventArgs e)
    {
        // Repeated on every open: the list window is recreated when the item count changes, and the theme
        // set on the previous one goes with it.
        NeonTheme.ApplyDarkDropDown(this);
        base.OnDropDown(e);
    }

    /// <summary>The item height of an owner-drawn combo is never scaled by WinForms and never follows
    /// the inherited font on its own, so it is re-derived whenever the font arrives or changes.</summary>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyItemHeight();
    }

    private void ApplyItemHeight() => ItemHeight = Metrics.Row(Font, 3);

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            base.OnDrawItem(e);
            return;
        }

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using var background = new SolidBrush(selected ? ColorTranslator.FromHtml("#123238") : Palette.FieldBg);
        e.Graphics.FillRectangle(background, e.Bounds);

        var inset = Metrics.Px(4);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
            new Rectangle(e.Bounds.Left + inset, e.Bounds.Top, e.Bounds.Width - (inset * 2), e.Bounds.Height),
            selected ? Palette.NeonBlue : Palette.TextBody,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    /// <summary>The closed field is drawn by <c>ComboBox.FlatComboAdapter</c>, which hard-codes
    /// <c>SystemColors.Window</c> for the frame and <c>SystemColors.Control</c> for the drop button — a
    /// white outline and a light grey box that no property on the control can recolour. OnPaint is not
    /// raised for a drop-down list combo either, so the only place left to cover them is after the default
    /// WM_PAINT has finished.</summary>
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WmPaint)
        {
            PaintField();
        }
    }

    private void PaintField() => NeonField.PaintWindow(this, g =>
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // The whole field is repainted rather than only the button: the adapter's frame width is a private
        // detail that has already moved between framework versions, and covering it by guess leaves a white
        // hairline the moment it changes.
        using (var fill = new SolidBrush(Palette.FieldBg))
        {
            g.FillRectangle(fill, 0, 0, Width, Height);
        }

        var button = new Rectangle(Width - Metrics.Px(24), 0, Metrics.Px(24), Height);
        var inset = Metrics.Px(8);
        var caption = SelectedIndex >= 0 ? GetItemText(Items[SelectedIndex]) : string.Empty;
        TextRenderer.DrawText(g, caption, Font,
            new Rectangle(inset, 0, Math.Max(0, button.X - inset), Height), ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var d = button.Width;
        var centre = new Point(button.X + (d / 2), button.Y + (button.Height / 2));
        Point[] chevron =
        [
            new Point(centre.X - (d * 3 / 12), centre.Y - (d * 1 / 12)),
            new Point(centre.X, centre.Y + (d * 2 / 12)),
            new Point(centre.X + (d * 3 / 12), centre.Y - (d * 1 / 12)),
        ];
        using (var arrow = new Pen(Palette.NeonBlue, 1.5f * Metrics.Scale))
        {
            g.DrawLines(arrow, chevron);
        }

        var stroke = Focused ? Math.Max(1, Metrics.Px(2)) : Math.Max(1, Metrics.Px(1));
        using var border = new Pen(Focused ? Palette.NeonBlue : NeonTheme.IdleOutline, stroke);
        var edge = stroke / 2f;
        g.DrawRectangle(border, edge, edge, Width - stroke, Height - stroke);
    });
}
