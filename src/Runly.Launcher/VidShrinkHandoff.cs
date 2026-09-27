using System.ComponentModel;
using System.Diagnostics;
using Runly.Core.Models;
using Runly.Core.Shell;

namespace Runly.Launcher;

internal sealed class VidShrinkHandoff
{
    internal const string ApplicationName = "VidShrink";
    internal const string DefaultProgId = "Teknesyum.VidShrink.Video";
    internal const string VideoCategory = "video";

    private static readonly RegistryRoot[] ReadOrder = [RegistryRoot.CurrentUser, RegistryRoot.LocalMachine];

    private readonly IRegistryAccessor _registry;
    private readonly Func<string, bool> _fileExists;
    private readonly string? _selfPath;

    internal VidShrinkHandoff(IRegistryAccessor registry, Func<string, bool> fileExists, string? selfPath)
    {
        _registry = registry;
        _fileExists = fileExists;
        _selfPath = selfPath;
    }

    internal string? FindExecutable(string extension, ExtensionMapping? mapping)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var normalized = RunlyRegistryLayout.NormalizeExtension(extension);
        var claimedProgId = ClaimedProgId(normalized);
        var isVideo = string.Equals(mapping?.Category, VideoCategory, StringComparison.OrdinalIgnoreCase);
        if (claimedProgId is null && !isVideo)
        {
            return null;
        }

        var executable = ExecutableFor(claimedProgId ?? DefaultProgId);
        if (executable is null && claimedProgId is not null &&
            !string.Equals(claimedProgId, DefaultProgId, StringComparison.OrdinalIgnoreCase))
        {
            executable = ExecutableFor(DefaultProgId);
        }

        return executable;
    }

    internal bool TryHandOff(LaunchRequest request, RunlyConfig config, Func<string, string, bool> start, Runly.Core.Abstractions.ILogger logger)
    {
        if (request.Verb != LaunchVerb.Run || string.IsNullOrWhiteSpace(request.ScriptPath) || !_fileExists(request.ScriptPath))
        {
            return false;
        }

        var extension = Path.GetExtension(request.ScriptPath);
        config.TryGetMapping(extension, out var mapping);
        var executable = FindExecutable(extension, mapping);
        if (executable is null)
        {
            return false;
        }

        if (start(executable, request.ScriptPath))
        {
            logger.Info($"VidShrink'e devredildi: {executable} \"{request.ScriptPath}\"");
            return true;
        }

        logger.Warn($"VidShrink başlatılamadı, Runly devam ediyor: {executable}");
        return false;
    }

    internal static bool TryStart(string executable, string filePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? string.Empty,
            };
            startInfo.ArgumentList.Add(filePath);
            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    private string? ClaimedProgId(string extension)
    {
        foreach (var root in ReadOrder)
        {
            var capabilities = _registry.GetValue(root, RunlyRegistryLayout.RegisteredApplicationsKey, ApplicationName)?.AsString();
            if (string.IsNullOrWhiteSpace(capabilities))
            {
                continue;
            }

            var progId = _registry.GetValue(root, capabilities.Trim().TrimEnd('\\') + @"\FileAssociations", extension)?.AsString();
            if (!string.IsNullOrWhiteSpace(progId) && !RunlyRegistryLayout.IsRunlyProgId(progId))
            {
                return progId.Trim();
            }
        }

        return null;
    }

    private string? ExecutableFor(string progId)
    {
        foreach (var root in ReadOrder)
        {
            var command = _registry.GetValue(root, $@"Software\Classes\{progId}\shell\open\command", RegistryValueEntry.DefaultValueName)?.AsString();
            var executable = CommandExecutable(command);
            if (executable is null)
            {
                continue;
            }

            if (_selfPath is not null && string.Equals(Path.GetFullPath(executable), Path.GetFullPath(_selfPath), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (_fileExists(executable))
            {
                return executable;
            }
        }

        return null;
    }

    internal static string? CommandExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var text = Environment.ExpandEnvironmentVariables(command.Trim());
        string candidate;
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            if (end <= 1)
            {
                return null;
            }

            candidate = text[1..end];
        }
        else
        {
            var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            candidate = exe > 0 ? text[..(exe + 4)] : text.Split(' ', 2)[0];
        }

        if (!Path.IsPathFullyQualified(candidate) ||
            !candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return candidate;
    }
}
