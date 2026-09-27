using Runly.Core.Models;
using Runly.Core.Services;
using Runly.Core.Shell;

namespace Runly.Settings;

/// <summary>Entry point of <c>RunlySettings.exe</c>: wires the Core services and starts <see cref="MainForm"/>.</summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var selectedExtension = SettingsCommandLine.ParseSelectedExtension(args);

        // Must run before any window exists, otherwise Win32 scrollbars stay light.
        NeonTheme.EnableDarkMode();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var configStore = new ConfigStore();
        var config = configStore.Load();
        var logger = new FileLogger(config.LogEnabled);

        Application.ThreadException += (_, e) => ReportUnhandled(logger, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportUnhandled(logger, e.ExceptionObject as Exception ?? new Exception("Bilinmeyen istisna."));

        var trustStore = new TrustStoreService(null, logger);
        trustStore.Load();

        var pathSearcher = new PathSearcher(null, logger);
        var registry = new Win32RegistryAccessor();
        var menuCleaner = new ContextMenuCleaner(registry, new ContextMenuScanner(registry));
        var shellRegistrar = new ShellRegistrar(pathSearcher, menuCleaner);
        var registryBackup = new RegistryBackup(registry);

        var installDir = AppContext.BaseDirectory;
        UpdateService.CleanupOld(installDir);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var updates = new UpdateController(
            new UpdateService(http),
            typeof(Program).Assembly.GetName().Version ?? new Version(0, 0),
            installDir,
            logger);

        try
        {
            var form = new MainForm(configStore, config, trustStore, shellRegistrar, registryBackup, menuCleaner, logger, selectedExtension);
            form.AttachUpdates(updates);
            updates.Restart = () =>
            {
                form.Close();
                return !form.Visible;
            };
            form.Shown += async (_, _) => await updates.CheckAsync();
            Application.Run(form);
        }
        catch (Exception ex)
        {
            ReportUnhandled(logger, ex);
        }
    }

    private static void ReportUnhandled(Runly.Core.Abstractions.ILogger logger, Exception exception)
    {
        logger.Error("Beklenmeyen istisna", exception);

        NeonMessageBox.Show(
            $"Beklenmeyen bir hata oluştu ve uygulama devam edemiyor:\n\n{exception.Message}\n\n" +
            "Ayrıntılar günlük dosyasına yazıldı.",
            Runly.Core.Shell.RunlyRegistryLayout.ApplicationName + " Ayarları — Hata",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
