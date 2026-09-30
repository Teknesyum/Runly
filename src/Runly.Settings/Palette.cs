using System.Drawing.Text;
using System.Runtime.InteropServices;
using Runly.Core.Theme;

namespace Runly.Settings;

/// Teknesyum Neon — WinForms paleti.
///
/// R5 bu dosyayı "değerleri değiştirme" mührüyle sabitlemişti; o mühür R5'in kararıydı, engel değil.
/// R6 mührü açıp değerleri teknesyum-ui 2026-08 standardına çekti: ara gri yok (hiyerarşi boyut,
/// ağırlık ve neon renkle kurulur), zemin nötr siyah. Tek gri devre dışı kontrol içindir.
/// Yığın 1 (2026-08-25) hex sabitlerini <see cref="TeknesyumTokens"/>'a taşıdı — değerler artık
/// Core'da tek yerde duruyor, launcher aynı dosyadan okuyacak — ve ölçeği standardın beş basamağına
/// (14/16/20/24/30 px) çekti, pembe/mor metin rollerini ayırdı, uyarı rengini ekledi.
/// Ölçüler buradan Metrics'e, oradan bütün pencereye akar — puntoyu değiştirmek satır
/// yüksekliklerini de taşır.
internal static class Palette
{
    public static readonly Color Renk1 = ColorTranslator.FromHtml(TeknesyumTokens.Renk1);
    public static readonly Color Renk2 = ColorTranslator.FromHtml(TeknesyumTokens.Renk2);
    public static readonly Color Renk3 = ColorTranslator.FromHtml(TeknesyumTokens.Renk3);
    public static readonly Color Success = ColorTranslator.FromHtml(TeknesyumTokens.Success);

    /// <summary>Pembenin metin rolü. Dolgu hex'i siyah üstünde 6.44:1 verir, yani okunan hiçbir şeyde
    /// kullanılamaz; bu 7.72:1.</summary>
    public static readonly Color Renk2Text = ColorTranslator.FromHtml(TeknesyumTokens.Renk2Text);

    /// <summary>Morun metin rolü. Dolgu hex'i 4.57:1, bu 7.83:1.</summary>
    public static readonly Color Renk3Text = ColorTranslator.FromHtml(TeknesyumTokens.Renk3Text);

    /// <summary>Yalnız uyarı yüzeyi: metin, çerçeve, ikon. Dolgu ve buton yok — amber dolgu üstünde
    /// beyaz metin 1.67:1'e düşer.</summary>
    public static readonly Color Warning = ColorTranslator.FromHtml(TeknesyumTokens.Warning);

    public static readonly Color Surface = ColorTranslator.FromHtml(TeknesyumTokens.Surface);
    public static readonly Color AppBg = ColorTranslator.FromHtml(TeknesyumTokens.Bg);

    /// <summary>Giriş alanı zemini. Standartta ayrı bir "field" tokenı yok: alan zeminden dolgusuyla
    /// değil çerçevesiyle ayrılır, o yüzden yüzey rengiyle aynıdır.</summary>
    public static readonly Color FieldBg = Surface;

    /// <summary>Seçili satır ve seçili liste öğesi: renk-1 /30, opak karıştırılmış.</summary>
    public static readonly Color SelectedFill = ColorTranslator.FromHtml(TeknesyumTokens.SelectedFill);

    /// <summary>Izgara ayraç çizgisi: renk-1 /10, opak karıştırılmış. Dekoratif — eşik yok.</summary>
    public static readonly Color GridLine = ColorTranslator.FromHtml(TeknesyumTokens.GridLine);

    // Everything meant to be read is pure white. The old ramp (#D1D5DB → #4B5563) dimmed secondary
    // text until it stopped being readable on black and called the result hierarchy; hierarchy comes
    // from size, weight and the neon accents instead. TextDim and TextHint survive as names so call sites
    // did not all have to change at once, but both are white — reach for Disabled only when a control
    // really is disabled.
    public static readonly Color TextStrong = ColorTranslator.FromHtml(TeknesyumTokens.Text);
    public static readonly Color TextBody = TextStrong;
    public static readonly Color TextDim = TextStrong;
    public static readonly Color TextHint = TextStrong;

    /// <summary>Labels and section headings: bold, tracked, neon — never a dimmed grey.</summary>
    public static readonly Color TextLabel = Renk1;

    /// <summary>The one grey in the theme. Placeholder and genuinely inactive content only.</summary>
    public static readonly Color Disabled = ColorTranslator.FromHtml(TeknesyumTokens.Disabled);

    public const string GitHubUrl = "https://github.com/Teknesyum";
    public const string SponsorUrl = "https://github.com/sponsors/Teknesyum";

    public const string ReadmeUrlEn = "https://github.com/Teknesyum/Runly#readme";

    public const string ReadmeUrlTr = "https://github.com/Teknesyum/Runly/blob/main/README.tr.md";

    // ---- Font chain ----------------------------------------------------------------------------
    // The standard names one chain for every stack: sans 'Atkinson Hyperlegible Next' → 'Segoe UI',
    // mono 'Cascadia Mono' → 'Consolas'. The head of the chain is meant to be *embedded*, not assumed
    // installed, so it is looked for in this assembly's resources first. No .ttf is checked in yet —
    // adding one as an EmbeddedResource is the whole job, nothing here changes — and until then the
    // chain falls through to Segoe UI, which the standard counts as an incomplete delivery.
    private static readonly PrivateFontCollection s_embedded = LoadEmbeddedFonts();

    private static readonly InstalledFontCollection s_installed = new();

    public static readonly FontFamily SansFamily = ResolveFamily(TeknesyumTokens.SansChain);
    public static readonly FontFamily MonoFamily = ResolveFamily(TeknesyumTokens.MonoChain);

    // The token type scale is in design pixels; at 96 dpi a point is 4/3 of a pixel. WinForms has no 600
    // weight and no letter spacing, so headings stay Bold and the hierarchy is carried by size.
    private static float Pt(int designPixels) => designPixels * 0.75f * UiScale.Factor;

    public static readonly Font H2 = new(SansFamily, Pt(TeknesyumTokens.Fs4), FontStyle.Bold);
    public static readonly Font H3 = new(SansFamily, Pt(TeknesyumTokens.Fs3), FontStyle.Bold);
    public static readonly Font LabelFont = new(SansFamily, Pt(TeknesyumTokens.Fs1), FontStyle.Bold);
    public static readonly Font Body = new(SansFamily, Pt(TeknesyumTokens.Fs2));
    public static readonly Font Help = new(SansFamily, Pt(TeknesyumTokens.Fs1));
    public static readonly Font Mono = new(MonoFamily, Pt(TeknesyumTokens.Fs2), FontStyle.Bold);
    public static readonly Font MonoBody = new(MonoFamily, Pt(TeknesyumTokens.Fs1));
    public static readonly Font Hero = new(MonoFamily, Pt(TeknesyumTokens.Fs5), FontStyle.Bold);
    /// Caption glyphs are strokes, not letters: below ~12pt they anti-alias into grey mush.
    public static readonly Font CaptionGlyph = new(SansFamily, Pt(TeknesyumTokens.Fs2));

    private static PrivateFontCollection LoadEmbeddedFonts()
    {
        var collection = new PrivateFontCollection();
        var assembly = typeof(Palette).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);

            // AddMemoryFont keeps reading from this block for the lifetime of the collection, so the
            // handle is deliberately never freed: the collection lives as long as the process.
            var block = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, block, bytes.Length);
            collection.AddMemoryFont(block, bytes.Length);
        }

        return collection;
    }

    /// <summary>Resolves a family by name, embedded copies first. A private font cannot be reached by
    /// name through <c>new Font(string, ...)</c> — GDI+ silently substitutes — so the family object
    /// itself is what gets handed to every <see cref="Font"/> below.</summary>
    private static FontFamily ResolveFamily(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            foreach (var family in s_embedded.Families)
            {
                if (string.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return family;
                }
            }

            foreach (var family in s_installed.Families)
            {
                if (string.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return family;
                }
            }
        }

        // Every candidate list ends in a font Windows always ships (Segoe UI / Consolas), so this is
        // only reached if the enumeration itself failed — GenericSansSerif always resolves to something.
        return FontFamily.GenericSansSerif;
    }
}
