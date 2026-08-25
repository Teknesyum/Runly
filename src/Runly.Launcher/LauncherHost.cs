using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Runly.Core.Abstractions;
using Runly.Core.Models;
using Runly.Core.Paths;
using Runly.Core.Services;
using Runly.Launcher.Cli;
using Runly.Launcher.Ui;

[assembly: SupportedOSPlatform("windows")]

namespace Runly.Launcher;

/// <summary>
/// The whole launcher: parses the command line, runs the security gate, launches the script (SPEC 6, SPEC 7).
/// Both shipped binaries call <see cref="Main"/>; the only thing they disagree on is <see cref="LauncherSurface"/> (K29).
/// </summary>
internal static class LauncherHost
{
    private static ILogger s_logger = new FileLogger(enabled: true);
    private static TaskDialogInterop? s_dialogs;
    private static LauncherSurface s_surface = LauncherSurface.Console;

    /// <summary>Runs the launcher on behalf of one of the two entry-point assemblies.</summary>
    internal static int Main(string[] args, LauncherSurface surface)
    {
        s_surface = surface;
        UseUtf8Console();

        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            return HandleUnexpected(ex);
        }
    }

    private static int Run(string[] args)
    {
        if (!CommandLineParser.TryParse(args, out var request, out var parseError))
        {
            return ShowUsage(parseError);
        }

        TryEnsureDirectories();

        var configStore = new ConfigStore();
        var config = configStore.Load();

        s_logger = new FileLogger(config.LogEnabled);
        var dialogs = new TaskDialogInterop(s_logger);
        s_dialogs = dialogs;

        s_logger.Info($"Başlatıldı: verb={request.Verb} script=\"{request.ScriptPath}\" args={request.ScriptArgs.Length}");

        var motwService = new MotwService(s_logger);
        var inspector = new ScriptInspector(motwService);
        var pathSearcher = new PathSearcher(logger: s_logger);

        ScriptInfo script;
        try
        {
            script = inspector.Inspect(request.ScriptPath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException
                                      or UnauthorizedAccessException or ArgumentException)
        {
            s_logger.Error($"Script okunamadı: {request.ScriptPath}", ex);
            dialogs.ShowError("Dosya açılamadı", $"\"{request.ScriptPath}\" okunamadı.\n\n{ex.Message}");
            return ExitCode.UsageError;
        }

        if (request.Verb == LaunchVerb.Edit)
        {
            return RunEditVerb(config, script, pathSearcher, dialogs);
        }

        config.TryGetMapping(script.Extension, out var selectedMapping);

        var scriptArgs = request.ScriptArgs;
        if (request.Verb == LaunchVerb.PromptArgs)
        {
            // The arguments are collected before the interpreter is resolved so that the command line
            // shown in the security dialog is byte-for-byte the one that will run (SPEC 6).
            var typed = dialogs.AskArgs(script);
            if (typed is null)
            {
                s_logger.Info("Argüman kutusu iptal edildi.");
                return ExitCode.UserCancelled;
            }

            scriptArgs = [.. scriptArgs, .. ArgumentSplitter.Split(typed)];
        }

        if (selectedMapping is { Enabled: true, Kind: HandlerKind.Open })
        {
            return OpenFile(selectedMapping, script, scriptArgs, config, dialogs);
        }

        var resolver = new InterpreterResolver(pathSearcher);
        var interpreter = resolver.Resolve(script, config, scriptArgs);
        if (!interpreter.IsResolved)
        {
            return ReportMissingInterpreter(script, dialogs);
        }

        var trustStore = new TrustStoreService(logger: s_logger);
        trustStore.Load();

        var gate = new SecurityGate();
        var verdict = gate.Evaluate(script, config, trustStore);
        s_logger.Info($"Güvenlik kararı: {verdict}");

        if (verdict != SecurityVerdict.Trusted &&
            !PassSecurityGate(script, interpreter, verdict, trustStore, motwService, dialogs))
        {
            return ExitCode.UserCancelled;
        }

        return LaunchScript(request, config, script, interpreter);
    }

    private static int OpenFile(ExtensionMapping mapping, ScriptInfo file, string[] fileArgs, RunlyConfig config, TaskDialogInterop dialogs)
    {
        if (string.IsNullOrWhiteSpace(mapping.OpenWith))
        {
            var openSettings = dialogs.AskOpenSettings(
                "Uygulama seçilmedi",
                $"\"{file.Extension}\" uzantısı için açılacak uygulama seçilmedi.",
                file.FileName);
            if (openSettings) TryStartSettings(file.Extension);
            return ExitCode.NoInterpreter;
        }
        var trustStore = new TrustStoreService(logger: s_logger);
        trustStore.Load();
        var verdict = new SecurityGate().Evaluate(file, config, trustStore, HandlerKind.Open);
        if (verdict == SecurityVerdict.MotwBlocked)
        {
            var target = new ResolvedInterpreter
            {
                ExecutablePath = mapping.OpenWith,
                ArgumentLine = mapping.Args.Replace("{script}", file.Path, StringComparison.Ordinal),
                Source = InterpreterSource.Config,
            };
            if (!PassSecurityGate(file, target, verdict, trustStore, new MotwService(s_logger), dialogs))
            {
                return ExitCode.UserCancelled;
            }
        }

        var result = new ProcessLauncher().Open(mapping.OpenWith, mapping.Args, file, fileArgs, Environment.ProcessPath);
        if (result == OpenLaunchResult.Success) return ExitCode.Success;

        var message = result switch
        {
            OpenLaunchResult.InvalidExecutable => "Seçilen yol geçerli, mutlak bir .exe yolu değil.",
            OpenLaunchResult.Recursive => "Runly kendisini dosya açma uygulaması olarak çağıramaz.",
            _ => $"Seçilen uygulama bulunamadı veya başlatılamadı:\n{mapping.OpenWith}",
        };
        dialogs.ShowError("Uygulama açılamadı", message);
        return ExitCode.NoInterpreter;
    }

    /// <summary>Shows the security dialog and applies its answer; returns whether the script may run.</summary>
    private static bool PassSecurityGate(
        ScriptInfo script,
        ResolvedInterpreter interpreter,
        SecurityVerdict verdict,
        ITrustStore trustStore,
        IMotwService motwService,
        TaskDialogInterop dialogs)
    {
        var decision = dialogs.AskSecurity(script, interpreter.CommandLine, verdict);
        if (decision is null || !decision.Allow)
        {
            s_logger.Info($"Kullanıcı güvenlik kapısında iptal etti: {script.Path}");
            return false;
        }

        PersistTrust(script, decision, trustStore, dialogs);

        if (decision.StripMotw)
        {
            motwService.Strip(script.Path);
            s_logger.Info($"İnternet işareti kaldırıldı: {script.Path}");
        }

        return true;
    }

    // Decision K11: TrustFile/TrustFolder only mutate memory. Without this explicit Save() the user would
    // be asked the same question on every launch, and the failure would be invisible — so it is reported.
    private static void PersistTrust(ScriptInfo script, SecurityDecision decision, ITrustStore trustStore, TaskDialogInterop dialogs)
    {
        if (!decision.RememberFile && !decision.RememberFolder)
        {
            return;
        }

        if (decision.RememberFile)
        {
            trustStore.TrustFile(script);
        }

        if (decision.RememberFolder)
        {
            var folder = script.DirectoryPath;
            if (string.IsNullOrWhiteSpace(folder))
            {
                s_logger.Warn($"Klasör güveni yazılamadı, üst klasör bulunamadı: {script.Path}");
                return;
            }

            trustStore.TrustFolder(folder);
        }

        try
        {
            trustStore.Save();
            s_logger.Info($"Güven kaydedildi (dosya={decision.RememberFile}, klasör={decision.RememberFolder}): {trustStore.TrustPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            s_logger.Error("trust.json yazılamadı, güven kalıcı olmadı.", ex);
            dialogs.ShowError(
                "Güven kaydedilemedi",
                $"Seçiminiz \"{trustStore.TrustPath}\" dosyasına yazılamadı, bu yüzden bir dahaki sefere yine sorulacak.\n\n{ex.Message}");
        }
    }

    private static int RunEditVerb(RunlyConfig config, ScriptInfo script, IPathSearcher pathSearcher, TaskDialogInterop dialogs)
    {
        if (EditorLauncher.Open(config.EditorCommand, script.Path, pathSearcher, s_logger))
        {
            return ExitCode.Success;
        }

        s_logger.Error($"Hiçbir editör açılamadı: {script.Path}");
        dialogs.ShowError(
            "Editör açılamadı",
            $"\"{config.EditorCommand}\" ve \"{EditorLauncher.FallbackEditor}\" başlatılamadı. Ayarlardan başka bir editör seçebilirsiniz.");
        return ExitCode.ScriptFailed;
    }

    private static int ReportMissingInterpreter(ScriptInfo script, TaskDialogInterop dialogs)
    {
        var extension = script.Extension.Length == 0 ? "(uzantısız)" : script.Extension;
        s_logger.Warn($"Yorumlayıcı çözülemedi: {extension} — {script.Path}");

        var openSettings = dialogs.AskOpenSettings(
            "Yorumlayıcı bulunamadı",
            $"\"{extension}\" için yorumlayıcı ayarlı değil ya da kurulu değil.",
            script.FileName);

        if (openSettings)
        {
            TryStartSettings(script.Extension);
        }

        return ExitCode.NoInterpreter;
    }

    private static int LaunchScript(LaunchRequest request, RunlyConfig config, ScriptInfo script, ResolvedInterpreter interpreter)
    {
        var workingDirectory = script.DirectoryPath is { Length: > 0 } directory ? directory : Environment.CurrentDirectory;
        var keepMode = request.NoWait ? KeepWindowMode.Never : config.KeepWindowOpen;

        s_logger.Info($"Çalıştırılıyor: {interpreter.CommandLine}");

        return ScriptRunner.RunAndWait(
            new ProcessLauncher(),
            interpreter,
            workingDirectory,
            elevated: request.Verb == LaunchVerb.RunAs,
            (exitCode, elapsed) => ConsoleWaiter.WaitIfNeeded(exitCode, elapsed, keepMode, s_surface),
            s_logger);
    }

    private static int ShowUsage(string? error)
    {
        // The GUI binary has no console to print to, so the same text has to arrive as a dialog or not at all.
        if (s_surface == LauncherSurface.Gui)
        {
            var body = error is null ? CommandLineParser.UsageText : error + "\n\n" + CommandLineParser.UsageText;
            Dialogs().ShowError("Runly nasıl kullanılır", body);
            return ExitCode.UsageError;
        }

        if (error is not null)
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine();
        }

        Console.WriteLine(CommandLineParser.UsageText);

        // Launched from Explorer the console would vanish before the text could be read.
        if (!Console.IsOutputRedirected && !Console.IsInputRedirected)
        {
            Console.WriteLine();
            Console.WriteLine("Kapatmak için bir tuşa basın...");
            try
            {
                Console.ReadKey(intercept: true);
            }
            catch (InvalidOperationException)
            {
                // No console input handle; nothing to wait for.
            }
        }

        return ExitCode.UsageError;
    }

    private static TaskDialogInterop Dialogs() => s_dialogs ??= new TaskDialogInterop(s_logger);

    private static void TryStartSettings(string? extension = null)
    {
        var directory = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var settingsPath = Path.Combine(directory, "RunlySettings.exe");
        if (!File.Exists(settingsPath))
        {
            s_logger.Warn($"RunlySettings.exe bulunamadı: {settingsPath}");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo { FileName = settingsPath, UseShellExecute = true };
            if (SettingsCommandLine.NormalizeExtension(extension) is { } selected)
            {
                startInfo.Arguments = $"{SettingsCommandLine.SelectSwitch} {selected}";
            }

            using var process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            s_logger.Error($"RunlySettings.exe başlatılamadı: {settingsPath}", ex);
        }
    }

    // SPEC 11: nothing is swallowed. The stack trace goes to the log, the user sees a Turkish message.
    private static int HandleUnexpected(Exception ex)
    {
        try
        {
            s_logger.Error("Yakalanmayan hata.", ex);
        }
        catch (Exception loggingFailure) when (loggingFailure is IOException or UnauthorizedAccessException)
        {
            // Logging must never mask the original failure.
        }

        var body = $"{ex.Message}\n\nAyrıntılar günlüğe yazıldı:\n{RunlyPaths.LogPath}";
        if (s_dialogs is not null || s_surface == LauncherSurface.Gui)
        {
            Dialogs().ShowError("Runly beklenmedik bir hatayla karşılaştı", body);
        }
        else
        {
            Console.Error.WriteLine($"Runly beklenmedik bir hatayla karşılaştı: {body}");
        }

        return ExitCode.ScriptFailed;
    }

    private static void TryEnsureDirectories()
    {
        try
        {
            RunlyPaths.EnsureCreated();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // %APPDATA% is unavailable; the stores fall back to defaults and the launch continues.
        }
    }

    private static void UseUtf8Console()
    {
        if (s_surface == LauncherSurface.Gui)
        {
            return;
        }

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
            // A redirected or absent console handle; the default encoding stays in place.
        }
    }
}
