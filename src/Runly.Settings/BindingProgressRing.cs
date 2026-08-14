using System.Drawing.Drawing2D;

namespace Runly.Settings;

internal sealed class BindingProgressRing : Control
{
    private int _bound;
    private int _total;

    public BindingProgressRing()
    {
        DoubleBuffered = true;
        Height = 82;
        Dock = DockStyle.Top;
        Font = Palette.MonoBody;
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
        var circle = new Rectangle(10, 9, 58, 58);
        using var track = new Pen(Palette.FieldBg, 7);
        using var progress = new Pen(Palette.NeonBlue, 7) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawEllipse(track, circle);
        if (_total > 0) e.Graphics.DrawArc(progress, circle, -90, 360f * _bound / _total);
        TextRenderer.DrawText(e.Graphics, $"{_bound}/{_total}", Font, new Rectangle(76, 19, Width - 82, 24), Palette.TextBody);
        TextRenderer.DrawText(e.Graphics, Strings.Get("binding.progress"), Palette.LabelFont, new Rectangle(76, 43, Width - 82, 20), Palette.TextHint);
    }
}
