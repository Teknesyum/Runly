using System.Diagnostics;

namespace Runly.Core.Shell;

/// <summary>
/// Turns the configured editor command into the name the user sees in the context menu, so
/// "Runly: Düzenle" says which program it will open (K31 round).
/// </summary>
public static class EditorName
{
    /// <summary>The MUIVerb for the <c>edit</c> verb; the editor's name is appended when one is known.</summary>
    public static string EditVerbLabel(string? editorCommand)
    {
        var name = Describe(editorCommand);
        return name is null ? "Runly: Düzenle" : $"Runly: Düzenle ({name})";
    }

    /// <summary>The friendly name of the editor an <c>editorCommand</c> points at, or <see langword="null"/>.</summary>
    public static string? Describe(string? editorCommand)
    {
        if (string.IsNullOrWhiteSpace(editorCommand))
        {
            return null;
        }

        var path = editorCommand.Trim().Trim('"');
        if (path.Length == 0)
        {
            return null;
        }

        string fileName;
        try
        {
            fileName = Path.GetFileNameWithoutExtension(path);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (fileName.Length == 0)
        {
            return null;
        }

        var described = FromVersionInfo(path);
        if (described is not null)
        {
            return described;
        }

        return fileName.Length <= 2
            ? fileName.ToUpperInvariant()
            : char.ToUpperInvariant(fileName[0]) + fileName[1..];
    }

    private static string? FromVersionInfo(string path)
    {
        try
        {
            if (!Path.IsPathRooted(path) || !File.Exists(path))
            {
                return null;
            }

            var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            return string.IsNullOrEmpty(description) ? null : description;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
