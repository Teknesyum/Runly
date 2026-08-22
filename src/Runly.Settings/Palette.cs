using System.Drawing.Text;

namespace Runly.Settings;

/// Teknesyum Neon — WinForms paleti.
///
/// R5 bu dosyayı "değerleri değiştirme" mührüyle sabitlemişti; o mühür R5'in kararıydı, engel değil.
/// R6 mührü açıp değerleri teknesyum-ui 2026-08 standardına çekti: ölçek 14/16/20/28 piksel, ara gri
/// yok (hiyerarşi boyut, ağırlık ve neon renkle kurulur), zemin nötr siyah. Tek gri devre dışı
/// kontrol içindir. Ölçüler buradan Metrics'e, oradan bütün pencereye akar — puntoyu değiştirmek
/// satır yüksekliklerini de taşır.
internal static class Palette
{
    public static readonly Color NeonBlue = ColorTranslator.FromHtml("#00F3FF");
    public static readonly Color NeonPink = ColorTranslator.FromHtml("#FF00EA");
    public static readonly Color NeonPurple = ColorTranslator.FromHtml("#B026FF");
    public static readonly Color Success = ColorTranslator.FromHtml("#34D399");

    public static readonly Color Surface = ColorTranslator.FromHtml("#0A0A0C");
    public static readonly Color AppBg = ColorTranslator.FromHtml("#000000");
    public static readonly Color FieldBg = ColorTranslator.FromHtml("#101214");

    // Everything meant to be read is pure white. The old ramp (#D1D5DB → #4B5563) dimmed secondary
    // text until it stopped being readable on black and called the result hierarchy; hierarchy comes
    // from size, weight and the neon accents instead. TextDim survives as a name so call sites did not
    // all have to change at once, but it is white — reach for Disabled only when a control really is.
    public static readonly Color TextStrong = ColorTranslator.FromHtml("#FFFFFF");
    public static readonly Color TextBody = ColorTranslator.FromHtml("#FFFFFF");
    public static readonly Color TextDim = ColorTranslator.FromHtml("#FFFFFF");

    /// <summary>Labels and section headings: bold, tracked, neon — never a dimmed grey.</summary>
    public static readonly Color TextLabel = NeonBlue;

    /// <summary>The one grey in the theme. Placeholder and genuinely inactive content only.</summary>
    public static readonly Color Disabled = ColorTranslator.FromHtml("#71717A");

    public static readonly Color TextHint = Disabled;

    public const string GitHubUrl = "https://github.com/Teknesyum";
    public const string SponsorUrl = "https://github.com/sponsors/Teknesyum";

    // ---- Font fallback chain (R5: font kurma/yükleme yasak; Inter ve JetBrains Mono bu makinede yok) ----
    private static readonly InstalledFontCollection s_installed = new();

    public static readonly string SansFamily = ResolveFamily("Inter", "Segoe UI");
    public static readonly string MonoFamily = ResolveFamily("JetBrains Mono", "Cascadia Mono", "Consolas");

    // Scale is 14 / 16 / 20 / 28 design pixels; at 96 dpi a point is 4/3 of a pixel, so the sizes
    // below are that scale in points. Nothing sits between two steps and nothing drops under 14px —
    // the old 7.5pt label was 10px and unreadable on a dark background.
    public static readonly Font H2 = new(SansFamily, 15f, FontStyle.Bold);
    public static readonly Font H3 = new(SansFamily, 12f, FontStyle.Bold);
    public static readonly Font LabelFont = new(SansFamily, 10.5f, FontStyle.Bold);
    public static readonly Font Body = new(SansFamily, 12f);
    public static readonly Font Help = new(SansFamily, 10.5f);
    public static readonly Font Mono = new(MonoFamily, 12f, FontStyle.Bold);
    public static readonly Font MonoBody = new(MonoFamily, 11f);
    public static readonly Font Hero = new(MonoFamily, 21f, FontStyle.Bold);
    /// Caption glyphs are strokes, not letters: below ~12pt they anti-alias into grey mush.
    public static readonly Font CaptionGlyph = new(SansFamily, 12f);

    private static string ResolveFamily(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            foreach (var family in s_installed.Families)
            {
                if (string.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        // Every candidate list ends in a font Windows always ships (Segoe UI / Consolas), so this is
        // only reached if InstalledFontCollection itself failed to enumerate — fall back to the last entry.
        return candidates[^1];
    }
}
