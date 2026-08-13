using System.Text;
using Runly.Core.Abstractions;
using Runly.Core.Defaults;
using Runly.Core.Models;

namespace Runly.Core.Services;

/// <summary>
/// Resolves shebang first, then the configuration mapping, expanding <c>{script}</c>/<c>{args}</c>/<c>{dir}</c>
/// into a ready command line (SPEC 8).
/// </summary>
public sealed class InterpreterResolver : IInterpreterResolver
{
    // Decision K4: the shebang→PATH fallback chain lives here, not in ScriptInspector.
    private static readonly Dictionary<string, string[]> ShebangFallbackChains = new(StringComparer.OrdinalIgnoreCase)
    {
        ["python3"] = ["python3", "python", "py"],
        ["python"] = ["python", "py"],
        ["node"] = ["node", "nodejs"],
    };

    private readonly IPathSearcher _pathSearcher;

    /// <summary>Creates a resolver that looks up candidate interpreter names through the given searcher.</summary>
    public InterpreterResolver(IPathSearcher pathSearcher)
    {
        ArgumentNullException.ThrowIfNull(pathSearcher);
        _pathSearcher = pathSearcher;
    }

    /// <inheritdoc />
    public ResolvedInterpreter Resolve(ScriptInfo script, RunlyConfig config, string[] scriptArgs)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(config);
        scriptArgs ??= [];

        return ResolveFromShebang(script, scriptArgs)
            ?? ResolveFromConfig(script, config, scriptArgs)
            ?? ResolvedInterpreter.NotFound;
    }

    private ResolvedInterpreter? ResolveFromShebang(ScriptInfo script, string[] scriptArgs)
    {
        if (string.IsNullOrWhiteSpace(script.ShebangInterpreter))
        {
            return null;
        }

        var candidates = ShebangFallbackChains.TryGetValue(script.ShebangInterpreter, out var chain)
            ? chain
            : [script.ShebangInterpreter];

        foreach (var candidate in candidates)
        {
            var executablePath = _pathSearcher.Find(candidate);
            if (executablePath is null)
            {
                continue;
            }

            return new ResolvedInterpreter
            {
                ExecutablePath = executablePath,
                ArgumentLine = BuildArgumentLine(DefaultConfig.ScriptThenArgs, script, scriptArgs),
                Source = InterpreterSource.Shebang,
            };
        }

        return null;
    }

    private ResolvedInterpreter? ResolveFromConfig(ScriptInfo script, RunlyConfig config, string[] scriptArgs)
    {
        if (!config.TryGetMapping(script.Extension, out var mapping) || !mapping.Enabled)
        {
            return null;
        }

        var executablePath = _pathSearcher.Find(mapping.Interpreter);
        if (executablePath is null)
        {
            return null;
        }

        return new ResolvedInterpreter
        {
            ExecutablePath = executablePath,
            ArgumentLine = BuildArgumentLine(mapping.Args, script, scriptArgs),
            Source = InterpreterSource.Config,
        };
    }

    // {script} and {dir} are substituted raw: every shipped template already wraps them in literal quotes
    // (e.g. "\"{script}\" {args}"). Only {args} is quoted here per token, since the caller's raw tokens
    // never carry their own quoting (T2.md).
    private static string BuildArgumentLine(string template, ScriptInfo script, string[] scriptArgs)
    {
        var joinedArgs = string.Join(' ', scriptArgs.Select(QuoteArgumentIfNeeded));

        return template
            .Replace("{script}", script.Path, StringComparison.Ordinal)
            .Replace("{dir}", script.DirectoryPath ?? string.Empty, StringComparison.Ordinal)
            .Replace("{args}", joinedArgs, StringComparison.Ordinal);
    }

    /// <summary>
    /// Quotes an argument using the Windows <c>CommandLineToArgvW</c> escaping rules when it contains whitespace
    /// or a quote: an embedded <c>"</c> becomes <c>\"</c>, and a run of backslashes immediately before the
    /// closing quote is doubled so it is not read as an escape (T2.md).
    /// </summary>
    internal static string QuoteArgumentIfNeeded(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        if (argument.IndexOfAny([' ', '\t', '"']) < 0)
        {
            return argument;
        }

        var builder = new StringBuilder();
        builder.Append('"');

        var backslashCount = 0;
        foreach (var ch in argument)
        {
            if (ch == '\\')
            {
                backslashCount++;
                continue;
            }

            if (ch == '"')
            {
                builder.Append('\\', backslashCount * 2 + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            builder.Append('\\', backslashCount);
            backslashCount = 0;
            builder.Append(ch);
        }

        builder.Append('\\', backslashCount * 2);
        builder.Append('"');

        return builder.ToString();
    }
}
