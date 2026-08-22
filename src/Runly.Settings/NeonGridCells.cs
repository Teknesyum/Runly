using System.Drawing.Drawing2D;

namespace Runly.Settings;

/// <summary>A cell whose whole job is to flip between two states, however the flip is triggered.</summary>
internal interface INeonToggleCell
{
    void Toggle();
}

/// <summary>
/// Base for the owner-drawn grid cells.
///
/// <para><see cref="DataGridViewCheckBoxColumn"/> and <see cref="DataGridViewComboBoxColumn"/> both hand
/// their glyph to the system renderer, which knows nothing about this theme: the check box comes back as a
/// white square and the combo cell as a white field with a grey drop button, on every one of the four
/// hundred rows. Neither has a property that changes it, so the glyph is drawn here instead.</para>
///
/// <para>Editing is suppressed through a null <see cref="EditType"/> rather than through
/// <see cref="DataGridViewCell.ReadOnly"/>, because ReadOnly is already carrying a different meaning on
/// this grid: rows the catalog blocks.</para>
/// </summary>
internal abstract class NeonToggleCell : DataGridViewTextBoxCell, INeonToggleCell
{
    private const DataGridViewPaintParts SurfaceParts =
        DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border;

    private bool _hover;
    private bool _pressed;

    public override Type? EditType => null;

    protected bool Hover => _hover;

    protected bool Pressed => _pressed;

    protected bool Focused =>
        DataGridView is { Focused: true } grid &&
        grid.CurrentCellAddress.X == ColumnIndex &&
        grid.CurrentCellAddress.Y == RowIndex;

    public void Toggle()
    {
        if (ReadOnly || DataGridView is null || RowIndex < 0)
        {
            return;
        }

        Value = Value is not true;
    }

    protected static Color Accent(bool on) => on ? Palette.NeonPurple : Palette.NeonBlue;

    protected int FillAlpha() => _pressed ? NeonTheme.PressedAlpha : _hover ? NeonTheme.HoverAlpha : NeonTheme.FillAlpha;

    protected abstract void PaintGlyph(Graphics graphics, Rectangle cellBounds, bool on, DataGridViewCellStyle cellStyle);

    protected override void Paint(
        Graphics graphics,
        Rectangle clipBounds,
        Rectangle cellBounds,
        int rowIndex,
        DataGridViewElementStates cellState,
        object? value,
        object? formattedValue,
        string? errorText,
        DataGridViewCellStyle cellStyle,
        DataGridViewAdvancedBorderStyle advancedBorderStyle,
        DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText,
            cellStyle, advancedBorderStyle, paintParts & SurfaceParts);

        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0 || cellBounds.Width <= 0 || cellBounds.Height <= 0)
        {
            return;
        }

        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        PaintGlyph(graphics, cellBounds, value is true, cellStyle);
        graphics.SmoothingMode = previous;
    }

    /// <summary>Focus has to be visible on its own, not inferred from the full-row selection: the row
    /// highlight says which extension is selected, this says which cell Space and Enter will flip.</summary>
    protected void PaintFocusRing(Graphics graphics, Rectangle glyph, int radius)
    {
        if (!Focused)
        {
            return;
        }

        var stroke = Math.Max(1, Metrics.Px(2));
        var ring = Rectangle.Inflate(glyph, stroke, stroke);
        using var path = NeonTheme.RoundedRect(ring, radius + stroke);
        using var pen = new Pen(Palette.NeonPink, stroke);
        graphics.DrawPath(pen, path);
    }

    protected override void OnMouseEnter(int rowIndex)
    {
        _hover = true;
        Repaint(rowIndex);
    }

    protected override void OnMouseLeave(int rowIndex)
    {
        _hover = false;
        _pressed = false;
        Repaint(rowIndex);
    }

    protected override void OnMouseDown(DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || ReadOnly)
        {
            return;
        }

        _pressed = true;
        Repaint(e.RowIndex);
    }

    protected override void OnMouseUp(DataGridViewCellMouseEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        Repaint(e.RowIndex);
    }

    protected override void OnMouseClick(DataGridViewCellMouseEventArgs e)
    {
        // A double click sends a second click with Clicks == 2; without this the pair cancels out and the
        // row is silently marked dirty for nothing.
        if (e.Button != MouseButtons.Left || e.Clicks > 1)
        {
            return;
        }

        Toggle();
    }

    private void Repaint(int rowIndex)
    {
        if (DataGridView is null || rowIndex < 0 || ColumnIndex < 0)
        {
            return;
        }

        DataGridView.InvalidateCell(ColumnIndex, rowIndex);
    }
}

/// <summary>Owner-drawn replacement for <see cref="DataGridViewCheckBoxCell"/>.</summary>
internal sealed class NeonCheckCell : NeonToggleCell
{
    protected override void PaintGlyph(Graphics graphics, Rectangle cellBounds, bool on, DataGridViewCellStyle cellStyle)
    {
        var side = Math.Min(Metrics.Px(20), Math.Min(cellBounds.Width, cellBounds.Height) - Metrics.Px(4));
        if (side <= 0)
        {
            return;
        }

        var box = new Rectangle(
            cellBounds.X + ((cellBounds.Width - side) / 2),
            cellBounds.Y + ((cellBounds.Height - side) / 2),
            side,
            side);

        if (ReadOnly)
        {
            NeonTheme.DrawCheckGlyph(graphics, box, on, NeonTheme.DisabledOutline, NeonTheme.DisabledOutline);
            return;
        }

        if (!on)
        {
            using var hover = new SolidBrush(Color.FromArgb(FillAlpha(), Palette.NeonBlue));
            using var path = NeonTheme.RoundedRect(box, Metrics.Px(3));
            graphics.FillPath(hover, path);
        }

        NeonTheme.DrawCheckGlyph(graphics, box, on, Palette.NeonBlue, on ? Palette.NeonBlue : NeonTheme.IdleOutline);
        PaintFocusRing(graphics, box, Metrics.Px(3));
    }
}

/// <summary>Owner-drawn two-state chip. Replaces a <see cref="DataGridViewComboBoxCell"/> whose entire
/// value set is two items: a drop-down asks for two clicks and a system-drawn arrow to choose between
/// them, where one click on the chip does the same thing.</summary>
internal sealed class NeonChipCell : NeonToggleCell
{
    private static int Radius => Metrics.Px(6);

    protected override void PaintGlyph(Graphics graphics, Rectangle cellBounds, bool on, DataGridViewCellStyle cellStyle)
    {
        if (OwningColumn is not NeonChipColumn column)
        {
            return;
        }

        var font = cellStyle.Font ?? DataGridView?.Font ?? Palette.MonoBody;
        var text = Strings.Get(on ? column.OnTextKey : column.OffTextKey);
        var textSize = TextRenderer.MeasureText(graphics, text, font, Size.Empty, TextFormatFlags.NoPadding);

        var height = Math.Min(cellBounds.Height - Metrics.Px(6), Metrics.Line(font) + Metrics.Px(8));
        var width = Math.Min(cellBounds.Width - Metrics.Px(8), textSize.Width + Metrics.Px(16));
        if (height <= 0 || width <= 0)
        {
            return;
        }

        var chip = new Rectangle(
            cellBounds.X + ((cellBounds.Width - width) / 2),
            cellBounds.Y + ((cellBounds.Height - height) / 2),
            width,
            height);

        var accent = ReadOnly ? NeonTheme.DisabledOutline : Accent(on);
        using var path = NeonTheme.RoundedRect(chip, Radius);

        if (!ReadOnly)
        {
            using var fill = new SolidBrush(Color.FromArgb(FillAlpha(), accent));
            graphics.FillPath(fill, path);

            // The inner stroke is the inset glow the theme puts on an outlined box; without it the chip
            // reads as a flat outline instead of a lit one.
            var inner = Rectangle.Inflate(chip, -Metrics.Px(1), -Metrics.Px(1));
            if (inner is { Width: > 0, Height: > 0 })
            {
                using var innerPath = NeonTheme.RoundedRect(inner, Radius);
                using var glow = new Pen(Color.FromArgb(NeonTheme.FillAlpha, accent), Metrics.Scale);
                graphics.DrawPath(glow, innerPath);
            }
        }

        using (var border = new Pen(Color.FromArgb(ReadOnly ? NeonTheme.FillAlpha : NeonTheme.OutlineAlpha, accent), Metrics.Scale))
        {
            graphics.DrawPath(border, path);
        }

        TextRenderer.DrawText(graphics, text, font, chip, accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        PaintFocusRing(graphics, chip, Radius);
    }
}

/// <summary>Column of <see cref="NeonCheckCell"/>.</summary>
internal sealed class NeonCheckColumn : DataGridViewColumn
{
    public NeonCheckColumn() : base(new NeonCheckCell())
    {
        ValueType = typeof(bool);
    }
}

/// <summary>Column of <see cref="NeonChipCell"/>. The two captions are held as locale keys, not as text,
/// so a language switch does not have to touch the cells.</summary>
internal sealed class NeonChipColumn : DataGridViewColumn
{
    public NeonChipColumn() : base(new NeonChipCell())
    {
        ValueType = typeof(bool);
    }

    public string OffTextKey { get; set; } = string.Empty;

    public string OnTextKey { get; set; } = string.Empty;

    public override object Clone()
    {
        var copy = (NeonChipColumn)base.Clone();
        copy.OffTextKey = OffTextKey;
        copy.OnTextKey = OnTextKey;
        return copy;
    }
}
