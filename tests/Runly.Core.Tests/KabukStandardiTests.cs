using System.Text.RegularExpressions;
using Xunit;

namespace Runly.Core.Tests;

public class KabukStandardiTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Runly.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Runly.sln bulunamadı");
    }

    private static IEnumerable<string> Sources(string project) =>
        Directory.EnumerateFiles(Path.Combine(Root, "src", project), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static List<string> Violations(IEnumerable<string> files, Regex pattern) =>
        files.SelectMany(f => File.ReadAllLines(f)
                .Select((line, i) => (line, i))
                .Where(x => !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal) && pattern.IsMatch(x.line))
                .Select(x => $"{Path.GetFileName(f)}:{x.i + 1}: {x.line.Trim()}"))
            .ToList();

    [Fact]
    public void RenkYalnizPaletten()
    {
        var raw = new Regex(@"Color\.FromArgb\(\s*(0x[0-9A-Fa-f]+|\d+)\s*,\s*(0x[0-9A-Fa-f]+|\d+)\s*,\s*(0x[0-9A-Fa-f]+|\d+)\s*[,)]|ColorTranslator\.|Color\.(?!FromArgb|Transparent|Empty)[A-Z][a-z]+[A-Za-z]*\b(?!\s*\()|SystemColors\.");
        var files = Sources("Runly.Settings").Where(f => Path.GetFileName(f) != "Palette.cs");
        Assert.Empty(Violations(files, raw));
    }

    [Fact]
    public void BaslaticiRenkleriTekYerde()
    {
        var colorref = new Regex(@"\bColor\w*\s*=\s*0x[0-9A-Fa-f]{6}\b|CreateSolidBrush\(\s*0x");
        var files = Sources("Runly.Launcher").Where(f => Path.GetFileName(f) != "NeonWindowChrome.cs");
        Assert.Empty(Violations(files, colorref));
    }

    [Fact]
    public void InfoRengiYoktur()
    {
        var info = new Regex(@"\bInfo\w*\s*(=|\{)");
        var files = new[]
        {
            Path.Combine(Root, "src", "Runly.Settings", "Palette.cs"),
        };
        Assert.Empty(Violations(files, info));
        Assert.DoesNotContain("\"info\"", File.ReadAllText(Path.Combine(Root, "teknesyum-ui", "theme.tokens.json")));
    }

    [Fact]
    public void EtiketDosyalariAnaDerlemeyeGomulur()
    {
        var project = File.ReadAllLines(Path.Combine(Root, "src", "Runly.Settings", "Runly.Settings.csproj"));
        foreach (var code in new[] { "tr", "en" })
        {
            var line = Assert.Single(project, l => l.Contains($"Runly.Settings.labels.{code}.json"));
            Assert.Contains("WithCulture=\"false\"", line);
        }
    }

    [Fact]
    public void YerTutucuMetinYok()
    {
        var cue = new Regex(@"PlaceholderText|CUEBANNER|0x1501\b");
        var files = Sources("Runly.Settings").Concat(Sources("Runly.Launcher"));
        Assert.Empty(Violations(files, cue));
    }

    [Fact]
    public void KendiBaslikCubugu()
    {
        var form = File.ReadAllText(Path.Combine(Root, "src", "Runly.Settings", "NeonForm.cs"));
        Assert.Contains("FormBorderStyle = FormBorderStyle.None", form);
        Assert.Contains("WsThickFrame", form);
        Assert.Contains("WmNcHitTest", form);
        Assert.Contains("HtCaption", form);
        Assert.Contains("HtLeft", form);
        Assert.Matches(@"WindowState\s*=\s*WindowState\s*==\s*FormWindowState\.Maximized", form);
    }

    [Fact]
    public void GorselTemaKitapligiYok()
    {
        var theme = new Regex(@"PackageReference\s+Include=""(MaterialSkin|MetroFramework|MetroModernUI|Krypton|Guna|ReaLTaiizor|DarkUI|Bunifu|SunnyUI|AntdUI|CyberColorful|MahApps|HandyControl|WPF-UI|Wpf\.Ui)", RegexOptions.IgnoreCase);
        var projects = Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories);
        Assert.Empty(Violations(projects, theme));
    }
}
