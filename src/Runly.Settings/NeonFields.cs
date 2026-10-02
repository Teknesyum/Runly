using System.Runtime.InteropServices;

namespace Runly.Settings;

/// <summary>
/// The neon frame drawn around text and list fields.
///
/// <para><see cref="BorderStyle.FixedSingle"/> is not an option here: WinForms maps it onto the Win32
/// <c>WS_BORDER</c> style, the frame is then painted by the system in <c>SystemColors.Window</c>, and on
/// this dark surface that is a white rectangle around every field. It cannot be recoloured through any
/// property, which is why the border is reserved and painted by hand instead.</para>
///
/// <para>The controls set <see cref="BorderStyle.None"/> so the system draws nothing, then carve a
/// non-client margin out of their own client area (WM_NCCALCSIZE) and fill it themselves (WM_NCPAINT).
/// Anyone tempted to go back to FixedSingle gets the white rectangle back.</para>
/// </summary>
internal static class NeonField
{
    private const int WmNcCalcSize = 0x0083;
    private const int WmNcPaint = 0x0085;
    private const int WmPaint = 0x000F;
    private const int WmSetFocus = 0x0007;
    private const int WmKillFocus = 0x0008;
    private const int WmSize = 0x0005;

    [DllImport("user32.dll")]
    private static extern nint GetWindowDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    /// <summary>Focus needs two design pixels to clear the 3:1 contrast floor at a glance, so the reserved
    /// margin is sized to the focus ring rather than to the idle outline.</summary>
    private static int Margin => Math.Max(1, Metrics.Px(2));

    private static int IdleWidth => Math.Max(1, Metrics.Px(1));

    /// <summary>Draws over a control through its window DC. <see cref="Graphics.FromHwnd"/> hands back the
    /// client DC, whose origin sits inside whatever frame the system drew — which is exactly the frame that
    /// has to be covered, so it stays visible however carefully the rectangle is placed.</summary>
    public static void PaintWindow(Control control, Action<Graphics> paint)
    {
        if (!control.IsHandleCreated || control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        var dc = GetWindowDC(control.Handle);
        if (dc == 0)
        {
            return;
        }

        try
        {
            using var g = Graphics.FromHdc(dc);
            paint(g);
        }
        finally
        {
            ReleaseDC(control.Handle, dc);
        }
    }

    /// <summary>Call after <c>base.WndProc</c>. Reserves the frame and paints it.</summary>
    public static void Handle(Control control, ref Message m)
    {
        switch (m.Msg)
        {
            case WmNcCalcSize when m.WParam != 0:
                Reserve(m.LParam);
                break;
            // WM_PAINT is in the list because WM_NCPAINT is not reliably delivered for every field: the
            // editor box shipped without a visible frame for exactly this reason, and it only became
            // obvious once the field fill stopped being lighter than the surface behind it.
            case WmNcPaint:
            case WmPaint:
            case WmSetFocus:
            case WmKillFocus:
            case WmSize:
                Draw(control);
                break;
        }
    }

    private static void Reserve(nint lParam)
    {
        var parameters = Marshal.PtrToStructure<NcCalcSizeParams>(lParam);
        var margin = Margin;
        parameters.Proposed.Left += margin;
        parameters.Proposed.Top += margin;
        parameters.Proposed.Right -= margin;
        parameters.Proposed.Bottom -= margin;
        Marshal.StructureToPtr(parameters, lParam, false);
    }

    private static void Draw(Control control)
    {
        if (!control.IsHandleCreated || control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        var dc = GetWindowDC(control.Handle);
        if (dc == 0)
        {
            return;
        }

        try
        {
            using var g = Graphics.FromHdc(dc);
            var margin = Margin;
            var width = control.Width;
            var height = control.Height;

            using (var frame = new SolidBrush(control.BackColor))
            {
                g.FillRectangle(frame, 0, 0, width, margin);
                g.FillRectangle(frame, 0, height - margin, width, margin);
                g.FillRectangle(frame, 0, 0, margin, height);
                g.FillRectangle(frame, width - margin, 0, margin, height);
            }

            var focused = control.Focused;
            var stroke = focused ? margin : IdleWidth;
            using var pen = new Pen(focused ? Palette.Renk1 : NeonTheme.IdleOutline, stroke);
            var inset = stroke / 2f;
            g.DrawRectangle(pen, inset, inset, width - stroke, height - stroke);
        }
        finally
        {
            ReleaseDC(control.Handle, dc);
        }
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
    private struct NcCalcSizeParams
    {
        public NativeRect Proposed;
        public NativeRect Before;
        public NativeRect After;
        public nint Sizing;
    }
}

/// <summary>Text field with an owner-drawn neon frame. See <see cref="NeonField"/> for why the border is
/// not a <see cref="BorderStyle"/>.</summary>
internal class NeonTextBox : TextBox
{
    public NeonTextBox()
    {
        BorderStyle = BorderStyle.None;
        BackColor = Palette.FieldBg;
        ForeColor = Palette.Renk1;
        Font = Palette.MonoBody;
        // The frame is carved out of the client area, and TextBox sizes itself to the text alone; without
        // a floor the reserved margin eats into the line and clips the descenders.
        AutoSize = false;
        MinimumSize = new Size(0, Metrics.TextBoxHeight);
        Height = Metrics.TextBoxHeight;
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        NeonField.Handle(this, ref m);
    }
}

/// <summary>Search field: a placeholder that stays visible while the box has focus (the window focuses it on
/// open, and <see cref="TextBox.PlaceholderText"/> hides itself on focus), and a clear glyph inside the right
/// edge that replaces the separate "Temizle" button.</summary>
internal sealed class NeonSearchBox : NeonTextBox
{
    private const int WmPaint = 0x000F;
    private const int EmSetMargins = 0x00D3;
    private const int EcRightMargin = 0x0002;

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private bool _glyphHover;

    public string Cue { get; set; } = string.Empty;

    public event EventHandler? ClearRequested;

    private int GlyphWidth => ClientSize.Height;

    private Rectangle GlyphBounds => new(ClientSize.Width - GlyphWidth, 0, GlyphWidth, ClientSize.Height);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ReserveGlyph();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ReserveGlyph();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ReserveGlyph();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hover = TextLength > 0 && GlyphBounds.Contains(e.Location);
        Cursor = hover ? Cursors.Hand : Cursors.IBeam;
        if (hover != _glyphHover)
        {
            _glyphHover = hover;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_glyphHover)
        {
            _glyphHover = false;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && TextLength > 0 && GlyphBounds.Contains(e.Location))
        {
            ClearRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.OnMouseDown(e);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WmPaint)
        {
            PaintOverlay();
        }
    }

    private void ReserveGlyph()
    {
        if (IsHandleCreated)
        {
            SendMessage(Handle, EmSetMargins, EcRightMargin, (nint)(GlyphWidth << 16));
        }
    }

    private void PaintOverlay()
    {
        if (!IsHandleCreated || ClientSize.Width <= GlyphWidth)
        {
            return;
        }

        using var g = CreateGraphics();
        if (TextLength == 0)
        {
            var cueBounds = new Rectangle(Metrics.Px(2), 0, ClientSize.Width - GlyphWidth - Metrics.Px(2), ClientSize.Height);
            TextRenderer.DrawText(g, Cue, Font, cueBounds, Palette.TextHint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            return;
        }

        using (var fill = new SolidBrush(BackColor))
        {
            g.FillRectangle(fill, GlyphBounds);
        }

        TextRenderer.DrawText(g, "✕", Palette.Body, GlyphBounds, _glyphHover ? Palette.Renk1 : Palette.TextHint,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

/// <summary>List field with an owner-drawn neon frame.</summary>
internal sealed class NeonListBox : ListBox
{
    public NeonListBox()
    {
        BorderStyle = BorderStyle.None;
        BackColor = Palette.FieldBg;
        ForeColor = Palette.TextBody;
        Font = Palette.MonoBody;
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        NeonField.Handle(this, ref m);
    }
}

/// <summary>Details-view list with an owner-drawn neon frame.</summary>
internal sealed class NeonListView : ListView
{
    public NeonListView()
    {
        BorderStyle = BorderStyle.None;
        BackColor = Palette.Surface;
        ForeColor = Palette.TextBody;
        Font = Palette.MonoBody;
        OwnerDraw = true;
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        using (var fill = new SolidBrush(Palette.Surface))
        {
            e.Graphics.FillRectangle(fill, e.Bounds);
        }

        using (var divider = new Pen(Color.FromArgb(NeonTheme.BorderAlpha, Palette.Renk1)))
        {
            e.Graphics.DrawLine(divider, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            e.Graphics.DrawLine(divider, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
        }

        var text = Rectangle.Inflate(e.Bounds, -Metrics.Px(TeknesyumTokens.Space2), 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text, Palette.LabelFont, text, Palette.TextStrong,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        if (e.Item is null || e.SubItem is null)
        {
            return;
        }

        using (var fill = new SolidBrush(e.Item.Selected ? Palette.SelectedFill : Palette.Surface))
        {
            e.Graphics.FillRectangle(fill, e.Bounds);
        }

        var text = Rectangle.Inflate(e.Bounds, -Metrics.Px(TeknesyumTokens.Space2), 0);
        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Font, text, Palette.TextBody,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        NeonField.Handle(this, ref m);
    }
}
