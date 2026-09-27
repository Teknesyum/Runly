using System.ComponentModel;
using System.Diagnostics;
using Runly.Core.Models;
using Runly.Core.Shell;

namespace Runly.Settings.Dialogs;

/// <summary>
/// The right-click menu preview: shows what a script type's menu will look like, and lets the user toggle
/// every entry Runly can reach. An entry Runly cannot manage keeps its place in the list with a way to the
/// program that owns it, so the preview never lies about what the menu holds.
/// </summary>
internal sealed class ContextMenuDialog : NeonForm
{
    private readonly List<(ContextMenuItem Item, NeonCheckBox? Hide, NeonCheckBox? Everywhere)> _rows = [];
    private readonly Dictionary<string, bool?> _admin = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _extensions;
    private readonly NeonComboBox _extensionBox = new();
    private readonly NeonComboBox _adminBox = new();
    private readonly FlowLayoutPanel _preview;
    private readonly RunlyConfig _config;
    private bool _loading = true;

    public ContextMenuDialog(
        IReadOnlyList<ContextMenuItem> items,
        IReadOnlySet<string> selected,
        IReadOnlySet<string> everywhere,
        RunlyConfig config)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(everywhere);
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _extensions = ContextMenuCleaner.RunExtensions(config).ToList();
        foreach (var extension in _extensions)
        {
            _admin[extension] = config.Extensions.TryGetValue(extension, out var mapping) ? mapping.RunAsAdmin : null;
        }

        Text = Strings.Get("menu.title");
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        ClientSize = new Size(Metrics.Px(720), Metrics.Px(620));
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(Metrics.Px(TeknesyumTokens.Space4));
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;

        var contentWidth = DisplayRectangle.Width;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Color.Transparent };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = Strings.Get("menu.intro"),
            AutoSize = true,
            ForeColor = Palette.TextBody,
            MaximumSize = new Size(contentWidth, 0),
            Margin = new Padding(0, 0, 0, Metrics.Px(TeknesyumTokens.Space2)),
        }, 0, 0);

        layout.Controls.Add(BuildHeader(), 0, 1);
        _preview = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Palette.Surface,
            Padding = new Padding(Metrics.Px(TeknesyumTokens.Space3), Metrics.Px(TeknesyumTokens.Space2), Metrics.Px(TeknesyumTokens.Space3), Metrics.Px(TeknesyumTokens.Space2)),
            Margin = new Padding(0, Metrics.Px(TeknesyumTokens.Space2), 0, Metrics.Px(TeknesyumTokens.Space2)),
            Width = contentWidth,
            MinimumSize = new Size(contentWidth, 0),
            MaximumSize = new Size(contentWidth, 0),
        };
        layout.Controls.Add(_preview, 0, 2);

        var list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Palette.AppBg,
            Margin = Padding.Empty,
        };
        var rowWidth = contentWidth - SystemInformation.VerticalScrollBarWidth - Metrics.Px(TeknesyumTokens.Space2);

        if (items.Count == 0)
        {
            list.Controls.Add(new Label { Text = Strings.Get("menu.empty"), AutoSize = true, ForeColor = Palette.TextDim, MaximumSize = new Size(rowWidth, 0) });
        }

        foreach (var item in items)
        {
            AddRow(list, item, selected, everywhere, rowWidth);
        }

        layout.Controls.Add(list, 0, 3);

        layout.Controls.Add(new Label
        {
            Text = Strings.Get("menu.elsewhere"),
            AutoSize = true,
            ForeColor = Palette.TextDim,
            MaximumSize = new Size(contentWidth, 0),
            Margin = new Padding(0, Metrics.Px(TeknesyumTokens.Space2), 0, 0),
        }, 0, 4);

        layout.Controls.Add(BuildButtons(), 0, 5);
        Controls.Add(layout);

        _loading = false;
        RefreshPreview();
    }

    /// <summary>Entries the user wants hidden.</summary>
    public IReadOnlyList<string> SelectedIds => _rows.Where(r => r.Hide?.Checked == true).Select(r => r.Item.Id).ToList();

    /// <summary>Entries the user wants gone from every file type, not only Runly's (K32).</summary>
    public IReadOnlyList<string> EverywhereIds => _rows
        .Where(r => r.Hide?.Checked == true && r.Everywhere?.Checked == true)
        .Select(r => r.Item.Id)
        .ToList();

    /// <summary>Per-extension elevation choice; <see langword="null"/> means "follow the global setting".</summary>
    public IReadOnlyDictionary<string, bool?> AdminOverrides => _admin;

    private Control BuildHeader()
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };

        row.Controls.Add(new Label
        {
            Text = Strings.Get("menu.previewFor"),
            AutoSize = true,
            ForeColor = Palette.TextDim,
            Margin = new Padding(0, Metrics.Px(TeknesyumTokens.Space2), Metrics.Px(TeknesyumTokens.Space2), 0),
        });

        _extensionBox.Width = Metrics.Px(110);
        _extensionBox.Margin = new Padding(0, 0, Metrics.Px(TeknesyumTokens.Space4), 0);
        foreach (var extension in _extensions)
        {
            _extensionBox.Items.Add(extension);
        }

        if (_extensionBox.Items.Count > 0)
        {
            _extensionBox.SelectedIndex = 0;
        }

        _extensionBox.SelectedIndexChanged += (_, _) =>
        {
            LoadAdminBox();
            RefreshPreview();
        };
        row.Controls.Add(_extensionBox);

        row.Controls.Add(new Label
        {
            Text = Strings.Get("menu.admin"),
            AutoSize = true,
            ForeColor = Palette.TextDim,
            Margin = new Padding(0, Metrics.Px(TeknesyumTokens.Space2), Metrics.Px(TeknesyumTokens.Space2), 0),
        });

        _adminBox.Width = Metrics.Px(220);
        _adminBox.Items.AddRange([
            Strings.Get("menu.adminDefault").Replace("{state}", Strings.Get(_config.RunAsAdmin ? "menu.adminOn" : "menu.adminOff"), StringComparison.Ordinal),
            Strings.Get("menu.adminOn"),
            Strings.Get("menu.adminOff"),
        ]);
        _adminBox.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _extensionBox.SelectedItem is not string extension)
            {
                return;
            }

            _admin[extension] = _adminBox.SelectedIndex switch { 1 => true, 2 => false, _ => null };
            RefreshPreview();
        };
        row.Controls.Add(_adminBox);

        LoadAdminBox();
        return row;
    }

    private void LoadAdminBox()
    {
        var wasLoading = _loading;
        _loading = true;
        var extension = _extensionBox.SelectedItem as string;
        var choice = extension is not null && _admin.TryGetValue(extension, out var value) ? value : null;
        _adminBox.SelectedIndex = choice switch { true => 1, false => 2, _ => 0 };
        _adminBox.Enabled = extension is not null;
        _loading = wasLoading;
    }

    private void AddRow(
        FlowLayoutPanel list,
        ContextMenuItem item,
        IReadOnlySet<string> selected,
        IReadOnlySet<string> everywhere,
        int rowWidth)
    {
        NeonCheckBox? hide = null;
        NeonCheckBox? everywhereCheck = null;

        if (item.Manageable)
        {
            hide = new NeonCheckBox
            {
                Text = item.Label,
                Checked = selected.Contains(item.Id),
                BackColor = Palette.AppBg,
                Margin = new Padding(0, Metrics.Px(TeknesyumTokens.Space2), 0, 0),
            };
            hide.CheckedChanged += (_, _) =>
            {
                if (everywhereCheck is not null)
                {
                    everywhereCheck.Enabled = hide.Checked;
                }

                RefreshPreview();
            };
            list.Controls.Add(hide);
        }
        else
        {
            list.Controls.Add(new Label
            {
                Text = item.Label,
                AutoSize = true,
                ForeColor = Palette.TextBody,
                MaximumSize = new Size(rowWidth, 0),
                Margin = new Padding(0, Metrics.Px(TeknesyumTokens.Space2), 0, 0),
            });
        }

        list.Controls.Add(new Label
        {
            Text = Describe(item),
            AutoSize = true,
            ForeColor = Palette.TextDim,
            MaximumSize = new Size(rowWidth - Metrics.Px(28), 0),
            Margin = new Padding(Metrics.Px(28), 0, 0, Metrics.Px(TeknesyumTokens.Space1)),
        });

        if (item.Manageable && item.Method == MenuHideMethod.AppliesTo)
        {
            everywhereCheck = new NeonCheckBox
            {
                Text = Strings.Get("menu.everywhere"),
                Checked = everywhere.Contains(item.Id),
                Enabled = hide?.Checked == true,
                BackColor = Palette.AppBg,
                ForeColor = Palette.TextDim,
                Margin = new Padding(Metrics.Px(28), 0, 0, Metrics.Px(TeknesyumTokens.Space2)),
            };
            list.Controls.Add(everywhereCheck);
        }

        if (!item.Manageable && !string.IsNullOrWhiteSpace(item.ConfigureTarget))
        {
            var target = item.ConfigureTarget!;
            var button = new NeonButton
            {
                Text = Strings.Get("menu.configure"),
                Primary = false,
                BackColor = Palette.AppBg,
                AutoSize = true,
                Margin = new Padding(Metrics.Px(28), 0, 0, Metrics.Px(TeknesyumTokens.Space2)),
            };
            button.Click += (_, _) => Open(target);
            list.Controls.Add(button);
        }

        _rows.Add((item, hide, everywhereCheck));
    }

    private Control BuildButtons()
    {
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, Metrics.Px(TeknesyumTokens.Space2), 0, 0),
            BackColor = Color.Transparent,
        };
        var gap = new Padding(Metrics.Px(TeknesyumTokens.Space3), 0, 0, 0);

        var cancel = new NeonButton { Text = Strings.Get("cancel"), Primary = false, BackColor = Palette.AppBg, DialogResult = DialogResult.Cancel, AutoSize = true, Margin = gap };
        var apply = new NeonButton { Text = Strings.Get("menu.apply"), Primary = true, BackColor = Palette.AppBg, DialogResult = DialogResult.OK, AutoSize = true, Margin = gap };
        var recommended = new NeonButton { Text = Strings.Get("menu.recommended"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = gap };
        var apps = new NeonButton { Text = Strings.Get("menu.installedApps"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = gap };

        recommended.Click += (_, _) =>
        {
            foreach (var (item, check, _) in _rows)
            {
                if (check is not null)
                {
                    check.Checked = item.Recommended;
                }
            }

            RefreshPreview();
        };
        apps.Click += (_, _) => Open("ms-settings:appsfeatures");

        buttons.Controls.Add(cancel);
        buttons.Controls.Add(apply);
        buttons.Controls.Add(recommended);
        buttons.Controls.Add(apps);

        AcceptButton = apply;
        CancelButton = cancel;
        return buttons;
    }

    private void RefreshPreview()
    {
        if (_loading)
        {
            return;
        }

        _preview.SuspendLayout();
        foreach (Control control in _preview.Controls)
        {
            control.Dispose();
        }

        _preview.Controls.Clear();

        var extension = _extensionBox.SelectedItem as string;
        if (extension is null)
        {
            _preview.Controls.Add(Line(Strings.Get("menu.noTypes"), dim: true));
            _preview.ResumeLayout();
            return;
        }

        var elevated = (_admin.TryGetValue(extension, out var choice) ? choice : null) ?? _config.RunAsAdmin;
        _preview.Controls.Add(Line(Strings.Get(elevated ? "menu.runVerbAdmin" : "menu.runVerb"), dim: false));
        _preview.Controls.Add(Line(EditorName.EditVerbLabel(_config.EditorCommand), dim: false));

        foreach (var (item, hide, _) in _rows)
        {
            if (item.Extensions is { } extensions && !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var hidden = hide?.Checked == true;
            _preview.Controls.Add(Line(hidden ? item.Label + "  — " + Strings.Get("menu.previewHidden") : item.Label, hidden));
        }

        _preview.ResumeLayout();
    }

    private Label Line(string text, bool dim) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = dim ? Palette.TextDim : Palette.TextBody,
        BackColor = Palette.Surface,
        MaximumSize = new Size(_preview.MinimumSize.Width - _preview.Padding.Horizontal, 0),
        Margin = new Padding(0, Metrics.Px(2), 0, Metrics.Px(2)),
    };

    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            NeonMessageBox.Show(null, $"'{target}' açılamadı: {ex.Message}", Strings.Get("menu.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string Describe(ContextMenuItem item)
    {
        var scope = item.Method == MenuHideMethod.AppliesTo
            ? Strings.Get("menu.scopeRunly").Replace("{extensions}", string.Join(", ", item.Extensions ?? []), StringComparison.Ordinal)
            : item.Extensions is null
                ? Strings.Get("menu.scopeAll")
                : Strings.Get("menu.scopeTypes").Replace("{extensions}", string.Join(", ", item.Extensions), StringComparison.Ordinal);

        var parts = new List<string> { scope };
        if (item.Recommended)
        {
            parts.Add(Strings.Get("menu.recommendedTag"));
        }

        if (item.Hidden)
        {
            parts.Add(Strings.Get("menu.hiddenTag"));
        }

        if (!item.Manageable)
        {
            parts.Add(Strings.Get("menu.unmanaged"));
        }

        parts.Add(item.Source);
        if (!string.IsNullOrWhiteSpace(item.Note))
        {
            parts.Add(item.Note!);
        }

        return string.Join(" · ", parts);
    }
}
