namespace Runly.Settings.Dialogs;

/// <summary>Shows a line-by-line "what just happened" report after install/uninstall/restore (SPEC 9, SPEC 10 §5).</summary>
internal sealed class ResultDialog : NeonForm
{
    /// <summary>
    /// Builds and shows the dialog modally. <paramref name="warningHeadline"/> replaces the green
    /// "✅ İşlem tamamlandı." line: an operation can succeed and still leave something the user must finish or
    /// clean up, and decisions K19/K20 forbid presenting those runs as if nothing were left to do.
    /// </summary>
    public static void Show(
        IWin32Window owner,
        string title,
        bool success,
        IReadOnlyList<string> lines,
        string? errorMessage,
        string? warningHeadline = null)
    {
        using var dialog = new ResultDialog(title, success, lines, errorMessage, warningHeadline);
        dialog.ShowDialog(owner);
    }

    private ResultDialog(string title, bool success, IReadOnlyList<string> lines, string? errorMessage, string? warningHeadline)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        ClientSize = new Size(Metrics.Px(540), Metrics.Px(360));
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(Metrics.Px(12));
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;

        var warned = success && !string.IsNullOrWhiteSpace(warningHeadline);

        var headline = new Label
        {
            Dock = DockStyle.Top,
            Height = Metrics.Row(Palette.H2, 19),
            Font = Palette.H2,
            Text = success
                ? warned ? "⚠ " + warningHeadline : "✅ İşlem tamamlandı."
                : "❌ İşlem başarısız oldu.",
            ForeColor = success
                ? warned ? Palette.NeonPink : Palette.Success
                : Palette.NeonPink,
        };

        var textBox = new NeonTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Palette.Surface,
            ForeColor = Palette.TextBody,
            Text = BuildText(lines, errorMessage),
        };

        var closeButton = new NeonButton
        {
            Text = "Kapat",
            BackColor = Palette.AppBg,
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = Metrics.ButtonHeight + Metrics.Px(8),
            BackColor = Palette.AppBg,
        };
        buttonPanel.Controls.Add(closeButton);

        Controls.Add(textBox);
        Controls.Add(headline);
        Controls.Add(buttonPanel);
        AcceptButton = closeButton;
        CancelButton = closeButton;

        Shown += (_, _) =>
        {
            textBox.SelectionLength = 0;
            closeButton.Focus();
        };
    }

    private static string BuildText(IReadOnlyList<string> lines, string? errorMessage)
    {
        var text = string.Join(Environment.NewLine, lines);
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            text += Environment.NewLine + Environment.NewLine + "Hata: " + errorMessage;
        }

        return text.Length == 0 ? "(kayıtlı işlem yok)" : text;
    }
}
