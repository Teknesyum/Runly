using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Runly.Core.Abstractions;
using Runly.Core.Models;

namespace Runly.Core.Services;

/// <summary>Starts the resolved interpreter and waits for it without redirecting the child's console (SPEC 7).</summary>
[SupportedOSPlatform("windows")]
public sealed class ProcessLauncher : IProcessLauncher
{
    private const int ErrorCancelled = 1223;

    /// <summary>Whether the given target must be started through <c>cmd.exe</c> instead of directly (K16).</summary>
    public static bool IsBatchTarget(string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return false;
        }

        var extension = Path.GetExtension(executablePath);
        return extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Full path of the command processor used to start batch targets.</summary>
    public static string CommandProcessorPath =>
        Environment.GetEnvironmentVariable("ComSpec") is { Length: > 0 } comSpec
            ? comSpec
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

    /// <summary>Builds the <c>cmd.exe</c> argument line that runs a batch target with the given arguments (K16).</summary>
    public static string BuildBatchArgumentLine(string batchPath, string argumentLine)
    {
        ArgumentNullException.ThrowIfNull(batchPath);

        var inner = $"\"{batchPath}\"";
        var escaped = EscapeShellMetacharacters(argumentLine ?? string.Empty);
        if (escaped.Length > 0)
        {
            inner = inner + " " + escaped;
        }

        // /d disables AutoRun hooks and /v:off prevents exclamation marks in user arguments from being
        // interpreted through delayed expansion. /s applies cmd's predictable outer-quote handling.
        return $"/d /s /v:off /c \"{inner}\"";
    }

    private static string EscapeShellMetacharacters(string argumentLine)
    {
        var builder = new System.Text.StringBuilder(argumentLine.Length + 8);
        var inQuotes = false;

        foreach (var c in argumentLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                builder.Append(c);
                continue;
            }

            if (!inQuotes && c is '&' or '|' or '<' or '>' or '^' or '(' or ')')
            {
                builder.Append('^');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public int Launch(ResolvedInterpreter interpreter, string workingDirectory, bool elevated)
    {
        ArgumentNullException.ThrowIfNull(interpreter);

        if (!interpreter.IsResolved)
        {
            return ExitCode.NoInterpreter;
        }

        var isBatch = IsBatchTarget(interpreter.ExecutablePath);
        var fileName = isBatch ? CommandProcessorPath : interpreter.ExecutablePath;
        var arguments = isBatch
            ? BuildBatchArgumentLine(interpreter.ExecutablePath, interpreter.ArgumentLine)
            : interpreter.ArgumentLine;

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = elevated,
        };

        if (elevated)
        {
            startInfo.Verb = "runas";
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return ExitCode.NoInterpreter;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            // The user declined the UAC elevation prompt; this is not an error condition (SPEC 7).
            return ExitCode.UserCancelled;
        }
        catch (Win32Exception)
        {
            // The interpreter executable could not be started (missing, not runnable, etc.).
            return ExitCode.NoInterpreter;
        }
    }

    /// <summary>Opens a file with an explicit absolute executable, without a console or wait.</summary>
    public OpenLaunchResult Open(string? executablePath, string argumentTemplate, ScriptInfo file, string[] fileArgs, string? runlyExecutablePath = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        fileArgs ??= [];

        if (string.IsNullOrWhiteSpace(executablePath)) return OpenLaunchResult.NotSelected;

        if (!Path.IsPathFullyQualified(executablePath) ||
            !string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return OpenLaunchResult.InvalidExecutable;
        }
        if (IsRunlyExecutable(executablePath, runlyExecutablePath)) return OpenLaunchResult.Recursive;
        if (!File.Exists(executablePath)) return OpenLaunchResult.NotFound;

        var arguments = (argumentTemplate ?? string.Empty)
            .Replace("{script}", file.Path, StringComparison.Ordinal)
            .Replace("{dir}", file.DirectoryPath ?? string.Empty, StringComparison.Ordinal)
            .Replace("{args}", string.Join(' ', fileArgs.Select(QuoteArgumentIfNeeded)), StringComparison.Ordinal);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                WorkingDirectory = file.DirectoryPath ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return process is null ? OpenLaunchResult.NotFound : OpenLaunchResult.Success;
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return OpenLaunchResult.NotFound;
        }
    }

    /// <summary>Returns whether an open handler would recurse into Runly itself.</summary>
    public static bool IsRunlyExecutable(string executablePath, string? runlyExecutablePath = null)
    {
        var candidate = Path.GetFullPath(executablePath);
        var ownPath = runlyExecutablePath ?? Environment.ProcessPath;
        return string.Equals(Path.GetFileName(candidate), "Runly.exe", StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(ownPath) &&
                string.Equals(candidate, Path.GetFullPath(ownPath), StringComparison.OrdinalIgnoreCase));
    }

    private static string QuoteArgumentIfNeeded(string argument) =>
        argument.IndexOfAny([' ', '\t', '"']) >= 0 ? '"' + argument.Replace("\"", "\\\"") + '"' : argument;
}
