using System.Drawing.Drawing2D;

namespace Runly.Settings;

internal sealed class BindingProgressRing : Control
{
    private int _bound;
    private int _total;

    private const int RingGrid = 58;

    public BindingProgressRing()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Top;
        Font = Palette.MonoBody;
        Height = Metrics.Px(RingGrid) + Metrics.Px(24);
    }

    public void SetProgress(int bound, int total)
    {
        _bound = Math.Max(0, bound);
        _total = Math.Max(0, total);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var ring = Metrics.Px(RingGrid);
        var circle = new Rectangle(Metrics.Px(10), Metrics.Px(9), ring, ring);
        var stroke = ring * 7f / RingGrid;
        // The track used to be the field fill, which was the one colour in the palette lighter than the
        // surface; now that fields are surface-coloured it would be invisible. A progress track draws no
        // boundary, so it takes the decorative rung rather than a border weight.
        using var track = new Pen(Color.FromArgb(NeonTheme.DecorativeAlpha, Palette.NeonBlue), stroke);
        using var progress = new Pen(Palette.NeonBlue, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawEllipse(track, circle);
        if (_total > 0) e.Graphics.DrawArc(progress, circle, -90, 360f * _bound / _total);

        var textLeft = circle.Right + Metrics.Px(8);
        var textWidth = Math.Max(0, Width - textLeft - Metrics.Px(6));
        var countLine = Metrics.Line(Font);
        var labelLine = Metrics.Line(Palette.LabelFont);
        var top = (Height - countLine - labelLine - Metrics.Px(4)) / 2;
        TextRenderer.DrawText(e.Graphics, $"{_bound}/{_total}", Font,
            new Rectangle(textLeft, top, textWidth, countLine), Palette.TextBody);
        TextRenderer.DrawText(e.Graphics, Strings.Get("binding.progress"), Palette.LabelFont,
            new Rectangle(textLeft, top + countLine + Metrics.Px(4), textWidth, labelLine), Palette.TextHint);
    }
}
