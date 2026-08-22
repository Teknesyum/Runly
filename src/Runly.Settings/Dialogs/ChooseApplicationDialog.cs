using System.Drawing.Drawing2D;
using Runly.Core.Models;
using Runly.Core.Services;
using Runly.Settings.Discovery;

namespace Runly.Settings.Dialogs;

/// <summary>Per-extension application picker: suggested handlers first, live search, and a Browse
/// escape hatch. Replaces typing an absolute executable path into the grid's handler cell.</summary>
internal sealed class ChooseApplicationDialog : NeonForm
{
    private const int SearchDebounceMs = 180;
    private const int RowHeight = 48;
    private const int IconSize = 32;

    private static readonly Color ChipFill = Tint(Palette.NeonPink, 26);
    private static readonly Color ChipBorder = Tint(Palette.NeonPink, 150);
    private static readonly Color ChipGlow = Tint(Palette.NeonPink, 60);
    private static readonly Color RowSelected = Tint(Palette.NeonBlue, 34);

    private sealed record AppChoice(string DisplayName, string Path, bool Suggested);

    private readonly List<AppChoice> _all;
    private readonly Dictionary<string, Image?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBox _searchBox;
    private readonly System.Windows.Forms.Timer _searchDebounce;
    private readonly ListBox _list;
    private readonly Label _emptyLabel;

    /// <summary>Pre-blends an accent against the field background. A translucent brush over an
    /// owner-drawn list row leaves the previous frame showing through when the list scrolls.</summary>
    private static Color Tint(Color accent, int alpha) => Color.FromArgb(
        Palette.FieldBg.R + ((accent.R - Palette.FieldBg.R) * alpha / 255),
        Palette.FieldBg.G + ((accent.G - Palette.FieldBg.G) * alpha / 255),
        Palette.FieldBg.B + ((accent.B - Palette.FieldBg.B) * alpha / 255));

    public ChooseApplicationDialog(
        string extension,
        HandlerKind kind,
        IReadOnlyList<InstalledApplication> applications,
        IReadOnlyCollection<string> suggestedExecutables,
        string? currentPath)
    {
        var suggested = suggestedExecutables.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // The Where clause keeps Runly out of its own handler list: the launcher would hand the file
        // straight back to itself. ProcessLauncher catches that loop at run time and returns Recursive,
        // but by then the mapping is already saved and the user has no idea why nothing opens.
        _all = applications
            .Where(app => !ProcessLauncher.IsRunlyExecutable(app.Path))
            .GroupBy(app => app.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(app => new AppChoice(app.DisplayName, app.Path, suggested.Contains(app.ExecutableName)))
            .OrderByDescending(choice => choice.Suggested)
            .ThenBy(choice => choice.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Text = Strings.Get("chooseApp.title");
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(600, 560);
        MinimumSize = new Size(520, 460);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;
        KeyPreview = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var prompt = new Label
        {
            Dock = DockStyle.Fill,
            Text = Strings.Get(kind == HandlerKind.Run ? "chooseApp.promptRun" : "chooseApp.prompt")
                .Replace("{extension}", extension, StringComparison.Ordinal),
            ForeColor = Palette.TextStrong,
            Font = Palette.H3,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        layout.Controls.Add(prompt, 0, 0);

        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = Strings.Get("chooseApp.searchPlaceholder"),
            BackColor = Palette.FieldBg,
            ForeColor = Palette.NeonBlue,
            Font = Palette.MonoBody,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 8),
        };

        // Same reason as the main window: every keystroke rebuilt the whole owner-drawn list, and each
        // newly visible row pulls an icon out of the shell on first paint.
        _searchDebounce = new System.Windows.Forms.Timer { Interval = SearchDebounceMs };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            ApplyFilter();
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _searchDebounce.Stop();
            _searchDebounce.Start();
        };
        layout.Controls.Add(_searchBox, 0, 1);

        var listHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.FieldBg, Padding = new Padding(1) };
        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.FieldBg,
            ForeColor = Palette.TextBody,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = RowHeight,
            IntegralHeight = false,
        };
        _list.DrawItem += DrawApplicationItem;
        _list.DoubleClick += (_, _) => Accept();
        _emptyLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = Strings.Get("chooseApp.empty"),
            ForeColor = Palette.TextDim,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false,
        };
        listHost.Controls.Add(_list);
        listHost.Controls.Add(_emptyLabel);
        layout.Controls.Add(listHost, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 8, 0, 0),
        };
        var selectButton = new NeonButton { Text = Strings.Get("chooseApp.select"), Primary = true, BackColor = Palette.AppBg, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        var cancelButton = new NeonButton { Text = Strings.Get("cancel"), Primary = false, BackColor = Palette.AppBg, DialogResult = DialogResult.Cancel, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        var browseButton = new NeonButton { Text = Strings.Get("chooseApp.browse"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        selectButton.Click += (_, _) => Accept();
        browseButton.Click += (_, _) => Browse();
        buttons.Controls.Add(selectButton);
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(browseButton);
        layout.Controls.Add(buttons, 0, 3);

        Controls.Add(layout);
        AcceptButton = selectButton;
        CancelButton = cancelButton;

        ApplyFilter();
        SelectCurrent(currentPath);
        Shown += (_, _) => _searchBox.Focus();
        KeyDown += OnDialogKeyDown;
    }

    public string SelectedPath { get; private set; } = string.Empty;

    public string SelectedDisplayName { get; private set; } = string.Empty;

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is not (Keys.Down or Keys.Up) || !_searchBox.Focused || _list.Items.Count == 0)
        {
            return;
        }

        _list.Focus();
        _list.SelectedIndex = Math.Clamp(_list.SelectedIndex < 0 ? 0 : _list.SelectedIndex, 0, _list.Items.Count - 1);
        e.Handled = true;
    }

    private void ApplyFilter()
    {
        var query = _searchBox.Text.Trim();
        var matches = _all.Where(choice =>
            query.Length == 0 ||
            choice.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            choice.Path.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var match in matches) _list.Items.Add(match);
        _list.EndUpdate();

        _emptyLabel.Visible = matches.Length == 0;
        _list.Visible = matches.Length > 0;
        if (matches.Length > 0) _list.SelectedIndex = 0;
    }

    private void SelectCurrent(string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath)) return;
        for (var index = 0; index < _list.Items.Count; index++)
        {
            if (_list.Items[index] is AppChoice choice &&
                string.Equals(choice.Path, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                _list.SelectedIndex = index;
                return;
            }
        }
    }

    private void DrawApplicationItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _list.Items[e.Index] is not AppChoice choice)
        {
            return;
        }

        var g = e.Graphics;
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        using (var background = new SolidBrush(selected ? RowSelected : Palette.FieldBg))
        {
            g.FillRectangle(background, e.Bounds);
        }

        if (selected)
        {
            using var marker = new SolidBrush(Palette.NeonBlue);
            g.FillRectangle(marker, e.Bounds.Left, e.Bounds.Top + 8, 3, e.Bounds.Height - 16);
        }

        var iconBox = new Rectangle(e.Bounds.Left + 14, e.Bounds.Top + ((RowHeight - IconSize) / 2), IconSize, IconSize);
        DrawApplicationIcon(g, choice.Path, iconBox);

        var chipWidth = choice.Suggested ? MeasureChip(g) : 0;
        var textLeft = iconBox.Right + 12;
        var textWidth = Math.Max(40, e.Bounds.Right - textLeft - 12 - chipWidth);

        TextRenderer.DrawText(g, choice.DisplayName, Palette.Body,
            new Rectangle(textLeft, e.Bounds.Top + 6, textWidth, 20),
            selected ? Palette.NeonBlue : Palette.TextStrong,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(g, choice.Path, Palette.MonoBody,
            new Rectangle(textLeft, e.Bounds.Top + 26, textWidth, 17),
            Palette.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);

        if (choice.Suggested)
        {
            DrawSuggestedChip(g, new Rectangle(e.Bounds.Right - chipWidth - 12,
                e.Bounds.Top + ((RowHeight - 24) / 2), chipWidth, 24));
        }
    }

    private static int MeasureChip(Graphics g) =>
        TextRenderer.MeasureText(g, Strings.Get("chooseApp.suggested"), Palette.H3).Width + 22;

    /// <summary>Chip in the Teknesyum "Çip" role: 6px radius, pre-blended pink fill, pink outline and
    /// a one-pixel outer halo. The glow belongs to the box, never to the glyphs.</summary>
    private static void DrawSuggestedChip(Graphics g, Rectangle bounds)
    {
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var halo = NeonTheme.RoundedRect(Rectangle.Inflate(bounds, 1, 1), 7))
        using (var haloPen = new Pen(ChipGlow))
        {
            g.DrawPath(haloPen, halo);
        }

        using (var path = NeonTheme.RoundedRect(bounds, 6))
        using (var fill = new SolidBrush(ChipFill))
        using (var pen = new Pen(ChipBorder))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        g.SmoothingMode = previous;

        TextRenderer.DrawText(g, Strings.Get("chooseApp.suggested"), Palette.H3, bounds, Palette.NeonPink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void DrawApplicationIcon(Graphics g, string path, Rectangle bounds)
    {
        var image = ResolveIcon(path);
        if (image is null)
        {
            using var placeholder = new Pen(Tint(Palette.NeonBlue, 90));
            g.DrawRectangle(placeholder, bounds.Left + 4, bounds.Top + 4, bounds.Width - 9, bounds.Height - 9);
            return;
        }

        var previous = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(image, bounds);
        g.InterpolationMode = previous;
    }

    private Image? ResolveIcon(string path)
    {
        if (_iconCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        Image? image = null;
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            image = icon?.ToBitmap();
        }
        catch (ArgumentException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        _iconCache[path] = image;
        return image;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _searchDebounce.Dispose();

        foreach (var image in _iconCache.Values)
        {
            image?.Dispose();
        }

        _iconCache.Clear();
        base.OnFormClosed(e);
    }

    private void Browse()
    {
        using var picker = new OpenFileDialog
        {
            Filter = Strings.Get("chooseApp.filter"),
            CheckFileExists = true,
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        SelectedPath = picker.FileName;
        SelectedDisplayName = Path.GetFileNameWithoutExtension(picker.FileName);
        DialogResult = DialogResult.OK;
    }

    private void Accept()
    {
        if (_list.SelectedItem is not AppChoice choice)
        {
            NeonMessageBox.Show(this, Strings.Get("chooseApp.pickOne"), Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SelectedPath = choice.Path;
        SelectedDisplayName = choice.DisplayName;
        DialogResult = DialogResult.OK;
    }
}
