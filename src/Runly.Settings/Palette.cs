using System.Drawing.Text;

namespace Runly.Settings;

/// Teknesyum Neon — WinForms paleti (R5). Değerleri değiştirme.
internal static class Palette
{
    public static readonly Color NeonBlue = ColorTranslator.FromHtml("#00F3FF");
    public static readonly Color NeonPink = ColorTranslator.FromHtml("#FF00EA");
    public static readonly Color NeonPurple = ColorTranslator.FromHtml("#B026FF");
    public static readonly Color Success = ColorTranslator.FromHtml("#34D399");

    public static readonly Color Surface = ColorTranslator.FromHtml("#08090A");
    public static readonly Color AppBg = ColorTranslator.FromHtml("#050507");
    public static readonly Color FieldBg = ColorTranslator.FromHtml("#101214");

    public static readonly Color TextStrong = ColorTranslator.FromHtml("#F3F4F6");
    public static readonly Color TextBody = ColorTranslator.FromHtml("#D1D5DB");
    public static readonly Color TextDim = ColorTranslator.FromHtml("#9CA3AF");
    public static readonly Color TextLabel = ColorTranslator.FromHtml("#6B7280");
    public static readonly Color TextHint = ColorTranslator.FromHtml("#4B5563");

    public const string GitHubUrl = "https://github.com/Teknesyum";
    public const string SponsorUrl = "https://github.com/sponsors/Teknesyum";

    // ---- Font fallback chain (R5: font kurma/yükleme yasak; Inter ve JetBrains Mono bu makinede yok) ----
    private static readonly InstalledFontCollection s_installed = new();

    public static readonly string SansFamily = ResolveFamily("Inter", "Segoe UI");
    public static readonly string MonoFamily = ResolveFamily("JetBrains Mono", "Cascadia Mono", "Consolas");

    public static readonly Font H2 = new(SansFamily, 13.5f, FontStyle.Bold);
    public static readonly Font H3 = new(SansFamily, 10.5f, FontStyle.Bold);
    public static readonly Font LabelFont = new(SansFamily, 7.5f, FontStyle.Bold);
    public static readonly Font Body = new(SansFamily, 10f);
    public static readonly Font Mono = new(MonoFamily, 10f, FontStyle.Bold);
    public static readonly Font MonoBody = new(MonoFamily, 9.5f);
    public static readonly Font Hero = new(MonoFamily, 16f, FontStyle.Bold);
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
