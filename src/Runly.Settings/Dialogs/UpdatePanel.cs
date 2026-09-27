using System.Globalization;

namespace Runly.Settings.Dialogs;

/// <summary>WinForms port of the teknesyum-ui <c>durum</c> template's GuncellemePaneli: status sentence,
/// new-version row, gradient progress with percentage, and the Var / İniyor / Hazır buttons. The template
/// ships only Avalonia, React and Electron; see Teknesyum-UI docs/olaylar/2026-09-27.</summary>
internal sealed class UpdatePanel : NeonForm
{
    private readonly UpdateController _controller;
    private readonly Label _status;
    private readonly Label _versionLabel;
    private readonly Label _version;
    private readonly NeonProgressBar _bar;
    private readonly Label _percent;
    private readonly TableLayoutPanel _progressRow;
    private readonly NeonButton _downloadInstall;
    private readonly NeonButton _download;
    private readonly NeonButton _cancel;
    private readonly NeonButton _install;
    private readonly NeonButton _close;

    public UpdatePanel(UpdateController controller)
    {
        _controller = controller;
        Text = Strings.Get("app.title") + " " + Strings.Get("update.title");
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;

        var statusRow = Metrics.Row(Palette.Body, 8) * 2;
        var versionRow = Metrics.Row(Palette.MonoBody, 8);
        var progressRow = Metrics.Row(Palette.MonoBody, 12);
        var buttonRow = Metrics.ButtonHeight + Metrics.Px(TeknesyumTokens.Space3);
        ClientSize = new Size(
            Metrics.Px(440),
            Metrics.CaptionHeight + Metrics.ResizeBorder + Metrics.Px(32) + statusRow + versionRow + progressRow + buttonRow);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(Metrics.Px(TeknesyumTokens.Space4)),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, statusRow));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, versionRow));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, progressRow));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _status = new Label { Dock = DockStyle.Fill, ForeColor = Palette.TextBody, Font = Palette.Body, TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(_status, 0, 0);

        var versionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        _versionLabel = new Label { AutoSize = true, ForeColor = Palette.TextLabel, Font = Palette.LabelFont, Margin = new Padding(0, Metrics.Px(2), Metrics.Px(TeknesyumTokens.Space2), 0) };
        _version = new Label { AutoSize = true, ForeColor = Palette.Renk1, Font = Palette.MonoBody, Margin = Padding.Empty };
        versionPanel.Controls.Add(_versionLabel);
        versionPanel.Controls.Add(_version);
        layout.Controls.Add(versionPanel, 0, 1);

        _progressRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        _progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bar = new NeonProgressBar { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, Metrics.Px(TeknesyumTokens.Space3), 0) };
        _percent = new Label { AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = Palette.Renk1, Font = Palette.MonoBody, Margin = Padding.Empty };
        _bar.DisplayChanged += (_, _) => _percent.Text = "%" + Math.Floor(_bar.Displayed).ToString(CultureInfo.InvariantCulture);
        _progressRow.Controls.Add(_bar, 0, 0);
        _progressRow.Controls.Add(_percent, 1, 0);
        layout.Controls.Add(_progressRow, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        _close = new NeonButton { Primary = false, BackColor = Palette.AppBg, AutoSize = true };
        _install = new NeonButton { Primary = true, BackColor = Palette.AppBg, AutoSize = true };
        _cancel = new NeonButton { Primary = false, BackColor = Palette.AppBg, AutoSize = true };
        _download = new NeonButton { Primary = false, BackColor = Palette.AppBg, AutoSize = true };
        _downloadInstall = new NeonButton { Primary = true, BackColor = Palette.AppBg, AutoSize = true };
        _close.Click += (_, _) => Close();
        _install.Click += (_, _) => _controller.Install();
        _cancel.Click += (_, _) => _controller.Cancel();
        _download.Click += (_, _) => _controller.Download(installWhenReady: false);
        _downloadInstall.Click += (_, _) => _controller.Download(installWhenReady: true);
        buttons.Controls.AddRange([_close, _install, _cancel, _download, _downloadInstall]);
        layout.Controls.Add(buttons, 0, 3);

        Controls.Add(layout);
        CancelButton = _close;

        _controller.Changed += OnChanged;
        Render();
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        if (!IsDisposed)
        {
            Render();
        }
    }

    private void Render()
    {
        var stage = _controller.Stage;
        _status.Text = stage switch
        {
            UpdateStage.Downloading => Strings.Get("update.downloading"),
            UpdateStage.Ready => Strings.Get("update.ready"),
            UpdateStage.Restarting => Strings.Get("update.restarting"),
            UpdateStage.Failed => string.Format(CultureInfo.CurrentCulture, Strings.Get("update.failed"), _controller.Error),
            _ => Strings.Get("update.available"),
        };
        _status.ForeColor = stage == UpdateStage.Failed ? Palette.Renk2Text : Palette.TextBody;

        _versionLabel.Text = Strings.Get("update.versionLabel");
        _version.Text = _controller.Release?.Tag ?? string.Empty;
        _downloadInstall.Text = Strings.Get("update.downloadInstall");
        _download.Text = Strings.Get("update.downloadButton");
        _cancel.Text = Strings.Get("update.cancel");
        _install.Text = Strings.Get("update.installButton");
        _close.Text = Strings.Get("update.close");

        var offer = stage is UpdateStage.Available or UpdateStage.Failed;
        _downloadInstall.Visible = offer;
        _download.Visible = offer;
        _cancel.Visible = stage == UpdateStage.Downloading;
        _install.Visible = stage == UpdateStage.Ready;
        _close.Visible = stage != UpdateStage.Restarting;

        _progressRow.Visible = stage is UpdateStage.Downloading or UpdateStage.Ready or UpdateStage.Restarting;
        switch (stage)
        {
            case UpdateStage.Downloading:
                _bar.Running = true;
                _bar.Target = _controller.Percent;
                _bar.Ceiling = _controller.Percent < 95 ? 95 : 100;
                break;
            case UpdateStage.Ready:
            case UpdateStage.Restarting:
                _bar.Running = false;
                _bar.Complete();
                break;
            default:
                _bar.Running = false;
                _bar.Reset();
                break;
        }

        _percent.ForeColor = stage is UpdateStage.Ready or UpdateStage.Restarting ? Palette.Success : Palette.Renk1;
        _percent.Text = "%" + Math.Floor(_bar.Displayed).ToString(CultureInfo.InvariantCulture);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller.Changed -= OnChanged;
        }

        base.Dispose(disposing);
    }
}
