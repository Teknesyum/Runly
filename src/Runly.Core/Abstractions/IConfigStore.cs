using Runly.Core.Models;

namespace Runly.Core.Abstractions;

/// <summary>Reads and writes <c>config.json</c>, falling back to the built-in defaults instead of failing (SPEC 5.1).</summary>
public interface IConfigStore
{
    /// <summary>Full path of the configuration file being used.</summary>
    string ConfigPath { get; }

    /// <summary>Loads the configuration; a missing or corrupt file yields the defaults and never throws.</summary>
    RunlyConfig Load();

    /// <summary>Writes the configuration atomically.</summary>
    void Save(RunlyConfig config);
}
