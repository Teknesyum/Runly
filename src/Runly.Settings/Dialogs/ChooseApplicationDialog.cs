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

    /// <summary>Shell icon edge. U1 replaces how the bitmap is fetched; changing it here is enough,
    /// because the row below is sized from it rather than from a second number that has to be kept in sync.</summary>
    private static int IconSize => Metrics.Px(32);

    /// <summary>The row carries the icon on one side and two stacked lines of text on the other, so it is
    /// the taller of the two plus a gutter. A literal here clipped the path line at 125% and 150%.</summary>
    private static int RowHeight =>
        Math.Max(IconSize, Metrics.Line(Palette.Body) + Metrics.Line(Palette.MonoBody)) + Metrics.Px(12);

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
        ClientSize = new Size(Metrics.Px(600), Metrics.Px(560));
        MinimumSize = new Size(Metrics.Px(520), Metrics.Px(460));
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
            Padding = new Padding(Metrics.Px(16), Metrics.Px(12), Metrics.Px(16), Metrics.Px(12)),
            BackColor = Color.Transparent,
        };
        // The prompt names the extension, so Turkish wraps where English does not: two lines are reserved.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, (Metrics.Line(Palette.H3) * 2) + Metrics.Px(14)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Metrics.TextBoxHeight + Metrics.Px(8)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Metrics.ButtonHeight + Metrics.Px(14)));

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

        _searchBox = new NeonTextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = Strings.Get("chooseApp.searchPlaceholder"),
            Margin = new Padding(0, 0, 0, Metrics.Px(8)),
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

        var listHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.FieldBg, Padding = new Padding(Metrics.Px(1)) };
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
            Padding = new Padding(0, Metrics.Px(8), 0, 0),
        };
        var selectButton = new NeonButton { Text = Strings.Get("chooseApp.select"), Primary = true, BackColor = Palette.AppBg, AutoSize = true, Margin = new Padding(Metrics.Px(8), 0, 0, 0) };
        var cancelButton = new NeonButton { Text = Strings.Get("cancel"), Primary = false, BackColor = Palette.AppBg, DialogResult = DialogResult.Cancel, AutoSize = true, Margin = new Padding(Metrics.Px(8), 0, 0, 0) };
        var browseButton = new NeonButton { Text = Strings.Get("chooseApp.browse"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = new Padding(Metrics.Px(8), 0, 0, 0) };
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

        var gutter = Metrics.Px(12);

        if (selected)
        {
            using var marker = new SolidBrush(Palette.NeonBlue);
            g.FillRectangle(marker, e.Bounds.Left, e.Bounds.Top + Metrics.Px(8), Metrics.Px(3), e.Bounds.Height - Metrics.Px(16));
        }

        var iconBox = new Rectangle(
            e.Bounds.Left + Metrics.Px(14),
            e.Bounds.Top + ((e.Bounds.Height - IconSize) / 2),
            IconSize,
            IconSize);
        DrawApplicationIcon(g, choice.Path, iconBox);

        var chipWidth = choice.Suggested ? MeasureChip(g) : 0;
        var textLeft = iconBox.Right + gutter;
        var textWidth = Math.Max(Metrics.Px(40), e.Bounds.Right - textLeft - gutter - chipWidth);

        // Both lines are laid out from the measured line heights and centred as a block, so the pair stays
        // inside the row whichever of the two fonts grows.
        var nameLine = Metrics.Line(Palette.Body);
        var pathLine = Metrics.Line(Palette.MonoBody);
        var textTop = e.Bounds.Top + ((e.Bounds.Height - nameLine - pathLine) / 2);

        TextRenderer.DrawText(g, choice.DisplayName, Palette.Body,
            new Rectangle(textLeft, textTop, textWidth, nameLine),
            selected ? Palette.NeonBlue : Palette.TextStrong,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(g, choice.Path, Palette.MonoBody,
            new Rectangle(textLeft, textTop + nameLine, textWidth, pathLine),
            Palette.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);

        if (choice.Suggested)
        {
            var chipHeight = Metrics.Row(Palette.H3, 5);
            DrawSuggestedChip(g, new Rectangle(e.Bounds.Right - chipWidth - gutter,
                e.Bounds.Top + ((e.Bounds.Height - chipHeight) / 2), chipWidth, chipHeight));
        }
    }

    private static int MeasureChip(Graphics g) =>
        TextRenderer.MeasureText(g, Strings.Get("chooseApp.suggested"), Palette.H3).Width + Metrics.Px(22);

    /// <summary>Chip in the Teknesyum "Çip" role: 6px radius, pre-blended pink fill, pink outline and
    /// a one-pixel outer halo. The glow belongs to the box, never to the glyphs.</summary>
    private static void DrawSuggestedChip(Graphics g, Rectangle bounds)
    {
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var radius = Metrics.Px(6);
        var haloOffset = Metrics.Px(1);

        using (var halo = NeonTheme.RoundedRect(Rectangle.Inflate(bounds, haloOffset, haloOffset), radius + haloOffset))
        using (var haloPen = new Pen(ChipGlow, Metrics.Scale))
        {
            g.DrawPath(haloPen, halo);
        }

        using (var path = NeonTheme.RoundedRect(bounds, radius))
        using (var fill = new SolidBrush(ChipFill))
        using (var pen = new Pen(ChipBorder, Metrics.Scale))
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
            var inset = Metrics.Px(4);
            using var placeholder = new Pen(Tint(Palette.NeonBlue, 90), Metrics.Scale);
            g.DrawRectangle(placeholder, bounds.Left + inset, bounds.Top + inset,
                bounds.Width - (inset * 2) - 1, bounds.Height - (inset * 2) - 1);
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
