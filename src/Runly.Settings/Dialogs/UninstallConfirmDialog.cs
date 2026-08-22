namespace Runly.Settings.Dialogs;

/// <summary>Confirmation for the "Kaldır" button, including the "Yedeği de geri yükle" choice (SPEC 10 §5, "Kaldırma").</summary>
internal sealed class UninstallConfirmDialog : NeonForm
{
    private readonly CheckBox _restoreBackupCheck;

    /// <summary>Creates the dialog; call <see cref="Form.ShowDialog()"/> and read <see cref="RestoreBackup"/> on <see cref="DialogResult.Yes"/>.</summary>
    public UninstallConfirmDialog()
    {
        Text = "Runly'yi kaldır";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        ClientSize = new Size(Metrics.Px(420), Metrics.Px(220));
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(Metrics.Px(16));
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var contentWidth = ClientSize.Width - Padding.Horizontal;

        var question = new Label
        {
            Text = "Runly'yi sistemden kaldırmak istediğinize emin misiniz? Bu, Runly'nin yazdığı tüm dosya ilişkilerini siler.",
            AutoSize = true,
            ForeColor = Palette.TextBody,
            MaximumSize = new Size(contentWidth, 0),
            Margin = new Padding(0, 0, 0, Metrics.Px(8)),
        };

        _restoreBackupCheck = new NeonCheckBox
        {
            Text = "Yedeği de geri yükle",
            BackColor = Palette.AppBg,
            Checked = false,
            AutoSize = true,
        };

        var explanation = new Label
        {
            Text = "Kapalı bırakırsanız bu uzantılar ilişkisiz kalır — \".js\" eski \"WScript\" davranışına dönmez.",
            ForeColor = Palette.TextDim,
            AutoSize = true,
            MaximumSize = new Size(contentWidth, 0),
            Margin = new Padding(0, Metrics.Px(8), 0, 0),
        };

        layout.Controls.Add(question, 0, 0);
        layout.Controls.Add(_restoreBackupCheck, 0, 1);
        layout.Controls.Add(explanation, 0, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(0, Metrics.Px(10), 0, 0),
            BackColor = Color.Transparent,
        };

        var cancelButton = new NeonButton { Text = "Vazgeç", Primary = false, BackColor = Palette.AppBg, DialogResult = DialogResult.Cancel, AutoSize = true };
        var removeButton = new NeonButton { Text = "Kaldır", Primary = true, BackColor = Palette.AppBg, DialogResult = DialogResult.Yes, AutoSize = true };

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(removeButton);
        layout.Controls.Add(buttonPanel, 0, 3);

        Controls.Add(layout);
        AcceptButton = removeButton;
        CancelButton = cancelButton;
    }

    /// <summary>Whether the user asked to also replay the most recent registry backup.</summary>
    public bool RestoreBackup => _restoreBackupCheck.Checked;
}
