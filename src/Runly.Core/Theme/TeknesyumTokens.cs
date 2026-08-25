namespace Runly.Core.Theme;

/// <summary>
/// The Teknesyum Neon palette, as plain hex strings.
///
/// <para>The values used to live twice — <c>Runly.Settings/Palette.cs</c> and
/// <c>Runly.Launcher/Ui/NeonWindowChrome.cs</c> — and had already drifted apart: the panel surface was
/// <c>#0A0A0C</c> in one and <c>#08090A</c> in the other. They are strings rather than colours because
/// this assembly is AOT-compatible and cannot take a <c>System.Drawing</c> reference; each front end
/// converts once, WinForms through <c>ColorTranslator</c> and the launcher through COLORREF.</para>
///
/// <para>Pink and purple carry two hexes each on purpose. The fill hex is the brand identity; the text
/// hex is the same OKLCH hue lightened until it clears the 7:1 text floor (measured hue drift: 0.06° for
/// pink, 0.11° for purple). Text never uses the fill hex.</para>
/// </summary>
public static class TeknesyumTokens
{
    /// <summary>Primary accent: action, active state, numeric emphasis, headings. 15.26:1 on <see cref="Bg"/>.</summary>
    public const string NeonBlue = "#00F3FF";

    /// <summary>Secondary fill, border and state. 6.44:1 — a fill, never text; see <see cref="PinkText"/>.</summary>
    public const string NeonPink = "#FF00EA";

    /// <summary>Tertiary fill, border and state. 4.57:1 — a fill, never text; see <see cref="PurpleText"/>.</summary>
    public const string NeonPurple = "#B026FF";

    /// <summary>The text role of pink: mono values, critical numbers, destructive labels. 7.72:1.</summary>
    public const string PinkText = "#FF54EB";

    /// <summary>The text role of purple: secondary links, ghost button captions. 7.83:1.</summary>
    public const string PurpleText = "#C67EFF";

    /// <summary>Application ground. Neutral, fully black — one gradient runs from here to <see cref="Surface"/>.</summary>
    public const string Bg = "#000000";

    /// <summary>Panel ground. Separated from <see cref="Bg"/> by its border, not by its fill: the two differ by 1.06:1.</summary>
    public const string Surface = "#08090A";

    /// <summary>Everything meant to be read: body, headings, table values, label text.</summary>
    public const string Text = "#FFFFFF";

    /// <summary>Labels and section headings. Follows <see cref="NeonBlue"/>; it is a role name, not a second value.</summary>
    public const string Label = NeonBlue;

    /// <summary>Error and destructive fills. Follows <see cref="NeonPink"/>.</summary>
    public const string Danger = NeonPink;

    /// <summary>Error text. Follows <see cref="PinkText"/> — the fill hex does not reach 7:1 as text.</summary>
    public const string DangerText = PinkText;

    /// <summary>Warning surface only: text, border, icon. No fill and no button — white on amber is 1.67:1.</summary>
    public const string Warning = "#FBBF24";

    /// <summary>Completion, and nothing else.</summary>
    public const string Success = "#34D399";

    /// <summary>The single grey. Disabled controls only, and always paired with a tooltip saying why.</summary>
    public const string Disabled = "#71717A";

    /// <summary>Active/selected fill: <see cref="NeonBlue"/> at 30% composited over <see cref="Bg"/>.
    /// Pre-composited because <c>DataGridView</c> cell styles ignore the alpha channel.</summary>
    public const string SelectedFill = "#00494D";

    /// <summary>Decorative hairline between rows: <see cref="NeonBlue"/> at 10% over <see cref="Bg"/>.
    /// Pre-composited because <c>DataGridView.GridColor</c> rejects alpha.</summary>
    public const string GridLine = "#00181A";
}
