using Runly.Core.Abstractions;

namespace Runly.Core.Tests;

/// <summary>In-memory <see cref="IPathSearcher"/> fake so <see cref="Services.InterpreterResolver"/> tests avoid touching the real PATH.</summary>
internal sealed class FakePathSearcher : IPathSearcher
{
    private readonly Dictionary<string, string> _installed = new(StringComparer.OrdinalIgnoreCase);

    public FakePathSearcher Install(string exeName, string fullPath)
    {
        _installed[exeName] = fullPath;
        return this;
    }

    public string? Find(string exeName) => _installed.GetValueOrDefault(exeName);
}
