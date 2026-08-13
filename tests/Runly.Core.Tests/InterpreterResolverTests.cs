using Runly.Core.Defaults;
using Runly.Core.Models;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>
/// Covers shebang priority over config, the K4 fallback chain, config-mapping fallback, placeholder
/// expansion for <c>{script}</c>/<c>{args}</c>/<c>{dir}</c>, and Windows argument quoting/escaping.
/// </summary>
public sealed class InterpreterResolverTests
{
    private static ScriptInfo MakeScript(
        string path = @"C:\scripts\run.js",
        string extension = ".js",
        string? shebangInterpreter = null) =>
        new() { Path = path, Extension = extension, ShebangInterpreter = shebangInterpreter };

    [Fact]
    public void Resolve_ShebangPresent_TakesPriorityOverConfigMapping()
    {
        var searcher = new FakePathSearcher()
            .Install("python", @"C:\Python\python.exe")
            .Install("node", @"C:\nodejs\node.exe");
        var resolver = new InterpreterResolver(searcher);
        var script = MakeScript(extension: ".js", shebangInterpreter: "python");
        var config = DefaultConfig.Create();

        var resolved = resolver.Resolve(script, config, []);

        Assert.Equal(InterpreterSource.Shebang, resolved.Source);
        Assert.Equal(@"C:\Python\python.exe", resolved.ExecutablePath);
    }

    [Fact]
    public void Resolve_ShebangPython3NotOnPath_FallsBackThroughPythonThenPy()
    {
        var searcher = new FakePathSearcher().Install("py", @"C:\Python\py.exe");
        var resolver = new InterpreterResolver(searcher);
        var script = MakeScript(shebangInterpreter: "python3");

        var resolved = resolver.Resolve(script, DefaultConfig.Create(), []);

        Assert.Equal(InterpreterSource.Shebang, resolved.Source);
        Assert.Equal(@"C:\Python\py.exe", resolved.ExecutablePath);
    }

    [Fact]
    public void Resolve_ShebangNotResolvable_FallsBackToConfigMapping()
    {
        var searcher = new FakePathSearcher().Install("node", @"C:\nodejs\node.exe");
        var resolver = new InterpreterResolver(searcher);
        // .js is configured for "node", but the shebang says "ruby" which is nowhere on PATH.
        var script = MakeScript(extension: ".js", shebangInterpreter: "ruby");

        var resolved = resolver.Resolve(script, DefaultConfig.Create(), []);

        Assert.Equal(InterpreterSource.Config, resolved.Source);
        Assert.Equal(@"C:\nodejs\node.exe", resolved.ExecutablePath);
    }

    [Fact]
    public void Resolve_NoShebang_UsesConfigMapping()
    {
        var searcher = new FakePathSearcher().Install("node", @"C:\nodejs\node.exe");
        var resolver = new InterpreterResolver(searcher);

        var resolved = resolver.Resolve(MakeScript(), DefaultConfig.Create(), []);

        Assert.Equal(InterpreterSource.Config, resolved.Source);
        Assert.True(resolved.IsResolved);
    }

    [Fact]
    public void Resolve_ExtensionDisabledInConfig_ReturnsNotFound()
    {
        var searcher = new FakePathSearcher().Install("node", @"C:\nodejs\node.exe");
        var resolver = new InterpreterResolver(searcher);
        // .ts ships disabled by default (SPEC 5.1).
        var script = MakeScript(extension: ".ts");

        var resolved = resolver.Resolve(script, DefaultConfig.Create(), []);

        Assert.Equal(InterpreterSource.None, resolved.Source);
        Assert.False(resolved.IsResolved);
    }

    [Fact]
    public void Resolve_InterpreterNotOnPath_ReturnsNotFound()
    {
        var resolver = new InterpreterResolver(new FakePathSearcher());

        var resolved = resolver.Resolve(MakeScript(), DefaultConfig.Create(), []);

        Assert.Same(ResolvedInterpreter.NotFound, resolved);
    }

    [Fact]
    public void Resolve_ExpandsScriptDirAndArgsPlaceholders()
    {
        var searcher = new FakePathSearcher().Install("node", @"C:\nodejs\node.exe");
        var resolver = new InterpreterResolver(searcher);
        var script = MakeScript(@"C:\proj\run.js");

        var resolved = resolver.Resolve(script, DefaultConfig.Create(), ["--flag", "value"]);

        Assert.Equal("\"C:\\proj\\run.js\" --flag value", resolved.ArgumentLine);
    }

    [Fact]
    public void Resolve_ArgumentWithSpaces_IsQuoted()
    {
        var searcher = new FakePathSearcher().Install("node", @"C:\nodejs\node.exe");
        var resolver = new InterpreterResolver(searcher);

        var resolved = resolver.Resolve(MakeScript(), DefaultConfig.Create(), ["hello world"]);

        Assert.Contains("\"hello world\"", resolved.ArgumentLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ArgumentWithEmbeddedQuote_IsEscaped()
    {
        var searcher = new FakePathSearcher().Install("node", @"C:\nodejs\node.exe");
        var resolver = new InterpreterResolver(searcher);

        var resolved = resolver.Resolve(MakeScript(), DefaultConfig.Create(), ["say \"hi\""]);

        Assert.Contains("\"say \\\"hi\\\"\"", resolved.ArgumentLine, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has space", "\"has space\"")]
    [InlineData("trailing\\", "trailing\\")]
    [InlineData("", "\"\"")]
    public void QuoteArgumentIfNeeded_MatchesWindowsEscapingRules(string input, string expected)
    {
        Assert.Equal(expected, InterpreterResolver.QuoteArgumentIfNeeded(input));
    }

    [Fact]
    public void QuoteArgumentIfNeeded_TrailingBackslashesBeforeClosingQuote_AreDoubled()
    {
        // A trailing backslash run that ends up right before the closing quote must be doubled,
        // otherwise CommandLineToArgvW would read it as escaping that quote.
        var quoted = InterpreterResolver.QuoteArgumentIfNeeded("dir with space\\");

        Assert.Equal("\"dir with space\\\\\"", quoted);
    }

    [Fact]
    public void Resolve_PsExecutionPolicyBypass_OnlyComesFromConfigTemplate_ResolverDoesNotAddIt()
    {
        var searcher = new FakePathSearcher().Install("powershell", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe");
        var resolver = new InterpreterResolver(searcher);
        var config = DefaultConfig.Create() with
        {
            Extensions = new Dictionary<string, ExtensionMapping>(DefaultConfig.Create().Extensions, StringComparer.OrdinalIgnoreCase)
            {
                [".ps1"] = new ExtensionMapping { Interpreter = "powershell", Args = "-File \"{script}\" {args}", Enabled = true },
            },
        };
        var script = MakeScript(@"C:\scripts\run.ps1", ".ps1");

        var resolved = resolver.Resolve(script, config, []);

        Assert.DoesNotContain("ExecutionPolicy", resolved.ArgumentLine, StringComparison.Ordinal);
    }
}
