namespace Runly.Core.Abstractions;

/// <summary>Finds executables by scanning PATH and PATHEXT manually, with a 24 hour cache (SPEC 8).</summary>
public interface IPathSearcher
{
    /// <summary>
    /// Returns the full path of an executable, or <see langword="null"/> when it is not installed.
    /// Zero-byte Microsoft Store stubs are skipped.
    /// </summary>
    string? Find(string exeName);
}
