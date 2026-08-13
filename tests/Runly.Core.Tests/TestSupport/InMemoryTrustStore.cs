using Runly.Core.Abstractions;
using Runly.Core.Defaults;
using Runly.Core.Models;

namespace Runly.Core.Tests;

/// <summary>In-memory <see cref="ITrustStore"/> fake so <see cref="Services.SecurityGate"/> tests stay IO-free.</summary>
internal sealed class InMemoryTrustStore : ITrustStore
{
    public string TrustPath => "memory://trust.json";

    public TrustStore Data { get; private set; } = DefaultConfig.CreateTrustStore();

    public void Load() => Data = DefaultConfig.CreateTrustStore();

    public void Save()
    {
        // Nothing to persist; this fake only exists in memory for the test's lifetime.
    }

    public bool IsTrusted(ScriptInfo script) =>
        Data.TrustedFolders.Any(folder => script.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) ||
        (Data.TrustedFiles.TryGetValue(script.Path, out var entry) &&
         string.Equals(entry.Sha256, script.Sha256, StringComparison.OrdinalIgnoreCase));

    public void TrustFile(ScriptInfo script) =>
        Data.TrustedFiles[script.Path] = new TrustedFileEntry { Sha256 = script.Sha256 ?? string.Empty, AddedUtc = DateTimeOffset.UtcNow };

    public void TrustFolder(string folderPath) => Data.TrustedFolders.Add(folderPath);

    public void UntrustFolder(string folderPath) => Data.TrustedFolders.RemoveAll(f => f == folderPath);

    public void ClearTrustedFiles() => Data.TrustedFiles.Clear();
}
