using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Runly.Settings;

/// Teknesyum Neon — sahip-çizim WinForms bileşenleri (R5). WinForms varsayılan kontrolleri
/// temasız olduğu için panel/buton/radyo/onay kutusu burada elle çiziliyor.
internal static class NeonTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>Turns the title bar dark (SPEC/R5: otherwise a white strip sits on top of the neon window).</summary>
    public static void ApplyDarkTitleBar(Form form)
    {
        var value = 1;
        DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
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
        Height = 32;
        MinimumSize = new Size(0, 30);
        Padding = new Padding(14, 4, 14, 4);
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        return new Size(text.Width + Padding.Horizontal + 6, Math.Max(32, text.Height + Padding.Vertical + 4));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var accent = Primary ? Palette.NeonBlue : Palette.NeonPurple;
        var bounds = new Rectangle(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));

        using var path = NeonTheme.RoundedRect(bounds, 12);

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

        using (var glow = new Pen(Color.FromArgb(_hover ? 200 : 130, accent), _hover ? 2f : 1.5f))
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
        Padding = new Padding(16, 34, 16, 16);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Palette.AppBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

        using var path = NeonTheme.RoundedRect(bounds, 16);
        using (var fill = new SolidBrush(Palette.Surface))
        {
            g.FillPath(fill, path);
        }

        using (var border = new Pen(Color.FromArgb(80, Palette.NeonBlue), 1f))
        {
            g.DrawPath(border, path);
        }

        TextRenderer.DrawText(g, Title, Palette.H3, new Rectangle(16, 10, Width - 32, 20),
            Palette.NeonBlue, TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }
}

/// <summary>Owner-drawn radio button: system glyphs cannot be recoloured without full custom paint.</summary>
internal sealed class NeonRadioButton : RadioButton
{
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
        return new Size(14 + 8 + textSize.Width + 2, Math.Max(18, textSize.Height));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const int d = 14;
        var circle = new Rectangle(0, (Height - d) / 2, d, d);

        using (var ring = new Pen(Checked ? Palette.NeonBlue : Palette.TextLabel, 1.5f))
        {
            g.DrawEllipse(ring, circle);
        }

        if (Checked)
        {
            using var dot = new SolidBrush(Palette.NeonBlue);
            g.FillEllipse(dot, circle.X + 3, circle.Y + 3, d - 6, d - 6);
        }

        var textBounds = new Rectangle(d + 8, 0, Width - d - 8, Height);
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
        return new Size(14 + 8 + textSize.Width + 2, Math.Max(18, textSize.Height));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const int d = 14;
        var box = new Rectangle(0, (Height - d) / 2, d, d);
        using var path = NeonTheme.RoundedRect(box, 3);

        if (Checked)
        {
            using var fill = new SolidBrush(Palette.NeonBlue);
            g.FillPath(fill, path);
        }

        using (var ring = new Pen(Checked ? Palette.NeonBlue : Palette.TextLabel, 1.5f))
        {
            g.DrawPath(ring, path);
        }

        if (Checked)
        {
            using var check = new Pen(Palette.Surface, 2f);
            Point[] checkPoints =
            [
                new Point(box.X + 3, box.Y + 7),
                new Point(box.X + 6, box.Y + 10),
                new Point(box.X + 11, box.Y + 4),
            ];
            g.DrawLines(check, checkPoints);
        }

        var textBounds = new Rectangle(d + 8, 0, Width - d - 8, Height);
        TextRenderer.DrawText(g, Text, Font, textBounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Single neon-blue link in the footer strip. Opens its target in the default browser.</summary>
internal class NeonLink : LinkLabel
{
    public NeonLink(string text, string url)
    {
        AutoSize = true;
        Text = text;
        Font = Palette.LabelFont;
        LinkColor = Palette.NeonBlue;
        ActiveLinkColor = Palette.NeonPink;
        VisitedLinkColor = Palette.NeonBlue;
        LinkBehavior = LinkBehavior.HoverUnderline;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        LinkClicked += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true })?.Dispose();
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
        ItemHeight = 22;
    }

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

        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
            new Rectangle(e.Bounds.Left + 4, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height),
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

/// <summary>Signature block, R5 §"İmza bloğu": exactly one instance, bottom-right of the settings window.</summary>
internal sealed class SignatureBlock : NeonLink
{
    public SignatureBlock() : base("Teknesyum", Palette.GitHubUrl)
    {
        TextAlign = ContentAlignment.MiddleRight;
    }
}
