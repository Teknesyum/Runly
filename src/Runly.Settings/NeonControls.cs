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
            SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        }

        foreach (Control child in control.Controls)
        {
            ApplyDarkScrollBarsCore(child);
        }
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

/// <summary>Replaces <see cref="GroupBox"/>: a rounded, glow-bordered panel with an uppercase neon title.</summary>
internal sealed class NeonGroupPanel : Panel
{
    public string Title { get; set; }

    public NeonGroupPanel(string title)
    {
        Title = title.ToUpperInvariant();
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

        using (var ring = new Pen(Checked ? Palette.NeonBlue : Palette.TextLabel, 1.5f * Metrics.Scale))
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
        const int grid = NeonRadioButton.GlyphGrid;
        var d = Metrics.Px(grid);
        var box = new Rectangle(0, (Height - d) / 2, d, d);
        using var path = NeonTheme.RoundedRect(box, Metrics.Px(3));

        if (Checked)
        {
            using var fill = new SolidBrush(Palette.NeonBlue);
            g.FillPath(fill, path);
        }

        using (var ring = new Pen(Checked ? Palette.NeonBlue : Palette.TextLabel, 1.5f * Metrics.Scale))
        {
            g.DrawPath(ring, path);
        }

        if (Checked)
        {
            using var check = new Pen(Palette.Surface, 2f * Metrics.Scale);
            Point[] checkPoints =
            [
                new Point(box.X + (d * 3 / grid), box.Y + (d * 7 / grid)),
                new Point(box.X + (d * 6 / grid), box.Y + (d * 10 / grid)),
                new Point(box.X + (d * 11 / grid), box.Y + (d * 4 / grid)),
            ];
            g.DrawLines(check, checkPoints);
        }

        var gap = Metrics.Px(8);
        var textBounds = new Rectangle(d + gap, 0, Width - d - gap, Height);
        TextRenderer.DrawText(g, Text, Font, textBounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Owner-drawn dark combo box. A native ComboBox paints its list and its closed field with
/// system colours, which reads as a white hole in this theme once the control is actually visible.</summary>
internal sealed class NeonComboBox : ComboBox
{
    public NeonComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        BackColor = Palette.FieldBg;
        ForeColor = Palette.TextBody;
        ApplyItemHeight();
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Color.FromArgb(120, Palette.NeonBlue));
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }
}
