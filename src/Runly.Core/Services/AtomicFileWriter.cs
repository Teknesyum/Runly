namespace Runly.Core.Services;

/// <summary>Shared temp-file-then-move write and corrupt-file backup used by the JSON-backed stores (SPEC 5.1, 5.2, 8).</summary>
internal static class AtomicFileWriter
{
    /// <summary>Writes <paramref name="content"/> to <paramref name="path"/> via a temp file and an overwriting move.</summary>
    public static void Write(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            // A failed replace (locked target, permissions, disk errors) must not litter the settings
            // directory with GUID-named temporary files. Preserve the original exception if cleanup fails.
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Renames an existing file to <c>&lt;path&gt;.bak</c>, overwriting any previous backup. No-op if the file is gone.</summary>
    public static void RenameToBackup(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.Move(path, path + ".bak", overwrite: true);
    }
}
