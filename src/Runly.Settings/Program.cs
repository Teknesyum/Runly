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
        var shellRegistrar = new ShellRegistrar(pathSearcher);
        var registryBackup = new RegistryBackup(new Win32RegistryAccessor());

        try
        {
            Application.Run(new MainForm(configStore, config, trustStore, shellRegistrar, registryBackup, logger, selectedExtension));
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
            "Runly Ayarları — Hata",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
