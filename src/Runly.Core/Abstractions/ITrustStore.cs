using Runly.Core.Models;

namespace Runly.Core.Abstractions;

/// <summary>Holds and persists the trusted folders and file fingerprints used by the security gate (SPEC 5.2, SPEC 6).</summary>
public interface ITrustStore
{
    /// <summary>Full path of the trust file being used.</summary>
    string TrustPath { get; }

    /// <summary>The in-memory trust data; valid after <see cref="Load"/>.</summary>
    TrustStore Data { get; }

    /// <summary>Loads the trust file; a missing or corrupt file yields an empty store and never throws.</summary>
    void Load();

    /// <summary>Writes the trust file atomically.</summary>
    void Save();

    /// <summary>Whether the script is covered by a trusted folder, or by a trusted file entry whose hash still matches.</summary>
    bool IsTrusted(ScriptInfo script);

    /// <summary>Remembers this exact file and its current hash, replacing any earlier entry for the same path.</summary>
    void TrustFile(ScriptInfo script);

    /// <summary>Remembers a folder so every script under it runs without prompting.</summary>
    void TrustFolder(string folderPath);

    /// <summary>Removes a trusted folder; used by the settings GUI (SPEC 10.3).</summary>
    void UntrustFolder(string folderPath);

    /// <summary>Removes every trusted file entry; used by the settings GUI (SPEC 10.3).</summary>
    void ClearTrustedFiles();
}
