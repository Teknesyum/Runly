namespace Runly.Settings;

/// <summary>
/// Single source for every size in the settings UI. Nothing here is a taste decision; a raw pixel
/// count in a control is a defect, and this class exists so there is somewhere else to put it.
///
/// Two independent reasons, both already proven on this codebase:
///
/// 1. WinForms does not scale this window. Owner-drawn <c>ItemHeight</c> and the DataGridView row and
///    header heights are documented not to follow the DPI (dotnet/winforms#6382), and the forms here
///    neutralise their own <c>AutoScaleMode</c> as well: assigning <c>AutoScaleDimensions</c> while the
///    form is still empty pins it to the current DPI, so <c>PerformAutoScale</c> ends up with a factor of
///    1 and every control added afterwards keeps its literal size forever. Text is the exception — it is
///    stored in points and rasterised against the device DC, so at 125% and 150% the glyphs grow by half
///    while the boxes around them do not. That is the clipping.
/// 2. The typography base moves in 0.3 (body 13px to 16px). A row written as <c>34</c> survives neither
///    change; a row written as "one line plus 14 design pixels" survives both.
/// </summary>
internal static class Metrics
{
    private const int DesignDpi = 96;

    /// Turkish copy is what has to fit: the dotted capital, the breve and a descender in one sample.
    private const string LineSample = "İğpQy";

    private static readonly Dictionary<Font, int> LineHeights = new();

    private static bool s_initialized;
    private static bool s_dpiKnown;
    private static int s_dpi = DesignDpi;
    private static float s_measurementRatio = 1f;

    private static int s_buttonHeight;
    private static int s_buttonMinHeight;
    private static int s_captionHeight;
    private static int s_sectionLabelHeight;
    private static int s_footerLineHeight;
    private static int s_textBoxHeight;
    private static int s_groupTitleBand;
    private static int s_gridRowHeight;
    private static int s_gridHeaderHeight;
    private static int s_categoryIconSize;
    private static int s_categoryRowHeight;
    private static int s_radioRowHeight;
    private static int s_captionButtonWidth;
    private static int s_captionIconSize;
    private static int s_windowCornerRadius;
    private static int s_resizeBorder;

    /// <summary>Fixes the scale for the whole process. Called once from the main window, before any
    /// control is built; every later call is ignored so a dialog opened on a second monitor cannot
    /// re-derive sizes underneath a layout that was already measured.</summary>
    public static void Initialize(Control probe)
    {
        if (s_initialized)
        {
            return;
        }

        if (probe.DeviceDpi > 0)
        {
            s_dpi = probe.DeviceDpi;
            s_dpiKnown = true;
        }

        Prepare();
    }

    public static int Dpi
    {
        get
        {
            Ensure();
            return s_dpi;
        }
    }

    public static float Scale
    {
        get
        {
            Ensure();
            return (float)s_dpi / DesignDpi;
        }
    }

    /// <summary>A length authored at 96 dpi, in device pixels. Use for pure spacing — gutters, radii,
    /// icon boxes — never for anything sized to hold a line of text.</summary>
    public static int Px(int designPixels)
    {
        Ensure();
        return (int)Math.Round(designPixels * ((float)s_dpi / DesignDpi), MidpointRounding.AwayFromZero);
    }

    /// <summary>Height of one rendered line of <paramref name="font"/>, measured rather than assumed:
    /// <see cref="Font.Height"/> reports the line spacing and misses the Turkish diacritics that actually
    /// get drawn, so the taller of the two wins.</summary>
    public static int Line(Font font)
    {
        Ensure();
        if (LineHeights.TryGetValue(font, out var cached))
        {
            return cached;
        }

        var drawn = TextRenderer.MeasureText(LineSample, font, Size.Empty, TextFormatFlags.NoPadding).Height;
        var height = (int)Math.Ceiling(Math.Max(font.Height, drawn) * s_measurementRatio);
        LineHeights[font] = height;
        return height;
    }

    /// <summary>Height of a control holding a single line, with <paramref name="designPadding"/> split
    /// above and below it.</summary>
    public static int Row(Font font, int designPadding) => Line(font) + Px(designPadding);

    /// <summary>Height of <paramref name="rows"/> identical stacked single-line controls.</summary>
    public static int Stack(Font font, int rows, int designPadding) => rows * Row(font, designPadding);

    /// <summary>Filled or outlined <see cref="NeonButton"/>, and the slot a container must reserve for one.</summary>
    public static int ButtonHeight
    {
        get
        {
            Ensure();
            return s_buttonHeight;
        }
    }

    public static int ButtonMinHeight
    {
        get
        {
            Ensure();
            return s_buttonMinHeight;
        }
    }

    /// <summary>Custom caption band. Sized to the caption glyphs, which are the tallest thing on it.</summary>
    public static int CaptionHeight
    {
        get
        {
            Ensure();
            return s_captionHeight;
        }
    }

    /// <summary>Letter-spaced neon section label plus its margin.</summary>
    public static int SectionLabelHeight
    {
        get
        {
            Ensure();
            return s_sectionLabelHeight;
        }
    }

    /// <summary>The footer strip: label line plus its descender. Smaller clips, larger opens a gap.</summary>
    public static int FooterLineHeight
    {
        get
        {
            Ensure();
            return s_footerLineHeight;
        }
    }

    /// <summary>Single-line <see cref="TextBox"/> including its border.</summary>
    public static int TextBoxHeight
    {
        get
        {
            Ensure();
            return s_textBoxHeight;
        }
    }

    /// <summary>Top padding of <see cref="NeonGroupPanel"/>: the band its title is drawn into.</summary>
    public static int GroupTitleBand
    {
        get
        {
            Ensure();
            return s_groupTitleBand;
        }
    }

    public static int GridRowHeight
    {
        get
        {
            Ensure();
            return s_gridRowHeight;
        }
    }

    public static int GridHeaderHeight
    {
        get
        {
            Ensure();
            return s_gridHeaderHeight;
        }
    }

    /// <summary>Category rail icon. U1 replaces how the bitmap is obtained; the size stays here so the
    /// row height moves with it.</summary>
    public static int CategoryIconSize
    {
        get
        {
            Ensure();
            return s_categoryIconSize;
        }
    }

    public static int CategoryRowHeight
    {
        get
        {
            Ensure();
            return s_categoryRowHeight;
        }
    }

    /// <summary>One owner-drawn radio or check row, margins included.</summary>
    public static int RadioRowHeight
    {
        get
        {
            Ensure();
            return s_radioRowHeight;
        }
    }

    /// <summary>Minimise / maximise / close hit area. Kept well past the 24 DIP floor at every scale.</summary>
    public static int CaptionButtonWidth
    {
        get
        {
            Ensure();
            return s_captionButtonWidth;
        }
    }

    public static int CaptionIconSize
    {
        get
        {
            Ensure();
            return s_captionIconSize;
        }
    }

    public static int WindowCornerRadius
    {
        get
        {
            Ensure();
            return s_windowCornerRadius;
        }
    }

    /// <summary>Grab gutter on the three non-caption edges of a borderless window.</summary>
    public static int ResizeBorder
    {
        get
        {
            Ensure();
            return s_resizeBorder;
        }
    }

    private static void Ensure()
    {
        if (!s_initialized)
        {
            Prepare();
        }
    }

    private static void Prepare()
    {
        s_initialized = true;

        // TextRenderer measures against the desktop DC. When that DC reports a different resolution than
        // the window we are laying out, every measurement is off by the ratio between them — correcting
        // once here keeps it out of every call site.
        using (var screen = Graphics.FromHwnd(nint.Zero))
        {
            var measurementDpi = screen.DpiY > 0 ? screen.DpiY : DesignDpi;
            if (!s_dpiKnown)
            {
                // Reached when a message box is raised before the main window exists. The desktop DC is
                // the only resolution available at that point.
                s_dpi = (int)Math.Round(measurementDpi, MidpointRounding.AwayFromZero);
                s_dpiKnown = true;
            }

            s_measurementRatio = s_dpi / measurementDpi;
        }

        LineHeights.Clear();

        s_buttonHeight = Row(Palette.Body, 13);
        s_buttonMinHeight = Row(Palette.Body, 11);
        s_captionHeight = Math.Max(Line(Palette.Body), Line(Palette.CaptionGlyph)) + Px(14);
        s_sectionLabelHeight = Row(Palette.LabelFont, 8);
        s_footerLineHeight = Row(Palette.LabelFont, 4);
        s_textBoxHeight = Row(Palette.MonoBody, 13);
        s_groupTitleBand = Row(Palette.H3, 15);
        s_gridRowHeight = Row(Palette.MonoBody, 9);
        s_gridHeaderHeight = Row(Palette.H3, 11);
        s_categoryIconSize = Px(20);
        s_categoryRowHeight = Math.Max(s_categoryIconSize, Line(Palette.Body)) + Px(14);
        s_radioRowHeight = Row(Palette.Body, 13);
        s_captionButtonWidth = Px(52);
        s_captionIconSize = Px(16);
        s_windowCornerRadius = Px(12);
        s_resizeBorder = Px(7);
    }
}
