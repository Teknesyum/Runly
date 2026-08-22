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
            case WmNcPaint:
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
            using var pen = new Pen(focused ? Palette.NeonBlue : NeonTheme.IdleOutline, stroke);
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
        ForeColor = Palette.NeonBlue;
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
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        NeonField.Handle(this, ref m);
    }
}
