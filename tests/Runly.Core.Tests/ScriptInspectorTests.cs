using System.Text;
using Runly.Core.Abstractions;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>Covers shebang variants (plain, env-wrapped, Windows separators), BOM handling, empty files and the hash size cap.</summary>
public sealed class ScriptInspectorTests : IDisposable
{
    private readonly string _tempDir;

    public ScriptInspectorTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("runly-inspector-tests-").FullName;
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private sealed class NoMotwService : IMotwService
    {
        public bool HasMotw(string path) => false;
        public int? GetZoneId(string path) => null;
        public void Strip(string path) { }
    }

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string WriteFile(string name, string content) => WriteFile(name, Encoding.UTF8.GetBytes(content));

    [Fact]
    public void Inspect_MissingFile_Throws()
    {
        var inspector = new ScriptInspector(new NoMotwService());

        Assert.Throws<FileNotFoundException>(() => inspector.Inspect(Path.Combine(_tempDir, "missing.js")));
    }

    [Fact]
    public void Inspect_PlainShebang_ParsesInterpreterName()
    {
        var path = WriteFile("a.py", "#!/usr/bin/python3\nprint('hi')\n");
        var inspector = new ScriptInspector(new NoMotwService());

        var info = inspector.Inspect(path);

        Assert.Equal("/usr/bin/python3", info.Shebang);
        Assert.Equal("python3", info.ShebangInterpreter);
    }

    [Fact]
    public void Inspect_EnvWrappedShebang_ParsesRealInterpreterAfterEnv()
    {
        var path = WriteFile("a.js", "#!/usr/bin/env node -X\nconsole.log(1)\n");
        var inspector = new ScriptInspector(new NoMotwService());

        var info = inspector.Inspect(path);

        Assert.Equal("node", info.ShebangInterpreter);
    }

    [Fact]
    public void Inspect_WindowsStyleShebangPath_StillParsesInterpreterName()
    {
        var path = WriteFile("a.txt", "#!C:\\Python\\python.exe\nprint(1)\n");
        var inspector = new ScriptInspector(new NoMotwService());

        var info = inspector.Inspect(path);

        Assert.Equal("python.exe", info.ShebangInterpreter);
    }

    [Fact]
    public void Inspect_NoShebang_LeavesShebangFieldsNull()
    {
        var path = WriteFile("a.js", "console.log(1)\n");
        var inspector = new ScriptInspector(new NoMotwService());

        var info = inspector.Inspect(path);

        Assert.Null(info.Shebang);
        Assert.Null(info.ShebangInterpreter);
    }

    [Fact]
    public void Inspect_Utf8Bom_IsStrippedFromFirstLine()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = bom.Concat(Encoding.UTF8.GetBytes("#!/usr/bin/env node\nhello\n")).ToArray();
        var path = WriteFile("a.js", content);
        var inspector = new ScriptInspector(new NoMotwService());

        var info = inspector.Inspect(path);

        Assert.Equal("node", info.ShebangInterpreter);
        Assert.StartsWith("#!", info.FirstLines[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("#!/usr/bin/env -S python -u\n", "python")]
    [InlineData("#!/usr/bin/env\tnode\n", "node")]
    [InlineData("#!/usr/bin/env --split-string python3 -u\n", "python3")]
    public void Inspect_EnvShebangOptionsAndWhitespace_ResolveActualInterpreter(string content, string expected)
    {
        var path = WriteFile("portable-script", content);

        var info = new ScriptInspector(new NoMotwService()).Inspect(path);

        Assert.Equal(expected, info.ShebangInterpreter);
    }

    [Fact]
    public void Inspect_EmptyFile_ReturnsEmptyLinesAndValidHash()
    {
        var path = WriteFile("empty.js", []);
        var inspector = new ScriptInspector(new NoMotwService());

        var info = inspector.Inspect(path);

        Assert.Empty(info.FirstLines);
        Assert.Equal(0, info.SizeBytes);
        Assert.NotNull(info.Sha256);
        // SHA-256 of the empty string, the well-known constant.
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", info.Sha256);
    }

    [Fact]
    public void Inspect_MoreThan100Lines_CapsFirstLinesAt100()
    {
        var content = string.Concat(Enumerable.Range(0, 150).Select(i => $"line{i}\n"));
        var path = WriteFile("many.js", content);
        var inspector = new ScriptInspector(new NoMotwService());

        var info = inspector.Inspect(path);

        Assert.Equal(100, info.FirstLines.Count);
        Assert.Equal("line0", info.FirstLines[0]);
    }

    [Fact]
    public void Inspect_FileLargerThanHashLimit_SkipsHashing()
    {
        var path = Path.Combine(_tempDir, "huge.js");
        using (var stream = File.Create(path))
        {
            stream.SetLength(ScriptInspector.HashSizeLimitBytes + 1);
        }

        var inspector = new ScriptInspector(new NoMotwService());
        var info = inspector.Inspect(path);

        Assert.Null(info.Sha256);
    }

    [Fact]
    public void Inspect_ReadsSizeAndExtension()
    {
        var path = WriteFile("script.PS1", "Write-Host 'hi'\n");
        var inspector = new ScriptInspector(new NoMotwService());

        var info = inspector.Inspect(path);

        Assert.Equal(".ps1", info.Extension);
        Assert.True(info.SizeBytes > 0);
    }
}
