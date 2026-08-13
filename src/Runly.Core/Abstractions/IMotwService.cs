namespace Runly.Core.Abstractions;

/// <summary>Reads and removes the <c>:Zone.Identifier</c> alternate data stream that marks downloaded files (SPEC 6).</summary>
public interface IMotwService
{
    /// <summary>Whether the file carries a zone identifier of 3 (internet) or 4 (untrusted).</summary>
    bool HasMotw(string path);

    /// <summary>The zone identifier, or <see langword="null"/> when the stream is absent or unreadable.</summary>
    int? GetZoneId(string path);

    /// <summary>Deletes the <c>:Zone.Identifier</c> stream.</summary>
    void Strip(string path);
}
