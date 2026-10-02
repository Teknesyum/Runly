using System.Diagnostics;
using System.Text.Json;
using Runly.Core.Abstractions;
using Runly.Core.Services;

namespace Runly.Settings;

internal enum UpdateStage
{
    None,
    Available,
    Downloading,
    Ready,
    Restarting,
    Failed,
}

/// <summary>The work side of the update channel. It owns the stage and the percentage; the badge and
/// <see cref="Dialogs.UpdatePanel"/> only read them and redraw on <see cref="Changed"/>.</summary>
internal sealed class UpdateController : IDisposable
{
    public const int RestartDelayMs = 1800;

    private readonly UpdateService _service;
    private readonly Version _current;
    private readonly string _installDir;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _life = new();
    private CancellationTokenSource? _download;
    private string? _package;

    public UpdateController(UpdateService service, Version current, string installDir, ILogger? logger)
    {
        _service = service;
        _current = current;
        _installDir = installDir;
        _logger = logger;
    }

    public event EventHandler? Changed;

    public UpdateStage Stage { get; private set; }

    public ReleaseInfo? Release { get; private set; }

    public double Percent { get; private set; }

    public string? Error { get; private set; }

    /// <summary>Called on the UI thread once the new files are in place and the announcement has been on
    /// screen for <see cref="RestartDelayMs"/>. Returns false when the user kept the window open.</summary>
    public Func<bool>? Restart { get; set; }

    public bool IsInstalled => File.Exists(Path.Combine(_installDir, "Runly.exe"));

    /// <summary>Asks the release channel once. True: a newer release is waiting; false: this is the
    /// latest; null: not installed, already in an update stage, or the check failed.</summary>
    public async Task<bool?> CheckAsync()
    {
        if (!IsInstalled || Stage != UpdateStage.None)
        {
            return null;
        }

        try
        {
            var release = await _service.CheckAsync(_current, _life.Token);
            if (release is null)
            {
                return false;
            }

            Release = release;
            Set(UpdateStage.Available);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger?.Info("Güncelleme denetimi yapılamadı: " + ex.Message);
            return null;
        }
    }

    public async void Download(bool installWhenReady)
    {
        if (Release is null || Stage is UpdateStage.Downloading or UpdateStage.Restarting)
        {
            return;
        }

        if (Stage == UpdateStage.Ready)
        {
            if (installWhenReady)
            {
                Install();
            }

            return;
        }

        _download = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        Error = null;
        Percent = 0;
        Set(UpdateStage.Downloading);
        var progress = new Progress<double>(value =>
        {
            if (Stage == UpdateStage.Downloading && value > Percent)
            {
                Percent = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        });

        try
        {
            _package = await _service.DownloadAsync(Release, progress, _download.Token);
            Percent = 100;
            Set(UpdateStage.Ready);
            if (installWhenReady)
            {
                Install();
            }
        }
        catch (OperationCanceledException)
        {
            Percent = 0;
            Set(_life.IsCancellationRequested ? UpdateStage.None : UpdateStage.Available);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Fail(ex);
        }
        finally
        {
            _download?.Dispose();
            _download = null;
        }
    }

    public void Cancel() => _download?.Cancel();

    public void Install()
    {
        if (Stage != UpdateStage.Ready || _package is null)
        {
            return;
        }

        try
        {
            UpdateService.Apply(_package, _installDir);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Fail(ex);
            return;
        }

        _logger?.Info("Güncelleme yüklendi: " + Release?.Tag);
        Set(UpdateStage.Restarting);
        var timer = new System.Windows.Forms.Timer { Interval = RestartDelayMs };
        timer.Tick += (_, _) =>
        {
            timer.Dispose();
            if (Restart?.Invoke() ?? false)
            {
                Process.Start(new ProcessStartInfo(Path.Combine(_installDir, "RunlySettings.exe")) { UseShellExecute = false, WorkingDirectory = _installDir });
            }
        };
        timer.Start();
    }

    /// <summary>Puts the controller in a stage without doing the work. Only the UI audit uses it.</summary>
    internal void Preview(UpdateStage stage, ReleaseInfo release, double percent, string? error = null)
    {
        Release = release;
        Percent = percent;
        Error = error;
        Set(stage);
    }

    private void Fail(Exception ex)
    {
        _logger?.Error("Güncelleme yarıda kaldı", ex);
        Error = ex.Message;
        Set(UpdateStage.Failed);
    }

    private void Set(UpdateStage stage)
    {
        Stage = stage;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _life.Cancel();
        _life.Dispose();
    }
}
