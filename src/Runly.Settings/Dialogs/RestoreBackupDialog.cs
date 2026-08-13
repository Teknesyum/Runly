using Runly.Core.Shell;

namespace Runly.Settings.Dialogs;

/// <summary>Lets the user pick a registry backup to replay, newest first (SPEC 10 §5, "Yedekten geri yükle").</summary>
internal sealed class RestoreBackupDialog : NeonForm
{
    private readonly ListView _list;

    /// <summary>Creates the dialog over an already-sorted (newest first) backup list.</summary>
    public RestoreBackupDialog(IReadOnlyList<BackupInfo> backups)
    {
        Text = "Yedekten geri yükle";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 320);
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(12);
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            BackColor = Palette.Surface,
            ForeColor = Palette.TextBody,
            Font = Palette.MonoBody,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _list.Columns.Add("Tarih", 160);
        _list.Columns.Add("Dosya", 240);
        _list.Columns.Add("Boyut", 90);

        foreach (var backup in backups)
        {
            var item = new ListViewItem(backup.CreatedUtc.ToLocalTime().ToString("g"))
            {
                Tag = backup,
            };
            item.SubItems.Add(backup.FileName);
            item.SubItems.Add($"{backup.SizeBytes / 1024.0:N1} KB");
            _list.Items.Add(item);
        }

        if (_list.Items.Count > 0)
        {
            _list.Items[0].Selected = true;
        }

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40,
            BackColor = Palette.AppBg,
        };

        var cancelButton = new NeonButton { Text = "Vazgeç", Primary = false, BackColor = Palette.AppBg, DialogResult = DialogResult.Cancel, AutoSize = true };
        var restoreButton = new NeonButton { Text = "Geri yükle", Primary = true, BackColor = Palette.AppBg, AutoSize = true };
        restoreButton.Click += OnRestoreClicked;

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(restoreButton);

        Controls.Add(_list);
        Controls.Add(buttonPanel);
        AcceptButton = restoreButton;
        CancelButton = cancelButton;
    }

    /// <summary>The backup the user selected; valid on <see cref="DialogResult.OK"/>.</summary>
    public BackupInfo? SelectedBackup { get; private set; }

    private void OnRestoreClicked(object? sender, EventArgs e)
    {
        if (_list.SelectedItems.Count == 0)
        {
            NeonMessageBox.Show(this, "Lütfen bir yedek seçin.", "Runly Ayarları",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SelectedBackup = (BackupInfo)_list.SelectedItems[0].Tag!;
        DialogResult = DialogResult.OK;
    }
}
