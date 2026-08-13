using System.Drawing.Drawing2D;

namespace Runly.Settings;

/// <summary>A theme-native replacement for the bright system MessageBox.</summary>
internal static class NeonMessageBox
{
    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons, MessageBoxIcon icon) =>
        Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        using var dialog = new NeonMessageDialog(text, caption, buttons, icon, defaultButton);
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}

internal sealed class NeonMessageDialog : NeonForm
{
    private readonly MessageBoxIcon _icon;

    public NeonMessageDialog(string message, string caption, MessageBoxButtons buttons,
        MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        _icon = icon;
        Text = Strings.Translate(caption);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;
        Padding = new Padding(20);
        ClientSize = MeasureDialog(message);

        var card = new NeonGroupPanel(CaptionFor(icon)) { Dock = DockStyle.Fill };
        card.Padding = new Padding(70, 42, 20, 18);

        var messageLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = Strings.Translate(message),
            ForeColor = Palette.TextBody,
            Font = Palette.Body,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 3, 0, 0),
        };

        var iconBadge = new IconBadge(icon)
        {
            Location = new Point(18, 46),
            Size = new Size(36, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Palette.Surface,
        };

        var definitions = ButtonDefinitions(buttons);
        NeonButton? defaultControl = null;
        NeonButton? cancelControl = null;
        for (var i = definitions.Length - 1; i >= 0; i--)
        {
            var definition = definitions[i];
            var button = new NeonButton
            {
                Text = definition.Text,
                DialogResult = definition.Result,
                Primary = i == DefaultIndex(defaultButton, definitions.Length),
                BackColor = Palette.Surface,
                AutoSize = true,
                Margin = new Padding(8, 0, 0, 0),
            };
            buttonBar.Controls.Add(button);
            if (button.Primary) defaultControl = button;
            if (definition.Result is DialogResult.Cancel or DialogResult.No) cancelControl ??= button;
        }

        card.Controls.Add(messageLabel);
        card.Controls.Add(iconBadge);
        card.Controls.Add(buttonBar);
        Controls.Add(card);
        AcceptButton = defaultControl;
        CancelButton = cancelControl;
        Shown += (_, _) => defaultControl?.Focus();
    }

    private static Size MeasureDialog(string message)
    {
        var width = Math.Clamp(TextRenderer.MeasureText(message, Palette.Body, new Size(520, 0),
            TextFormatFlags.WordBreak).Width + 150, 430, 620);
        var textHeight = TextRenderer.MeasureText(message, Palette.Body, new Size(width - 150, 0),
            TextFormatFlags.WordBreak).Height;
        return new Size(width, Math.Clamp(textHeight + 210, 270, 490));
    }

    private static string CaptionFor(MessageBoxIcon icon) => icon switch
    {
        MessageBoxIcon.Error => "HATA",
        MessageBoxIcon.Warning => "DİKKAT",
        MessageBoxIcon.Question => "ONAY",
        _ => "BİLGİ",
    };

    private static (string Text, DialogResult Result)[] ButtonDefinitions(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.YesNo => [("Evet", DialogResult.Yes), ("Hayır", DialogResult.No)],
        MessageBoxButtons.YesNoCancel => [("Evet", DialogResult.Yes), ("Hayır", DialogResult.No), ("Vazgeç", DialogResult.Cancel)],
        MessageBoxButtons.OKCancel => [("Tamam", DialogResult.OK), ("Vazgeç", DialogResult.Cancel)],
        MessageBoxButtons.RetryCancel => [("Yeniden dene", DialogResult.Retry), ("Vazgeç", DialogResult.Cancel)],
        _ => [("Tamam", DialogResult.OK)],
    };

    private static int DefaultIndex(MessageBoxDefaultButton button, int count) =>
        Math.Min(button switch
        {
            MessageBoxDefaultButton.Button2 => 1,
            MessageBoxDefaultButton.Button3 => 2,
            _ => 0,
        }, count - 1);

    private sealed class IconBadge : Control
    {
        private readonly MessageBoxIcon _kind;

        public IconBadge(MessageBoxIcon kind)
        {
            _kind = kind;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Palette.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var color = _kind switch
            {
                MessageBoxIcon.Error or MessageBoxIcon.Warning => Palette.NeonPink,
                MessageBoxIcon.Question => Palette.NeonPurple,
                _ => Palette.NeonBlue,
            };
            using var glow = new SolidBrush(Color.FromArgb(32, color));
            using var ring = new Pen(color, 2f);
            e.Graphics.FillEllipse(glow, 2, 2, 31, 31);
            e.Graphics.DrawEllipse(ring, 2, 2, 31, 31);
            var glyph = _kind switch
            {
                MessageBoxIcon.Error => "×",
                MessageBoxIcon.Warning => "!",
                MessageBoxIcon.Question => "?",
                _ => "i",
            };
            TextRenderer.DrawText(e.Graphics, glyph, Palette.H2, ClientRectangle, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
