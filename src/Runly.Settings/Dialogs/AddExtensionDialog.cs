using Runly.Core.Defaults;
using Runly.Core.Models;

namespace Runly.Settings.Dialogs;

/// <summary>Small modal that collects a new extension mapping for the "Uzantı ekle" button (SPEC 10.2).</summary>
internal sealed class AddExtensionDialog : NeonForm
{
    private readonly TextBox _extensionBox;
    private readonly TextBox _interpreterBox;
    private readonly TextBox _argsBox;

    /// <summary>Creates the dialog; call <see cref="Form.ShowDialog()"/> and read <see cref="Extension"/> etc. on <see cref="DialogResult.OK"/>.</summary>
    public AddExtensionDialog()
    {
        Text = "Uzantı ekle";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 170);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12),
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _extensionBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = ".rb", BackColor = Palette.FieldBg, ForeColor = Palette.NeonBlue, Font = Palette.MonoBody, BorderStyle = BorderStyle.FixedSingle };
        _interpreterBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "ruby", BackColor = Palette.FieldBg, ForeColor = Palette.NeonBlue, Font = Palette.MonoBody, BorderStyle = BorderStyle.FixedSingle };
        _argsBox = new TextBox { Dock = DockStyle.Fill, Text = DefaultConfig.ScriptThenArgs, BackColor = Palette.FieldBg, ForeColor = Palette.NeonBlue, Font = Palette.MonoBody, BorderStyle = BorderStyle.FixedSingle };

        layout.Controls.Add(new Label { Text = "Uzantı:", ForeColor = Palette.TextDim, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(_extensionBox, 1, 0);
        layout.Controls.Add(new Label { Text = "Yorumlayıcı:", ForeColor = Palette.TextDim, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        layout.Controls.Add(_interpreterBox, 1, 1);
        layout.Controls.Add(new Label { Text = "Argümanlar:", ForeColor = Palette.TextDim, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        layout.Controls.Add(_argsBox, 1, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.Transparent,
        };

        var cancelButton = new NeonButton { Text = "Vazgeç", Primary = false, BackColor = Palette.AppBg, DialogResult = DialogResult.Cancel, AutoSize = true };
        var okButton = new NeonButton { Text = "Ekle", Primary = true, BackColor = Palette.AppBg, AutoSize = true };
        okButton.Click += OnAddClicked;

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(okButton);
        layout.Controls.Add(buttonPanel, 1, 3);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    /// <summary>Normalised extension (with leading dot) entered by the user.</summary>
    public string Extension { get; private set; } = string.Empty;

    /// <summary>Interpreter name or path entered by the user.</summary>
    public string Interpreter { get; private set; } = string.Empty;

    /// <summary>Argument template entered by the user.</summary>
    public string Args { get; private set; } = string.Empty;

    private void OnAddClicked(object? sender, EventArgs e)
    {
        var extension = RunlyConfig.NormalizeExtension(_extensionBox.Text);

        if (extension.Length <= 1)
        {
            NeonMessageBox.Show(this, "Geçerli bir uzantı girin (örnek: .rb).", "Runly Ayarları",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Extension = extension;
        Interpreter = _interpreterBox.Text.Trim();
        Args = string.IsNullOrWhiteSpace(_argsBox.Text) ? DefaultConfig.ScriptThenArgs : _argsBox.Text;
        DialogResult = DialogResult.OK;
    }
}
