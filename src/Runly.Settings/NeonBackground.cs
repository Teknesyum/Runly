using System.Drawing.Drawing2D;

namespace Runly.Settings;

/// <summary>
/// The one background gradient the window has.
///
/// <para>The standard calls a flat <c>#000000</c> fill an incomplete delivery: the ground is a soft
/// gradient running between <c>bg</c> and <c>surface</c>, and there is exactly one of it — panels sit on
/// top rather than carrying gradients of their own. The two ends differ by 1.06:1, so this is a texture,
/// not a hierarchy; it carries no information.</para>
///
/// <para>Every surface that draws it resolves the brush against the <em>form's</em> client rectangle and
/// then shifts the origin back to its own, which is what keeps the caption band, the layout panels and
/// the owner-drawn buttons on one continuous sheet instead of each restarting the ramp at its own top
/// left corner. Motion is deliberately absent: the standard permits a slow drift, but this is a settings
/// window and a permanently running animation would burn CPU while idle.</para>
/// </summary>
internal static class NeonBackground
{
    /// The standard asks for at least 11 stops because a two-stop ramp bands on a dark screen. Thirteen
    /// is that floor plus a margin; the criterion is the screenshot, not the number.
    private const int StopCount = 13;

    /// <summary>Fills <paramref name="control"/>'s whole client area with the window gradient.</summary>
    public static void Paint(Graphics graphics, Control control)
    {
        var form = control as Form ?? control.FindForm();
        if (form is null || !form.IsHandleCreated || !control.IsHandleCreated ||
            form.ClientSize.Width <= 0 || form.ClientSize.Height <= 0)
        {
            graphics.Clear(Palette.AppBg);
            return;
        }

        var size = form.ClientSize;
        var origin = control.PointToScreen(Point.Empty);
        var formOrigin = form.PointToScreen(Point.Empty);

        var state = graphics.Save();
        graphics.TranslateTransform(formOrigin.X - origin.X, formOrigin.Y - origin.Y);
        using (var brush = CreateBrush(new Rectangle(Point.Empty, size)))
        {
            graphics.FillRectangle(brush, 0, 0, size.Width, size.Height);
        }

        graphics.Restore(state);
    }

    /// <summary>Paints the gradient into one region of a control — an owner-drawn list row, say, where the
    /// caller only owns its own slice of the surface.</summary>
    public static void Paint(Graphics graphics, Control control, Rectangle clip)
    {
        using var previous = graphics.Clip;
        graphics.SetClip(clip);
        Paint(graphics, control);
        graphics.Clip = previous;
    }

    /// <summary>Erases an owner-drawn control's background: the gradient where the control stands on the
    /// app ground, the flat colour where it stands on an opaque panel.</summary>
    public static void Clear(Graphics graphics, Control control, Color background)
    {
        if (background.ToArgb() == Palette.AppBg.ToArgb())
        {
            Paint(graphics, control);
            return;
        }

        graphics.Clear(background);
    }

    private static LinearGradientBrush CreateBrush(Rectangle bounds)
    {
        var brush = new LinearGradientBrush(bounds, Palette.AppBg, Palette.Surface, LinearGradientMode.ForwardDiagonal)
        {
            WrapMode = WrapMode.TileFlipXY,
        };

        var positions = new float[StopCount];
        var colors = new Color[StopCount];
        for (var i = 0; i < StopCount; i++)
        {
            var position = (float)i / (StopCount - 1);
            positions[i] = position;

            // Smoothstep rather than a straight line: it flattens both ends and puts what little range
            // there is (ten 8-bit levels) into the middle, so no single step is ever more than one level.
            var eased = position * position * (3f - (2f * position));
            colors[i] = Blend(Palette.AppBg, Palette.Surface, eased);
        }

        brush.InterpolationColors = new ColorBlend { Positions = positions, Colors = colors };
        return brush;
    }

    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        from.R + (int)Math.Round((to.R - from.R) * amount),
        from.G + (int)Math.Round((to.G - from.G) * amount),
        from.B + (int)Math.Round((to.B - from.B) * amount));
}

/// <summary>A <see cref="TableLayoutPanel"/> that lets the window gradient through instead of filling
/// itself with a flat colour. Used for the containers that make up the app ground; the opaque panels
/// (bottom bar, group panels, grid) keep their own surface and sit on top.</summary>
internal sealed class NeonLayoutPanel : TableLayoutPanel
{
    public NeonLayoutPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Palette.AppBg;
    }

    protected override void OnPaintBackground(PaintEventArgs e) => NeonBackground.Paint(e.Graphics, this);
}
