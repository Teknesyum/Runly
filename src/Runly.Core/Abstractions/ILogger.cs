namespace Runly.Core.Abstractions;

/// <summary>Appends to <c>runly.log</c>; logging failures are swallowed and never reach the user (SPEC 11).</summary>
public interface ILogger
{
    /// <summary>Writes an informational line.</summary>
    void Info(string message);

    /// <summary>Writes a warning line.</summary>
    void Warn(string message);

    /// <summary>Writes an error line, including the exception details when one is supplied.</summary>
    void Error(string message, Exception? exception = null);
}
