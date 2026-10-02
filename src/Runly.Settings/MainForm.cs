using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using Runly.Core.Abstractions;
using Runly.Core.Defaults;
using Runly.Core.Models;
using Runly.Core.Paths;
using Runly.Core.Shell;
using Runly.Core.Services;
using System.Text.RegularExpressions;
using Runly.Settings.Dialogs;
using Runly.Settings.Catalog;
using Runly.Settings.Discovery;

namespace Runly.Settings;

/// <summary>The single settings window: status strip, extension table, security/behavior panels, bottom bar (SPEC 10).</summary>
internal sealed partial class MainForm : NeonForm
{
    [GeneratedRegex(@"\b[A-Za-z0-9_.-]+\.(?:exe|dll)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HostExecutable();

    private const int ColEnabled = 0;
    private const int ColExtension = 1;
    private const int ColKind = 2;
    private const int ColInterpreter = 3;
    private const int ColFound = 4;
    private const int ColArgs = 5;
    private const int ColStatus = 6;

    private const int SearchDebounceMs = 180;

    // DataGridView hücre dolgusu alfa kanalını yok sayar: yarı saydam bir BackColor beyaza dönüp
    // satırı bozar. Tint'ler bu yüzden yüzey rengiyle önceden karıştırılıp opak veriliyor.
    private static readonly Color BoundBack = Tint(Palette.Success, 40);
    private static readonly Color BoundFore = Palette.TextBody;
    // "Windows onayı bekliyor" bir uyarıdır, dikkat çeken bir marka vurgusu değil: pembeyle aynı rengi
    // taşıyınca ikisi ayırt edilemiyordu. Amber yalnız uyarı yüzeyinde durur — metin, çerçeve, ikon.
    private static readonly Color NeedsChoiceBack = Palette.Surface;
    private static readonly Color NeedsChoiceFore = Palette.Warning;
    private static readonly Color NotBoundBack = Palette.FieldBg;
    private static readonly Color NotBoundFore = Palette.TextHint;

    private static Color Tint(Color accent, int alpha) => Color.FromArgb(
        Palette.Surface.R + ((accent.R - Palette.Surface.R) * alpha / 255),
        Palette.Surface.G + ((accent.G - Palette.Surface.G) * alpha / 255),
        Palette.Surface.B + ((accent.B - Palette.Surface.B) * alpha / 255));

    /// <summary>
    /// Section label in the Teknesyum "Etiket" role: small, bold, uppercase, letter-spaced, dim.
    /// WinForms has no letter-spacing property, so the spacing is baked into the text.
    /// </summary>
    /// <summary>Row label of a settings panel: body size, not the small bold caption it used to be, which
    /// read weakly in renk-1 on black. Rows holding a stack (radios, the folder list) pin it to the top so it
    /// reads with the first line; single-line rows centre it.</summary>
    private static Label FieldLabel(string key, bool top = false) => new()
    {
        Text = Strings.Get(key),
        AutoSize = true,
        Font = Palette.Body,
        ForeColor = Palette.TextLabel,
        Anchor = top ? AnchorStyles.Top | AnchorStyles.Left : AnchorStyles.Left,
        Margin = Padding.Empty,
    };

    /// <summary>The shared panel grid: label column, field column taking the slack, and a column for the
    /// field's own buttons.</summary>
    private static TableLayoutPanel FieldGrid(params int[] rowHeights)
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = rowHeights.Length + 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PanelLabelWidth));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var height in rowHeights)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return grid;
    }

    // Absolute rows only exist where AutoSize has already misjudged the content once (see the comments at
    // each use). They stay absolute, but the number is composed from what has to fit, so a taller font
    // grows the slot instead of clipping inside it.
    private static int FolderButtonHeight => Metrics.ButtonMinHeight;

    private static int FolderButtonWidth => Metrics.Px(78);

    /// Three stacked radios; 72 design pixels fitted only two and painted the folders label over the third.
    private static int RadioStackHeight => Metrics.Stack(Palette.Body, 3, 13);

    /// Two stacked folder buttons plus the margin between them; one button used to be cut in half here.
    private static int FoldersAreaHeight => (FolderButtonHeight * 2) + Metrics.Px(TeknesyumTokens.Space4);

    private static int TrustedFilesRowHeight => Metrics.ButtonHeight + Metrics.Px(TeknesyumTokens.Space1);

    private static int EditorRowHeight => Metrics.ButtonHeight + Metrics.Px(TeknesyumTokens.Space3);

    /// The same band as the action row under the table, so the table sits between two equal strips; the
    /// controls are centred in it rather than pushed down by hand-tuned top margins.
    private static int SearchStripHeight => ExtensionButtonsHeight;

    private static int ExtensionButtonsHeight => Metrics.ButtonHeight + Metrics.Px(TeknesyumTokens.Space4);

    /// The security panel is the taller of the two, so it sets the row both of them share.
    /// The security panel is the taller of the two, so it sets the row both of them share. The leading
    /// term is the gap above the pair, which belongs to the row as well: the panels are docked into it,
    /// so padding the container without paying for it here just eats the panel's own bottom padding.
    private static int PanelsRowHeight =>
        Metrics.Px(TeknesyumTokens.Space5) + Metrics.GroupTitleBand + Metrics.Px(TeknesyumTokens.Space4) + RadioStackHeight +
        Math.Max(FoldersAreaHeight + TrustedFilesRowHeight, EditorRowHeight + (TrustedFilesRowHeight * 2)) + Metrics.Px(TeknesyumTokens.Space5);

    /// Keys of every row label in the two settings panels. Both panels share one label column sized to
    /// the longest of these in any language, so their fields start on the same vertical line.
    private static readonly string[] PanelLabelKeys = ["security.ask", "trustedFolders", "trustedFiles", "windowOpen", "editorCommand", "logging", "elevation"];

    private static int PanelLabelWidth =>
        Strings.Languages.SelectMany(language => PanelLabelKeys.Select(key => TextRenderer.MeasureText(Strings.GetIn(language, key), Palette.Body).Width)).Max()
        + Metrics.Px(TeknesyumTokens.Space3);

    /// One button row inside the strip's 16/16 padding. The footer that used to sit under it moved into
    /// the caption band, so nothing else shares this row any more.
    private static int BottomBarHeight => Metrics.ButtonHeight + Metrics.Px(32);

    private readonly IConfigStore _configStore;
    private readonly ITrustStore _trustStore;
    private readonly IShellRegistrar _shellRegistrar;
    private readonly RegistryBackup _registryBackup;
    private readonly ContextMenuCleaner _menuCleaner;
    private readonly ILogger _logger;
    private RunlyConfig _config;

    private bool _dirty;
    private bool _initializing = true;
    private bool _suppressGridEvents;
    private bool _autoRefreshInFlight;
    private DateTime _lastAutoRefresh = DateTime.MinValue;

    /// <summary>Suggested handler per extension. <see cref="UsageHistory.Rank"/> opens registry keys,
    /// which a 408-row refresh cannot afford once per row per refresh; the grid asks this cache and the
    /// cache asks the registry at most once per extension.</summary>
    private readonly Dictionary<string, string?> _suggestedHandlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>B6: the config file's timestamp when this window last read or wrote it. A newer stamp
    /// on disk means someone edited the file behind our back, and saving would silently revert them.</summary>
    private DateTime _configStamp = DateTime.MinValue;

    private readonly DataGridView _grid;
    private readonly ListBox _categoryList;
    private readonly ColumnStyle _categoryRailColumn;
    private readonly TextBox _searchBox;
    private readonly Label _searchResultLabel;
    private readonly System.Windows.Forms.Timer _searchDebounce;
    private CancellationTokenSource? _searchScan;

    /// <summary>The last registry scan, kept so a keystroke can redraw the grid without re-reading the
    /// registry: typing cannot change a binding. Every other refresh path rebuilds it from scratch.</summary>
    private readonly Dictionary<string, ExtensionStatus> _statusSnapshot = new(StringComparer.OrdinalIgnoreCase);
    private bool _reuseStatusSnapshot;
    private readonly ComboBox _bulkAppBox;
    private readonly IReadOnlyList<InstalledApplication> _installedApplications;
    private readonly Dictionary<string, Icon> _categoryIcons = new(StringComparer.Ordinal);
    private readonly Label _statusLabel;
    private readonly Label _exePathLabel;
    private readonly LinkLabel _configPathLink;
    private readonly Button _refreshButton;
    private readonly RichTextBox _detailText;
    private readonly Button _detailAskButton;
    private readonly Button _detailChooseButton;
    private readonly Label _detailPlaceholder;
    private readonly BindingProgressRing _bindingProgress;

    private readonly RadioButton _radioAlwaysAsk;
    private readonly RadioButton _radioTrustOnFirstUse;
    private readonly RadioButton _radioNeverAsk;
    private readonly ListBox _trustedFoldersList;
    private readonly Label _trustedFilesLabel;
    private SecurityMode _lastGoodSecurityMode;

    private readonly RadioButton _radioKeepAlways;
    private readonly RadioButton _radioKeepOnError;
    private readonly RadioButton _radioKeepNever;
    private readonly TextBox _editorCommandBox;
    private readonly CheckBox _logEnabledCheck;
    private readonly CheckBox _runAsAdminCheck;

    private readonly NeonToolTip _statusTip = new();

    private readonly Button _installButton;
    private readonly Button _uninstallButton;
    private readonly Button _restoreButton;
    private readonly Button _saveButton;
    private readonly Label _progressLabel;
    private readonly string? _selectedExtension;
    private readonly CaptionItem _captionStatus;
    private readonly CaptionItem _captionVersion;
    private readonly CaptionItem _captionUpdate;
    private UpdateController? _updates;
    private UpdatePanel? _updatePanel;
    private readonly CaptionItem _captionLanguage;
    private readonly CaptionItem _captionScale;
    private readonly CaptionItem _captionSponsor;
    private readonly CaptionItem _captionHelp;

    /// <summary>Builds the whole window from code; see SPEC 10 for the layout this follows.</summary>
    public MainForm(
        IConfigStore configStore,
        RunlyConfig config,
        ITrustStore trustStore,
        IShellRegistrar shellRegistrar,
        RegistryBackup registryBackup,
        ContextMenuCleaner menuCleaner,
        ILogger logger,
        string? selectedExtension = null)
    {
        _menuCleaner = menuCleaner;
        _selectedExtension = SettingsCommandLine.NormalizeExtension(selectedExtension);

        // Before any control exists: every size below is derived from this one reading, and re-reading it
        // per control would let two halves of the window disagree.
        Metrics.Initialize(this);

        _configStore = configStore;
        _trustStore = trustStore;
        _shellRegistrar = shellRegistrar;
        _registryBackup = registryBackup;
        _logger = logger;
        _config = config;
        _configStamp = ReadConfigStamp();
        Strings.Language = string.Equals(config.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "tr";
        _lastGoodSecurityMode = config.SecurityMode;
        _installedApplications = new ApplicationFinder().FindAll();

        Text = Strings.Get("app.title");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        var workArea = Screen.PrimaryScreen?.WorkingArea.Size ?? new Size(Metrics.Px(1480), Metrics.Px(1000));
        Size = new Size(
            Math.Min(Metrics.Px(1480), (int)(workArea.Width * 0.9)),
            Math.Min(Metrics.Px(1000), (int)(workArea.Height * 0.9)));
        // The floor is what the layout actually needs, not a round number: the caption band now carries
        // the status, the version, the language switch, the support button and the signature next to
        // three window buttons, and the panels below grew with the 24 design-pixel spacing scale.
        MinimumSize = new Size(
            Math.Min(Metrics.Px(1320), workArea.Width),
            Math.Min(Metrics.Px(860), workArea.Height));
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;
        var root = new NeonLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // A flat 270 could not hold the security panel once the third radio and the second folder button
        // were given real room, which is why this is now the sum of those parts rather than a number.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, PanelsRowHeight));
        // One button row plus the signature line; keeps the two visually joined.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, BottomBarHeight));

        // The status strip was removed: it repeated the footer indicator and its 13.5pt line clipped
        // descenders. These four stay unparented — code paths still set their Text without a UI slot.
        var buttonGap = new Padding(Metrics.Px(TeknesyumTokens.Space3), 0, 0, 0);
        _refreshButton = new NeonButton { Text = "Yenile", Primary = false, AutoSize = true, Margin = buttonGap };
        _refreshButton.Click += (_, _) => RefreshStatusOnly(force: true);
        _statusLabel = new Label { Visible = false };
        _exePathLabel = new Label { Visible = false };
        _configPathLink = new LinkLabel { Visible = false };
        _configPathLink.LinkClicked += (_, _) => OpenContainingFolder(_configStore.ConfigPath);

        // ---- 2. Extension table + detail panel -------------------------------------------
        var gridArea = new NeonLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Padding = new Padding(Metrics.Px(TeknesyumTokens.Space5), Metrics.Px(TeknesyumTokens.Space4), Metrics.Px(TeknesyumTokens.Space5), 0) };
        _categoryRailColumn = new ColumnStyle(SizeType.Absolute, Metrics.Px(210));
        gridArea.ColumnStyles.Add(_categoryRailColumn);
        gridArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gridArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Metrics.Px(300)));
        // The search strip sits above the table, not below it: buried at the bottom of a
        // WrapContents=false button flow it was pushed past the right edge and never found.
        gridArea.RowStyles.Add(new RowStyle(SizeType.Absolute, SearchStripHeight));
        gridArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        gridArea.RowStyles.Add(new RowStyle(SizeType.Absolute, ExtensionButtonsHeight));

        _grid = BuildExtensionGrid();
        _categoryList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.AppBg,
            ForeColor = Palette.TextBody,
            BorderStyle = BorderStyle.None,
            Font = Palette.Body,
            Margin = new Padding(0, 0, Metrics.Px(TeknesyumTokens.Space4), 0),
            DrawMode = DrawMode.OwnerDrawFixed,
            // Owner-drawn item heights are the one thing WinForms is documented never to scale
            // (dotnet/winforms#6382): the row has to hold the icon and one line of the label at whatever
            // size those currently are.
            ItemHeight = Metrics.CategoryRowHeight,
        };
        LoadCategoryIcons();
        foreach (var category in ExtensionCatalog.Entries.Select(entry => entry.Category).Distinct(StringComparer.Ordinal))
        {
            _categoryList.Items.Add(category);
        }
        _categoryList.SelectedIndexChanged += (_, _) => RefreshExtensionGrid();
        _categoryList.DrawItem += DrawCategoryItem;
        gridArea.Controls.Add(_categoryList, 0, 1);
        gridArea.Controls.Add(_grid, 1, 1);

        var detailPanel = new NeonGroupPanel(Strings.Get("details")) { Dock = DockStyle.Fill, Margin = new Padding(Metrics.Px(TeknesyumTokens.Space4), 0, 0, 0) };
        _detailPlaceholder = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Bir satır seçtiğinizde ayrıntılar burada görünür.",
            ForeColor = Palette.TextHint,
            Font = Palette.Body,
        };
        _detailText = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Palette.Surface,
            ForeColor = Palette.TextBody,
            Font = Palette.Body,
            Visible = false,
        };
        _detailAskButton = new NeonButton { Dock = DockStyle.Bottom, AutoSize = true, Visible = false };
        _detailChooseButton = new NeonButton { Dock = DockStyle.Bottom, AutoSize = true, Visible = false, Primary = false, Text = Strings.Get("catalog.chooseApp") };
        _bindingProgress = new BindingProgressRing();
        _detailAskButton.Click += OnDetailAskButtonClicked;
        _detailChooseButton.Click += (_, _) => ChooseApplicationForSelectedRow();
        detailPanel.Controls.Add(_detailText);
        detailPanel.Controls.Add(_detailAskButton);
        detailPanel.Controls.Add(_detailChooseButton);
        detailPanel.Controls.Add(_detailPlaceholder);
        detailPanel.Controls.Add(_bindingProgress);
        gridArea.Controls.Add(detailPanel, 2, 1);

        // One row of single-cell columns instead of a flow: every control is Anchor=Left in its own cell, so
        // the table centres them on one line whatever their heights, and the bulk-assign group on the right
        // end shares that line instead of sitting on its own baseline.
        var extRow = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        extRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var extButtonMargin = new Padding(0, 0, Metrics.Px(TeknesyumTokens.Space3), 0);
        var addExtButton = new NeonButton { Text = "Uzantı ekle", Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        var removeExtButton = new NeonButton { Text = "Seçili uzantıyı sil", Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        var exportButton = new NeonButton { Text = Strings.Get("profile.export"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        var importButton = new NeonButton { Text = Strings.Get("profile.import"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        addExtButton.Click += OnAddExtensionClicked;
        removeExtButton.Click += OnRemoveExtensionClicked;
        exportButton.Click += (_, _) => ExportProfile();
        importButton.Click += (_, _) => ImportProfile();

        // Bulk assignment is an action on the category, not a filter, so it lives with the other actions
        // under the table rather than beside the search box. Choosing one row's application is the detail
        // panel's job; the copy that used to sit here as well is gone.
        var bulkLabel = new Label { Text = Strings.Get("catalog.bulkLabel"), AutoSize = true, Font = Palette.Body, ForeColor = Palette.TextBody, Margin = new Padding(0, 0, Metrics.Px(TeknesyumTokens.Space2), 0) };
        _bulkAppBox = new NeonComboBox { Width = Metrics.Px(220), Margin = new Padding(0, 0, Metrics.Px(TeknesyumTokens.Space2), 0) };
        foreach (var app in _installedApplications) _bulkAppBox.Items.Add(app);
        _bulkAppBox.DisplayMember = nameof(InstalledApplication.DisplayName);
        var bulkButton = new NeonButton { Text = Strings.Get("catalog.bulkOpen"), Primary = true, AutoSize = true, Margin = Padding.Empty };
        bulkButton.Click += (_, _) => AssignCategoryToSelectedApplication();

        Control[] extCells = [addExtButton, removeExtButton, exportButton, importButton, null!, bulkLabel, _bulkAppBox, bulkButton];
        extRow.ColumnCount = extCells.Length;
        for (var column = 0; column < extCells.Length; column++)
        {
            if (extCells[column] is null)
            {
                extRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                continue;
            }

            extRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            extCells[column].Anchor = AnchorStyles.Left;
            extRow.Controls.Add(extCells[column], column, 0);
        }

        // The search row is cut along the grid's own columns: the rail gets its heading, the box starts on
        // the table's left edge, so the eye finds one left edge per column instead of three loose ones. The
        // label, the "Temizle" button and the example line all said the same thing as the placeholder.
        var categoriesLabel = new Label { Text = Strings.Get("categories"), AutoSize = true, Font = Palette.H3, ForeColor = Palette.Renk1, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = _grid.Margin };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var searchBox = new NeonSearchBox { Cue = Strings.Get("catalog.searchLabel"), AccessibleName = Strings.Get("catalog.searchLabel"), AccessibleDescription = Strings.Get("catalog.searchClear"), Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = Padding.Empty };
        searchBox.ClearRequested += (_, _) => ClearSearch();
        _searchBox = searchBox;

        // The catalog carries 400+ rows and every refresh reprojects and refills the whole grid, so
        // rebuilding on each keystroke makes typing stutter. Only typing is delayed: ApplyLanguage and
        // the other call sites keep calling RefreshExtensionGrid directly and stay immediate.
        _searchDebounce = new System.Windows.Forms.Timer { Interval = SearchDebounceMs };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            var scan = new CancellationTokenSource();
            _searchScan = scan;
            _reuseStatusSnapshot = true;
            try
            {
                RefreshExtensionGrid(scan.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _reuseStatusSnapshot = false;
                if (ReferenceEquals(_searchScan, scan)) _searchScan = null;
                scan.Dispose();
            }
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _searchScan?.Cancel();
            _searchDebounce.Stop();
            _searchDebounce.Start();
        };
        _searchBox.KeyDown += OnSearchBoxKeyDown;
        _searchResultLabel = new Label { AutoSize = true, Font = Palette.MonoBody, ForeColor = Palette.Renk2Text, Anchor = AnchorStyles.Left, Margin = new Padding(Metrics.Px(TeknesyumTokens.Space3), 0, 0, 0) };
        searchRow.Controls.Add(_searchBox, 0, 0);
        searchRow.Controls.Add(_searchResultLabel, 1, 0);

        _categoryList.SelectedIndex = 0;
        gridArea.Controls.Add(categoriesLabel, 0, 0);
        gridArea.Controls.Add(searchRow, 1, 0);
        gridArea.Controls.Add(extRow, 0, 2);
        gridArea.SetColumnSpan(extRow, 3);

        Shown += (_, _) => _searchBox.Focus();
        Shown += (_, _) => ApplyRequestedExtension();
        KeyPreview = true;
        KeyDown += OnMainFormKeyDown;

        root.Controls.Add(gridArea, 0, 0);

        // ---- 3 & 4. Security + behavior panels --------------------------------------------
        var panelsRow = new NeonLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(Metrics.Px(TeknesyumTokens.Space5), Metrics.Px(TeknesyumTokens.Space5), Metrics.Px(TeknesyumTokens.Space5), 0) };
        panelsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panelsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        (_radioAlwaysAsk, _radioTrustOnFirstUse, _radioNeverAsk, _trustedFoldersList, _trustedFilesLabel, var securityGroup) = BuildSecurityPanel();
        (_radioKeepAlways, _radioKeepOnError, _radioKeepNever, _editorCommandBox, _logEnabledCheck, _runAsAdminCheck, var behaviorGroup) = BuildBehaviorPanel();

        panelsRow.Controls.Add(securityGroup, 0, 0);
        panelsRow.Controls.Add(behaviorGroup, 1, 0);
        root.Controls.Add(panelsRow, 0, 1);

        // ---- 5. Bottom bar --------------------------------------------------------------------
        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Padding = new Padding(Metrics.Px(TeknesyumTokens.Space5), Metrics.Px(TeknesyumTokens.Space4), Metrics.Px(TeknesyumTokens.Space5), Metrics.Px(TeknesyumTokens.Space4)), Margin = Padding.Empty, BackColor = Palette.Surface };
        bottomBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Three groups instead of one run of seven equal buttons: setup on the left (with the destructive
        // "Kaldır" last and set apart), the progress line and the window tools in the middle, and the form's
        // own Kaydet / Kapat on the right. Each group is AutoSize; only the progress line takes the slack.
        var buttonsRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var groupGap = Metrics.Px(TeknesyumTokens.Space5);
        _progressLabel = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = Palette.MonoBody, ForeColor = Palette.Renk1, Margin = new Padding(groupGap, 0, groupGap, 0) };

        FlowLayoutPanel ButtonGroup(Padding margin) => new() { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Margin = margin };
        var firstInGroup = Padding.Empty;

        var closeButton = new NeonButton { Text = "Kapat", Primary = false, AutoSize = true, Margin = buttonGap };
        _saveButton = new NeonButton { Text = "Kaydet", Primary = true, AutoSize = true, Margin = firstInGroup };
        _restoreButton = new NeonButton { Text = "Yedekten geri yükle", Primary = false, AutoSize = true, Margin = buttonGap };
        var menuButton = new NeonButton { Text = Strings.Get("menu.button"), Primary = false, AutoSize = true, Margin = firstInGroup };
        menuButton.Click += OnContextMenuClicked;
        _uninstallButton = new NeonButton { Text = "Kaldır", Primary = false, Danger = true, AutoSize = true, Margin = new Padding(groupGap, 0, 0, 0) };
        _installButton = new NeonButton { Text = "Kur / Güncelle", Primary = true, AutoSize = true, Margin = firstInGroup };

        closeButton.Click += (_, _) => Close();
        _saveButton.Click += (_, _) => SaveAll();
        _restoreButton.Click += OnRestoreClicked;
        _uninstallButton.Click += OnUninstallClicked;
        _installButton.Click += OnInstallClicked;

        var setupGroup = ButtonGroup(Padding.Empty);
        setupGroup.Controls.Add(_installButton);
        setupGroup.Controls.Add(_restoreButton);
        setupGroup.Controls.Add(_uninstallButton);
        var toolsGroup = ButtonGroup(Padding.Empty);
        toolsGroup.Controls.Add(menuButton);
        toolsGroup.Controls.Add(_refreshButton);
        var formGroup = ButtonGroup(new Padding(groupGap, 0, 0, 0));
        formGroup.Controls.Add(_saveButton);
        formGroup.Controls.Add(closeButton);

        buttonsRow.Controls.Add(setupGroup, 0, 0);
        buttonsRow.Controls.Add(_progressLabel, 1, 0);
        buttonsRow.Controls.Add(toolsGroup, 2, 0);
        buttonsRow.Controls.Add(formGroup, 3, 0);
        bottomBar.Controls.Add(buttonsRow, 0, 0);

        root.Controls.Add(bottomBar, 0, 2);

        Controls.Add(root);

        // ---- 6. Caption band: status, version, language, support link, signature ----------------
        // R5 4 and 5.3: the support link and the signature belong immediately left of the window
        // buttons, and the strip they used to live in is gone. Items are handed over right to left.
        _captionStatus = new CaptionItem { Font = Palette.Body, Color = Palette.TextStrong, Dot = Palette.TextHint };
        // teknesyum-ui 0.34: the version sits left, beside the name, as a quiet grey button that checks
        // for updates; hover turns it renk-1 like every other caption link.
        _captionVersion = new CaptionItem
        {
            Style = CaptionItemStyle.Link,
            Leading = true,
            Font = Palette.Mono,
            Color = Palette.TextMuted,
            Accent = Palette.Renk1,
            Click = CheckForUpdatesNow,
        };
        _captionLanguage = new CaptionItem
        {
            Style = CaptionItemStyle.Link,
            Font = Palette.Mono,
            Color = Palette.Renk1,
            Accent = Palette.Renk2Text,
            Click = () => ChangeLanguage(Strings.Language == "tr" ? "en" : "tr"),
        };

        _captionScale = new CaptionItem
        {
            Style = CaptionItemStyle.Link,
            Font = Palette.Mono,
            Color = Palette.Renk1,
            Accent = Palette.Renk2Text,
            Click = ChangeUiScale,
        };

        _captionSponsor = new CaptionItem
        {
            Text = Strings.Get("sig.support"),
            Style = CaptionItemStyle.Link,
            Icon = CaptionItemIcon.Coffee,
            Font = Palette.Body,
            Color = Palette.Renk3Text,
            Accent = Palette.Renk2Text,
            Click = () => OpenUrl(Palette.SponsorUrl),
        };
        // The repository front page is always English -- GitHub has no language negotiation. The user
        // has already stated a language here, so this link honours it and the question does not have
        // to be asked again on the web page.
        _captionHelp = new CaptionItem
        {
            Text = Strings.Get("caption.help"),
            Style = CaptionItemStyle.Link,
            Font = Palette.Body,
            Color = Palette.Renk1,
            Accent = Palette.Renk2Text,
            Click = () => OpenUrl(Strings.Language == "tr" ? Palette.ReadmeUrlTr : Palette.ReadmeUrlEn),
        };
        var captionSignature = new CaptionItem
        {
            Text = Strings.Get("sig.brand"),
            Style = CaptionItemStyle.Link,
            Font = Palette.Body,
            Color = Palette.Renk1,
            Accent = Palette.Renk1,
            Click = () => OpenUrl(Palette.GitHubUrl),
        };
        _captionUpdate = new CaptionItem
        {
            Style = CaptionItemStyle.Link,
            Font = Palette.Body,
            Color = Palette.TextStrong,
            Accent = Palette.Renk1,
            Visible = false,
            Click = OnUpdateBadgeClick,
        };
        // Right part, right to left: Teknesyum, Destek Ol, TR/EN, update badge, then Runly's own tools.
        SetCaptionItems(captionSignature, _captionSponsor, _captionLanguage, _captionUpdate, _captionStatus, _captionScale, _captionHelp, _captionVersion);

        FormClosing += OnFormClosing;
        Activated += (_, _) => RefreshStatusOnly(force: false);

        // ---- Initial state ------------------------------------------------------------------
        ApplySecurityRadio(_config.SecurityMode);
        ApplyKeepWindowRadio(_config.KeepWindowOpen);
        _editorCommandBox.Text = string.IsNullOrWhiteSpace(_config.EditorCommand) ? DefaultConfig.DefaultEditorCommand : _config.EditorCommand;
        _logEnabledCheck.Checked = _config.LogEnabled;
        _runAsAdminCheck.Checked = _config.RunAsAdmin;
        RefreshTrustedFolders();
        RefreshTrustedFilesLabel();

        _radioAlwaysAsk.CheckedChanged += OnSecurityRadioChanged;
        _radioTrustOnFirstUse.CheckedChanged += OnSecurityRadioChanged;
        _radioNeverAsk.CheckedChanged += OnSecurityRadioChanged;
        _radioKeepAlways.CheckedChanged += (_, _) => MarkDirtyUnlessInitializing();
        _radioKeepOnError.CheckedChanged += (_, _) => MarkDirtyUnlessInitializing();
        _radioKeepNever.CheckedChanged += (_, _) => MarkDirtyUnlessInitializing();
        _editorCommandBox.TextChanged += (_, _) => MarkDirtyUnlessInitializing();
        _logEnabledCheck.CheckedChanged += (_, _) => MarkDirtyUnlessInitializing();
        _runAsAdminCheck.CheckedChanged += (_, _) => MarkDirtyUnlessInitializing();

        _initializing = false;

        RefreshExtensionGrid();
        RefreshStatusStrip();
        ApplyLanguage();
    }

    private DataGridView BuildExtensionGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            // No cell carries a tooltip of its own, so the only thing this produced was a stray
            // "False" bubble over the enabled checkbox.
            ShowCellToolTips = false,
            // Fill, not fixed widths: at 1280 the fixed layout ran ~160px past the viewport and pushed
            // the "Durum" column — the one carrying the "Varsayılan yap" button — off screen behind a
            // horizontal scrollbar. Weights keep every column reachable at MinimumSize too.
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Palette.Surface,
            GridColor = Palette.GridLine, // opaque, dim blue-tinted line (GridColor rejects alpha)
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            EnableHeadersVisualStyles = false,
            Font = Palette.MonoBody,
        };

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Palette.Surface,
            ForeColor = Palette.Renk1,
            Font = Palette.H3,
            SelectionBackColor = Palette.Surface,
            SelectionForeColor = Palette.Renk1,
            Alignment = DataGridViewContentAlignment.MiddleCenter,
        };
        grid.ColumnHeadersHeight = Metrics.GridHeaderHeight;
        // Neither the row template nor the header follows the DPI on its own, and both hold a line of
        // text. The template height goes in after Font: assigning Font resets it to the font's height.
        grid.RowTemplate.Height = Metrics.GridRowHeight;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.RowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Palette.Surface,
            ForeColor = Palette.TextBody,
            SelectionBackColor = Palette.SelectedFill,
            SelectionForeColor = Palette.TextBody,
        };
        // Centring lives on the grid, the weakest style layer: on RowsDefaultCellStyle it outranked every
        // column's own alignment and the free-text columns could not be left-aligned.
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Palette.FieldBg,
            SelectionBackColor = Palette.SelectedFill,
        };

        // These captions are dead defaults — ApplyLanguage overwrites all seven from locale/ before the
        // window is shown. They were still UPPERCASE, which is exactly the thing the standard bans, and a
        // dead string that contradicts the live one is worse than no string.
        grid.Columns.Add(new NeonCheckColumn { Name = "Enabled", HeaderText = "Etkin", FillWeight = 8, MinimumWidth = Metrics.Px(60), Resizable = DataGridViewTriState.False });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Extension", HeaderText = "Uzantı", FillWeight = 10, MinimumWidth = Metrics.Px(72), ReadOnly = true });
        grid.Columns.Add(new NeonChipColumn { Name = "Kind", HeaderText = "Tür", FillWeight = 13, MinimumWidth = Metrics.Px(90), OffTextKey = "kind.run", OnTextKey = "kind.open" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Interpreter", HeaderText = "İşleyici", FillWeight = 20, MinimumWidth = Metrics.Px(130) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Found", HeaderText = "Bulundu", FillWeight = 18, MinimumWidth = Metrics.Px(130), ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Args", HeaderText = "Argümanlar", FillWeight = 12, MinimumWidth = Metrics.Px(90) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Durum", FillWeight = 19, MinimumWidth = Metrics.Px(110), ReadOnly = true });

        // Free text reads from one left edge; only the checkbox, the chip and the status badge stay centred.
        foreach (var name in new[] { "Interpreter", "Found", "Args" })
        {
            grid.Columns[name].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.Columns[name].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }

        WidenHeadersToLongestTranslation(grid);
        grid.Columns[ColEnabled].MinimumWidth += HeaderCheckSide + Metrics.Px(TeknesyumTokens.Space2);

        // The select-all control lives in the column it acts on, as a checkbox beside the caption, rather
        // than as a button under the table that read like one more row action.
        grid.CellPainting += OnGridHeaderPainting;
        grid.ColumnHeaderMouseClick += (_, e) =>
        {
            if (e.ColumnIndex == ColEnabled && e.Button == MouseButtons.Left)
            {
                SetAllExtensionsEnabled(!AllVisibleExtensionsEnabled());
            }
        };
        grid.CellValueChanged += (_, e) => InvalidateEnabledHeader(e.ColumnIndex);
        grid.RowsAdded += (_, _) => InvalidateEnabledHeader(ColEnabled);
        grid.RowsRemoved += (_, _) => InvalidateEnabledHeader(ColEnabled);

        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        grid.CellValueChanged += OnGridCellValueChanged;
        grid.CellContentClick += OnGridCellContentClick;
        grid.CellDoubleClick += OnGridCellDoubleClick;
        grid.KeyDown += OnGridKeyDown;
        grid.SelectionChanged += (_, _) => UpdateDetailPanel();

        return grid;
    }

    private (RadioButton alwaysAsk, RadioButton trustOnFirstUse, RadioButton neverAsk, ListBox folders, Label filesLabel, Panel group) BuildSecurityPanel()
    {
        var group = new NeonGroupPanel(Strings.Get("security")) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, Metrics.Px(TeknesyumTokens.Space3), 0) };
        // Row heights stay Absolute: three stacked NeonRadioButtons inside a nested AutoSize flow is the
        // "AutoSize row + Dock=Fill child" trap R5 already hit (docs/tasks/R5.md); AutoSize mismeasured
        // the stack and painted the next row over it. The slots follow the body font.
        var layout = FieldGrid(RadioStackHeight, FoldersAreaHeight, TrustedFilesRowHeight);

        var radios = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        var alwaysAsk = new NeonRadioButton { Text = "Her seferinde sor", AutoSize = true };
        var trustOnFirstUse = new NeonRadioButton { Text = "İlk seferde sor, sonra güven", AutoSize = true };
        var neverAsk = new NeonRadioButton { Text = "Hiç sorma", AutoSize = true };
        radios.Controls.Add(alwaysAsk);
        radios.Controls.Add(trustOnFirstUse);
        radios.Controls.Add(neverAsk);
        layout.Controls.Add(FieldLabel("security.ask", top: true), 0, 0);
        layout.Controls.Add(radios, 1, 0);
        layout.SetColumnSpan(radios, 2);

        var foldersList = new NeonListBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Metrics.Px(TeknesyumTokens.Space2)) };
        // No margin of its own: the default 3px inset shrinks the cell below the fixed button width and
        // GDI clips the right half of the outline away, which is invisible in a build log.
        var folderButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        var folderButtonSize = new Size(FolderButtonWidth, FolderButtonHeight);
        var folderButtonPadding = new Padding(Metrics.Px(TeknesyumTokens.Space2), Metrics.Px(2), Metrics.Px(TeknesyumTokens.Space2), Metrics.Px(2));
        var addFolderButton = new NeonButton { Text = "Ekle", Primary = false, AutoSize = false, Size = folderButtonSize, Padding = folderButtonPadding, Margin = new Padding(Metrics.Px(TeknesyumTokens.Space2), 0, 0, Metrics.Px(TeknesyumTokens.Space2)) };
        var removeFolderButton = new NeonButton { Text = "Çıkar", Primary = false, AutoSize = false, Size = folderButtonSize, Padding = folderButtonPadding, Margin = new Padding(Metrics.Px(TeknesyumTokens.Space2), 0, 0, 0) };
        addFolderButton.Click += (_, _) => OnAddTrustedFolder(foldersList);
        removeFolderButton.Click += (_, _) => OnRemoveTrustedFolder(foldersList);
        folderButtons.Controls.Add(addFolderButton);
        folderButtons.Controls.Add(removeFolderButton);
        layout.Controls.Add(FieldLabel("trustedFolders", top: true), 0, 1);
        layout.Controls.Add(foldersList, 1, 1);
        layout.Controls.Add(folderButtons, 2, 1);

        var filesLabel = new Label { AutoSize = true, Font = Palette.MonoBody, ForeColor = Palette.TextDim, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        var clearFilesButton = new NeonButton { Text = "Tümünü temizle", Primary = false, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Metrics.Px(TeknesyumTokens.Space2), 0, 0, 0) };
        clearFilesButton.Click += OnClearTrustedFiles;
        layout.Controls.Add(FieldLabel("trustedFiles"), 0, 2);
        layout.Controls.Add(filesLabel, 1, 2);
        layout.Controls.Add(clearFilesButton, 2, 2);

        group.Controls.Add(layout);
        return (alwaysAsk, trustOnFirstUse, neverAsk, foldersList, filesLabel, group);
    }

    private (RadioButton always, RadioButton onError, RadioButton never, TextBox editor, CheckBox logEnabled, CheckBox runAsAdmin, Panel group) BuildBehaviorPanel()
    {
        var group = new NeonGroupPanel(Strings.Get("behavior")) { Dock = DockStyle.Fill, Margin = new Padding(Metrics.Px(TeknesyumTokens.Space3), 0, 0, 0) };
        // Same grid as the security panel (see the comment there for why the rows are Absolute); the label
        // column has the same width in both, so the two panels read as one form.
        var layout = FieldGrid(RadioStackHeight, EditorRowHeight, TrustedFilesRowHeight, TrustedFilesRowHeight);

        var keepRadios = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        var always = new NeonRadioButton { Text = "Her zaman", AutoSize = true };
        var onError = new NeonRadioButton { Text = "Sadece hata olursa", AutoSize = true };
        var never = new NeonRadioButton { Text = "Hiçbir zaman", AutoSize = true };
        keepRadios.Controls.Add(always);
        keepRadios.Controls.Add(onError);
        keepRadios.Controls.Add(never);
        layout.Controls.Add(FieldLabel("windowOpen", top: true), 0, 0);
        layout.Controls.Add(keepRadios, 1, 0);
        layout.SetColumnSpan(keepRadios, 2);

        // Anchored rather than docked, so the row centres box and buttons on one line.
        var editorBox = new NeonTextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = Padding.Empty };
        var editorButtons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        var chooseEditorButton = new NeonButton { Text = Strings.Get("chooseApp.browseEditor"), Primary = false, AutoSize = true, Margin = new Padding(Metrics.Px(TeknesyumTokens.Space2), 0, 0, 0) };
        chooseEditorButton.Click += OnChooseEditorClicked;
        var testButton = new NeonButton { Text = "Test et", Primary = false, AutoSize = true, Margin = new Padding(Metrics.Px(TeknesyumTokens.Space2), 0, 0, 0) };
        testButton.Click += OnTestEditorClicked;
        editorButtons.Controls.Add(chooseEditorButton);
        editorButtons.Controls.Add(testButton);
        layout.Controls.Add(FieldLabel("editorCommand"), 0, 1);
        layout.Controls.Add(editorBox, 1, 1);
        layout.Controls.Add(editorButtons, 2, 1);

        var logCheck = new NeonCheckBox { Text = "Günlük tut", AutoSize = true, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        var openLogButton = new NeonButton { Text = "Günlük klasörünü aç", Primary = false, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Metrics.Px(TeknesyumTokens.Space2), 0, 0, 0) };
        openLogButton.Click += (_, _) => OpenFolder(RunlyPaths.AppDataDir);
        layout.Controls.Add(FieldLabel("logging"), 0, 2);
        layout.Controls.Add(logCheck, 1, 2);
        layout.Controls.Add(openLogButton, 2, 2);

        var adminCheck = new NeonCheckBox { Text = Strings.Get("admin.global"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        layout.Controls.Add(FieldLabel("elevation"), 0, 3);
        layout.Controls.Add(adminCheck, 1, 3);
        layout.SetColumnSpan(adminCheck, 2);

        group.Controls.Add(layout);
        return (always, onError, never, editorBox, logCheck, adminCheck, group);
    }

    // ---- Extension grid -----------------------------------------------------------------

    private static ExtensionMapping CatalogDefault(CatalogEntry entry) => new()
    {
        Kind = entry.DefaultKind,
        Category = entry.Category,
        TypeName = entry.DisplayName.Tr,
        Args = "\"{script}\" {args}",
        Enabled = false,
    };

    private void LoadCategoryIcons()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var category in ExtensionCatalog.Entries.Select(entry => entry.Category).Distinct(StringComparer.Ordinal))
        {
            var fileName = RunlyRegistryLayout.CategoryIconFileName(category);
            using var stream = assembly.GetManifestResourceStream("Runly.Settings.assets." + fileName);
            // Asking the .ico for the scaled size lets it pick a real frame instead of stretching the 20px one.
            var iconSize = Metrics.CategoryIconSize;
            if (stream is not null) _categoryIcons[category] = new Icon(stream, new Size(iconSize, iconSize));
        }
    }

    private void DrawCategoryItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _categoryList.Items.Count) return;
        var category = (string)_categoryList.Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) != 0;
        if (selected)
        {
            using var background = new SolidBrush(Palette.Surface);
            e.Graphics.FillRectangle(background, e.Bounds);
        }
        else
        {
            NeonBackground.Paint(e.Graphics, _categoryList, e.Bounds);
        }

        if (selected)
        {
            using var strip = new SolidBrush(Palette.Renk1);
            e.Graphics.FillRectangle(strip, e.Bounds.Left, e.Bounds.Top, Metrics.Px(3), e.Bounds.Height);
        }

        var iconSize = Metrics.CategoryIconSize;
        var iconLeft = e.Bounds.Left + Metrics.Px(TeknesyumTokens.Space2);
        if (_categoryIcons.TryGetValue(category, out var icon))
            e.Graphics.DrawIcon(icon, new Rectangle(iconLeft, e.Bounds.Top + ((e.Bounds.Height - iconSize) / 2), iconSize, iconSize));

        var entries = ExtensionCatalog.Entries.Where(entry => entry.Category == category).ToArray();
        var catalogExtensions = ExtensionCatalog.Entries.Select(entry => entry.Extension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var custom = _config.Extensions.Where(pair => !catalogExtensions.Contains(pair.Key) && pair.Value.Category == category).ToArray();
        var enabled = entries.Count(entry => EffectiveMapping(entry.Extension).Enabled) + custom.Count(pair => pair.Value.Enabled);
        var total = entries.Length + custom.Length;
        var label = Strings.Get("category." + category);
        var fore = selected ? Palette.Renk1 : Palette.TextBody;
        var countWidth = Metrics.Px(46);
        var labelLeft = iconLeft + iconSize + Metrics.Px(TeknesyumTokens.Space2);
        TextRenderer.DrawText(e.Graphics, label, Font,
            new Rectangle(labelLeft, e.Bounds.Top, Math.Max(0, e.Bounds.Right - labelLeft - countWidth - Metrics.Px(TeknesyumTokens.Space2)), e.Bounds.Height), fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        // A count is a data number, so it is mono; the label beside it is a sentence and stays sans.
        TextRenderer.DrawText(e.Graphics, $"{enabled}/{total}", Palette.MonoBody,
            new Rectangle(e.Bounds.Right - countWidth - Metrics.Px(TeknesyumTokens.Space2), e.Bounds.Top, countWidth, e.Bounds.Height),
            selected ? Palette.Renk1 : Palette.TextBody,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }

    private void RefreshCategoryRail() => _categoryList.Invalidate();

    // The rail is as wide as its longest label in the current language and scale, so no category
    // name is cut to an ellipsis; the fixed 210 stays as the floor.
    private int CategoryRailWidth()
    {
        var widest = _categoryList.Items.Cast<string>()
            .Select(category => TextRenderer.MeasureText(Strings.Get("category." + category), _categoryList.Font).Width)
            .DefaultIfEmpty(0)
            .Max();
        var width = Metrics.Px(TeknesyumTokens.Space2) * 4 + Metrics.CategoryIconSize + widest + Metrics.Px(46)
            + _categoryList.Margin.Horizontal + SystemInformation.VerticalScrollBarWidth;
        return Math.Max(Metrics.Px(210), width);
    }

    private ExtensionMapping EffectiveMapping(string extension)
    {
        if (_config.Extensions.TryGetValue(extension, out var configured)) return configured;
        var entry = CatalogSearchIndex.Find(extension);
        return entry is null ? new ExtensionMapping { Category = "special" } : CatalogDefault(entry);
    }

    private string? SuggestedHandler(string extension, string? currentHandler)
    {
        if (!string.IsNullOrWhiteSpace(currentHandler))
        {
            return null;
        }

        if (!_suggestedHandlers.TryGetValue(extension, out var suggestion))
        {
            suggestion = HandlerSuggestion.Pick(extension, null, UsageHistory.Rank(extension, _config.Extensions));
            _suggestedHandlers[extension] = suggestion;
        }

        return suggestion;
    }

    private IEnumerable<string> VisibleExtensions()
    {
        var category = _categoryList.SelectedItem as string;
        var query = _searchBox.Text.Trim();
        return CatalogGridProjection.GetExtensions(ExtensionCatalog.Entries, _config, category, query);
    }

    private IEnumerable<string> VisibleExtensions(CancellationToken cancellationToken)
    {
        var category = _categoryList.SelectedItem as string;
        var query = _searchBox.Text.Trim();
        return CatalogGridProjection.GetExtensions(ExtensionCatalog.Entries, _config, category, query, cancellationToken);
    }

    private RunlyConfig VisibleStatusConfig() => VisibleStatusConfig(CancellationToken.None);

    private RunlyConfig VisibleStatusConfig(CancellationToken cancellationToken)
    {
        var visible = VisibleExtensions(cancellationToken).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Outside a search every enabled mapping is appended, so an enabled extension can never hide
        // in a category the user is not looking at. During a search that union is wrong: it answered
        // "6 sonuç" to a query that matches nothing, because those six were merely enabled.
        if (_searchBox.Text.Trim().Length == 0)
        {
            foreach (var pair in _config.Extensions.Where(pair => pair.Value.Enabled)) visible.Add(pair.Key);
        }
        return _config with
        {
            Extensions = visible.ToDictionary(extension => extension, EffectiveMapping, StringComparer.OrdinalIgnoreCase),
        };
    }

    private IReadOnlyList<ExtensionStatus> ScanStatuses(RunlyConfig visible)
    {
        if (!_reuseStatusSnapshot)
        {
            var fresh = _shellRegistrar.GetStatus(visible);
            _statusSnapshot.Clear();
            foreach (var status in fresh) _statusSnapshot[status.Extension] = status;
            return fresh;
        }

        var missing = visible.Extensions
            .Where(pair => !_statusSnapshot.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (missing.Count > 0)
        {
            foreach (var status in _shellRegistrar.GetStatus(visible with { Extensions = missing }))
            {
                _statusSnapshot[status.Extension] = status;
            }
        }

        return [.. visible.Extensions.Keys
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .Select(extension => _statusSnapshot.GetValueOrDefault(extension))
            .OfType<ExtensionStatus>()];
    }

    private void RefreshExtensionGrid() => RefreshExtensionGrid(CancellationToken.None);

    private void RefreshExtensionGrid(CancellationToken cancellationToken)
    {
        var refreshTimer = Stopwatch.StartNew();
        _suppressGridEvents = true;
        string? selectedExtension = null;
        var selectedIndex = -1;
        try
        {
            if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ExtensionStatus selected)
            {
                selectedExtension = selected.Extension;
            }

            _grid.Rows.Clear();

            var statuses = ScanStatuses(VisibleStatusConfig(cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            _bindingProgress.SetProgress(statuses.Count(status => status.Bound == BindingState.Bound), statuses.Count);
            var rows = new List<DataGridViewRow>(statuses.Count);
            foreach (var status in statuses)
            {
                var mapping = EffectiveMapping(status.Extension);

                var row = new DataGridViewRow { Height = Metrics.GridRowHeight };
                row.CreateCells(_grid);
                row.Cells[ColEnabled].Value = mapping.Enabled;
                row.Cells[ColExtension].Value = status.Extension;
                // The chip stores the state, not the caption it happens to be showing: a language switch
                // used to have to rewrite every cell, and comparing localised text back to a kind was one
                // renamed string away from silently saving the wrong handler.
                row.Cells[ColKind].Value = mapping.Kind == HandlerKind.Open;
                // NullValue rather than a placeholder string: the hint has to be visible in the cell
                // without ever becoming the cell's value, which OnGridCellValueChanged would save.
                var handler = mapping.Kind == HandlerKind.Run ? mapping.Interpreter : mapping.OpenWith;
                row.Cells[ColInterpreter].Value = string.IsNullOrWhiteSpace(handler) ? null : handler;
                if (string.IsNullOrWhiteSpace(handler))
                {
                    var suggested = SuggestedHandler(status.Extension, handler);
                    row.Cells[ColInterpreter].Style.NullValue = suggested is null
                        ? Strings.Get("handler.choosePrompt")
                        : Strings.Get("handler.suggested").Replace("{app}", Path.GetFileName(suggested), StringComparison.Ordinal);
                    row.Cells[ColInterpreter].Style.ForeColor = Palette.TextDim;
                }
                row.Cells[ColArgs].Value = mapping.Args;
                ApplyStatusToRow(row, status);
                var catalogEntry = CatalogEntryFor(status.Extension);

                // A blocked row already says why in its status cell. An unblocked one with a note looks
                // like every other row, so the extension itself carries the mark — the note is in the
                // details panel and nothing else on the row hints that it is worth reading.
                if (catalogEntry?.RiskNote is not null && catalogEntry.Blocked != true)
                {
                    row.Cells[ColExtension].Style.Font = Palette.Mono;
                    row.Cells[ColExtension].Style.ForeColor = Palette.Renk2Text;
                    row.Cells[ColExtension].Style.SelectionForeColor = Palette.Renk2Text;
                }

                if (catalogEntry?.Blocked == true)
                {
                    row.Cells[ColEnabled].ReadOnly = true;
                    row.Cells[ColKind].ReadOnly = true;
                    row.Cells[ColInterpreter].ReadOnly = true;
                    row.Cells[ColArgs].ReadOnly = true;
                    row.Cells[ColStatus].Value = catalogEntry.RiskNote is null ? Strings.Get("catalog.blocked") : (Strings.Language == "en" ? catalogEntry.RiskNote.En : catalogEntry.RiskNote.Tr);
                }

                if (selectedExtension is not null && string.Equals(selectedExtension, status.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = rows.Count;
                }

                rows.Add(row);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (rows.Count > 0)
            {
                _grid.SuspendLayout();
                try
                {
                    _grid.Rows.AddRange([.. rows]);
                }
                finally
                {
                    _grid.ResumeLayout();
                }
            }

            if (selectedIndex >= 0) _grid.Rows[selectedIndex].Selected = true;
        }
        finally
        {
            _suppressGridEvents = false;
        }

        UpdateSearchResultLabel();
        UpdateDetailPanel();
        refreshTimer.Stop();
        _logger.Info($"Kategori ızgarası yenilendi: {_grid.Rows.Count} satır, {refreshTimer.Elapsed.TotalMilliseconds:F1} ms.");
    }

    /// <summary>
    /// B7 fix: refreshes only the "Bulundu"/"Durum" columns and the status strip from the registry,
    /// leaving "Yorumlayıcı"/"Argümanlar" untouched so an unsaved edit in progress is never overwritten.
    /// Triggered on window <c>Activated</c> (throttled to once/second) and by the manual "Yenile" button.
    /// </summary>
    private void RefreshStatusOnly(bool force)
    {
        if (_autoRefreshInFlight)
        {
            return;
        }

        if (!force && DateTime.UtcNow - _lastAutoRefresh < TimeSpan.FromSeconds(1))
        {
            return;
        }

        if (_grid.IsCurrentCellInEditMode)
        {
            return;
        }

        _autoRefreshInFlight = true;
        _lastAutoRefresh = DateTime.UtcNow;

        var statusConfig = VisibleStatusConfig();
        var refreshTimer = Stopwatch.StartNew();
        Task.Run(() => _shellRegistrar.GetStatus(statusConfig)).ContinueWith(t =>
        {
            _autoRefreshInFlight = false;

            if (t.IsFaulted || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            var statuses = t.Result;

            void Apply()
            {
                if (IsDisposed)
                {
                    return;
                }

                ApplyStatusesToGrid(statuses);
                RefreshStatusStrip();
                refreshTimer.Stop();
                _logger.Info($"Etkin pencere durumu yenilendi: {statuses.Count} uzantı, {refreshTimer.Elapsed.TotalMilliseconds:F1} ms.");
            }

            if (InvokeRequired)
            {
                try
                {
                    Invoke(Apply);
                }
                catch (ObjectDisposedException)
                {
                    // Form closed between the check above and the marshal call.
                }
            }
            else
            {
                Apply();
            }
        }, TaskScheduler.Default);
    }

    private void ApplyStatusesToGrid(IReadOnlyList<ExtensionStatus> statuses)
    {
        if (_grid.IsCurrentCellInEditMode)
        {
            return;
        }

        _suppressGridEvents = true;
        try
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var extension = row.Cells[ColExtension].Value as string;
                var match = statuses.FirstOrDefault(s => string.Equals(s.Extension, extension, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    continue;
                }

                ApplyStatusToRow(row, match);
            }
        }
        finally
        {
            _suppressGridEvents = false;
        }

        UpdateDetailPanel();
        RefreshCategoryRail();
        _bindingProgress.SetProgress(statuses.Count(status => status.Bound == BindingState.Bound), statuses.Count);
    }

    private void UpdateSingleRowStatus(int rowIndex, string extension)
    {
        var match = _shellRegistrar.GetStatus(_config)
            .FirstOrDefault(s => string.Equals(s.Extension, extension, StringComparison.OrdinalIgnoreCase));

        if (match is null || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
        {
            return;
        }

        _suppressGridEvents = true;
        try
        {
            ApplyStatusToRow(_grid.Rows[rowIndex], match);
        }
        finally
        {
            _suppressGridEvents = false;
        }

        UpdateDetailPanel();
    }

    private void ApplyStatusToRow(DataGridViewRow row, ExtensionStatus status)
    {
        var mapping = EffectiveMapping(status.Extension);
        row.Cells[ColFound].Value = mapping.Kind == HandlerKind.Open && string.IsNullOrWhiteSpace(mapping.OpenWith)
            ? Strings.Get("handler.notSelected")
            : status.InterpreterFound ? $"✓ {Path.GetFileName(status.InterpreterPath)}" : Strings.Get("found.missing");

        var (text, back, fore) = DescribeStatus(status.Bound);

        if (status.Bound == BindingState.NeedsUserChoice)
        {
            if (row.Cells[ColStatus] is not NeonActionCell)
            {
                row.Cells[ColStatus] = new NeonActionCell();
            }

            row.Cells[ColStatus].Value = Strings.Get("askWindows");
        }
        else
        {
            if (row.Cells[ColStatus] is not DataGridViewTextBoxCell)
            {
                row.Cells[ColStatus] = new DataGridViewTextBoxCell();
            }

            row.Cells[ColStatus].Value = text;
        }

        row.Cells[ColStatus].Style.BackColor = back;
        row.Cells[ColStatus].Style.ForeColor = fore;
        row.Cells[ColStatus].Style.SelectionBackColor = back;
        row.Cells[ColStatus].Style.SelectionForeColor = fore;
        row.Tag = status;
    }

    private static (string Text, Color Back, Color Fore) DescribeStatus(BindingState state) => state switch
    {
        BindingState.Bound => (Strings.Get("bound"), BoundBack, BoundFore),
        BindingState.NeedsUserChoice => (Strings.Get("needsApproval"), NeedsChoiceBack, NeedsChoiceFore),
        _ => (Strings.Get("notBound"), NotBoundBack, NotBoundFore),
    };

    private void OnGridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressGridEvents || e.RowIndex < 0)
        {
            return;
        }

        if (e.ColumnIndex is not (ColEnabled or ColKind or ColInterpreter or ColArgs))
        {
            return;
        }

        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not ExtensionStatus status)
        {
            return;
        }
        var mapping = EffectiveMapping(status.Extension);

        var enabled = row.Cells[ColEnabled].Value is bool b ? b : mapping.Enabled;
        var kind = row.Cells[ColKind].Value is true ? HandlerKind.Open : HandlerKind.Run;
        var handler = row.Cells[ColInterpreter].Value as string ?? (kind == HandlerKind.Run ? mapping.Interpreter : mapping.OpenWith ?? string.Empty);
        var args = row.Cells[ColArgs].Value as string ?? mapping.Args;

        // Ticking Etkin is the approval: the suggestion the cell was only hinting at becomes the value
        // here and nowhere else. Unticking does not take it back — the handler is visible now, and a
        // silent revert would be the surprising half of the pair.
        if (e.ColumnIndex == ColEnabled && enabled && string.IsNullOrWhiteSpace(handler) &&
            SuggestedHandler(status.Extension, handler) is { } suggested)
        {
            handler = suggested;
            AdoptSuggestedHandler(row, suggested);
        }

        var catalogTypeName = ExtensionCatalog.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Extension, status.Extension, StringComparison.OrdinalIgnoreCase))?.DisplayName.Tr;

        _config.Extensions[status.Extension] = mapping with
        {
            Enabled = enabled,
            Kind = kind,
            Interpreter = kind == HandlerKind.Run ? handler : mapping.Interpreter,
            OpenWith = kind == HandlerKind.Open ? handler : null,
            TypeName = catalogTypeName ?? mapping.TypeName,
            Args = args,
        };
        MarkDirty();
        UpdateSingleRowStatus(e.RowIndex, status.Extension);
    }

    private void AdoptSuggestedHandler(DataGridViewRow row, string handler)
    {
        var suppressed = _suppressGridEvents;
        _suppressGridEvents = true;
        try
        {
            row.Cells[ColInterpreter].Style.NullValue = null;
            row.Cells[ColInterpreter].Style.ForeColor = Color.Empty;
            row.Cells[ColInterpreter].Value = handler;
        }
        finally
        {
            _suppressGridEvents = suppressed;
        }
    }

    private static int HeaderCheckSide => Metrics.Px(TeknesyumTokens.Space4);

    private void InvalidateEnabledHeader(int columnIndex)
    {
        if (columnIndex == ColEnabled && _grid is not null)
        {
            _grid.InvalidateCell(ColEnabled, -1);
        }
    }

    private bool AllVisibleExtensionsEnabled()
    {
        var any = false;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var cell = row.Cells[ColEnabled];
            if (cell.ReadOnly) continue;
            if (cell.Value is not true) return false;
            any = true;
        }

        return any;
    }

    private void OnGridHeaderPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 || e.ColumnIndex != ColEnabled || e.Graphics is null || e.CellStyle is null)
        {
            return;
        }

        e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
        var text = _grid.Columns[ColEnabled].HeaderText;
        var font = e.CellStyle.Font ?? _grid.Font;
        var textSize = TextRenderer.MeasureText(e.Graphics, text, font, Size.Empty, TextFormatFlags.NoPadding);
        var gap = Metrics.Px(TeknesyumTokens.Space2);
        var side = HeaderCheckSide;
        var left = e.CellBounds.X + ((e.CellBounds.Width - (side + gap + textSize.Width)) / 2);
        var box = new Rectangle(left, e.CellBounds.Y + ((e.CellBounds.Height - side) / 2), side, side);
        var on = AllVisibleExtensionsEnabled();
        NeonTheme.DrawCheckGlyph(e.Graphics, box, on, Palette.Renk1, on ? Palette.Renk1 : NeonTheme.IdleOutline);
        var textBounds = new Rectangle(box.Right + gap, e.CellBounds.Y, textSize.Width + gap, e.CellBounds.Height);
        TextRenderer.DrawText(e.Graphics, text, font, textBounds, e.CellStyle.ForeColor, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.Handled = true;
    }

    private void SetAllExtensionsEnabled(bool enabled)
    {
        foreach (var extension in VisibleExtensions())
        {
            if (RunlyRegistryLayout.IsBlockedExtension(extension)) continue;
            _config.Extensions[extension] = EffectiveMapping(extension) with { Enabled = enabled };
        }

        RefreshExtensionGrid();
        MarkDirty();
    }

    private void AssignCategoryToSelectedApplication()
    {
        if (_bulkAppBox.SelectedItem is not InstalledApplication app) return;
        foreach (var extension in VisibleExtensions())
        {
            if (RunlyRegistryLayout.IsBlockedExtension(extension)) continue;
            var mapping = EffectiveMapping(extension);
            _config.Extensions[extension] = mapping with
            {
                Kind = HandlerKind.Open,
                OpenWith = app.Path,
                TypeName = ExtensionCatalog.Entries.FirstOrDefault(entry =>
                    string.Equals(entry.Extension, extension, StringComparison.OrdinalIgnoreCase))?.DisplayName.Tr,
                Args = "\"{script}\" {args}",
                Enabled = true,
            };
        }
        MarkDirty();
        RefreshCategoryRail();
        RefreshExtensionGrid();
    }

    private void ExportProfile()
    {
        using var picker = new SaveFileDialog { Filter = Strings.Get("profile.filter"), FileName = "runly-config.json", AddExtension = true, DefaultExt = "json" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        var snapshot = _config with
        {
            SecurityMode = GetSelectedSecurityMode(),
            KeepWindowOpen = GetSelectedKeepWindowMode(),
            EditorCommand = _editorCommandBox.Text.Trim(),
            LogEnabled = _logEnabledCheck.Checked,
            RunAsAdmin = _runAsAdminCheck.Checked,
            Language = Strings.Language,
            Extensions = CreateSparseExtensions(),
        };
        new ConfigStore(picker.FileName).Save(snapshot);
        _progressLabel.Text = Strings.Get("profile.exported");
    }

    private void ImportProfile()
    {
        using var picker = new OpenFileDialog { Filter = Strings.Get("profile.filter"), CheckFileExists = true };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        var imported = new ConfigStore(picker.FileName, _logger).Load();
        _config = imported;
        ApplySecurityRadio(imported.SecurityMode);
        ApplyKeepWindowRadio(imported.KeepWindowOpen);
        _editorCommandBox.Text = imported.EditorCommand;
        _logEnabledCheck.Checked = imported.LogEnabled;
        _runAsAdminCheck.Checked = imported.RunAsAdmin;
        MarkDirty();
        RefreshExtensionGrid();
        RefreshCategoryRail();
        _progressLabel.Text = Strings.Get("profile.imported");
    }

    /// <summary>Space and Enter flip the focused toggle cell. The columns they act on used to be a system
    /// check box and a combo box, which handled this themselves; owner-drawing them means owning it too.</summary>
    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is not (Keys.Space or Keys.Enter))
        {
            return;
        }

        if (_grid.CurrentCell is not INeonToggleCell toggle || _grid.CurrentCell.ReadOnly)
        {
            return;
        }

        toggle.Toggle();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void OnGridCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != ColStatus)
        {
            return;
        }

        if (_grid.Rows[e.RowIndex].Cells[ColStatus] is not NeonActionCell)
        {
            return;
        }

        if (_grid.Rows[e.RowIndex].Tag is not ExtensionStatus status)
        {
            return;
        }

        AskWindows(status.Extension);
    }

    private void OnGridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].Tag is not ExtensionStatus status)
        {
            return;
        }

        if (e.ColumnIndex is ColEnabled or ColKind or ColArgs)
        {
            return;
        }

        ChooseApplicationFor(status.Extension);
    }

    private void ChooseApplicationForSelectedRow()
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Tag is not ExtensionStatus status)
        {
            NeonMessageBox.Show(this, Strings.Get("chooseApp.noRow"), Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ChooseApplicationFor(status.Extension);
    }

    /// <summary>Opens the picker for one extension and writes the choice back. A "Run" mapping keeps its
    /// kind and receives the executable as its interpreter; an "Open" mapping receives it as its handler.</summary>
    private void ChooseApplicationFor(string extension)
    {
        if (IsBlocked(extension))
        {
            NeonMessageBox.Show(this, Strings.Get("extension.blockedAdd"), Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _grid.EndEdit();
        var mapping = EffectiveMapping(extension);
        var catalogEntry = ExtensionCatalog.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Extension, extension, StringComparison.OrdinalIgnoreCase));
        var runMode = mapping.Kind == HandlerKind.Run;

        using var dialog = new ChooseApplicationDialog(
            extension,
            mapping.Kind,
            _installedApplications,
            catalogEntry?.SuggestedApps ?? [],
            runMode ? mapping.Interpreter : mapping.OpenWith,
            UsageHistory.Rank(extension, _config.Extensions));

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (runMode && LooksLikeEditor(dialog.SelectedPath) &&
            NeonMessageBox.Show(this,
                Strings.Get("chooseApp.looksLikeEditor")
                    .Replace("{app}", dialog.SelectedDisplayName, StringComparison.Ordinal)
                    .Replace("{extension}", extension, StringComparison.Ordinal),
                Strings.Get("app.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _editorCommandBox.Text = dialog.SelectedPath;
            _logger.Info($"Yorumlayıcı yerine düzenleyici olarak atandı: {dialog.SelectedPath}");
            return;
        }

        _config.Extensions[extension] = mapping with
        {
            Enabled = true,
            Interpreter = runMode ? dialog.SelectedPath : mapping.Interpreter,
            OpenWith = runMode ? mapping.OpenWith : dialog.SelectedPath,
            TypeName = catalogEntry?.DisplayName.Tr ?? mapping.TypeName,
            Args = string.IsNullOrWhiteSpace(mapping.Args) ? DefaultConfig.ScriptThenArgs : mapping.Args,
        };

        MarkDirty();
        RefreshCategoryRail();
        RefreshExtensionGrid();
        SelectExtensionRow(extension);
        _progressLabel.Text = Strings.Get("chooseApp.assigned")
            .Replace("{extension}", extension, StringComparison.Ordinal)
            .Replace("{app}", dialog.SelectedDisplayName, StringComparison.Ordinal);
        _logger.Info($"Uzantı eşlemesi seçildi: {extension} -> {dialog.SelectedPath}");
    }

    private static bool IsBlocked(string extension) =>
        RunlyRegistryLayout.IsBlockedExtension(extension) ||
        ExtensionCatalog.Entries.Any(entry =>
            string.Equals(entry.Extension, extension, StringComparison.OrdinalIgnoreCase) && entry.Blocked);

    /// <summary>
    /// Applies the <c>--select</c> extension the launcher passed in. It runs from <see cref="Form.Shown"/>
    /// rather than the constructor because <see cref="SelectExtensionRow"/> can only see rows the grid has
    /// already materialised, and the rail has to be moved to the extension's own category first.
    /// </summary>
    private void ApplyRequestedExtension()
    {
        if (_selectedExtension is not { Length: > 1 } extension)
        {
            return;
        }

        var blocked = IsBlocked(extension);
        var added = false;

        if (!blocked && !_config.Extensions.ContainsKey(extension))
        {
            var known = CatalogEntryFor(extension);
            _config.Extensions[extension] = new ExtensionMapping
            {
                Category = string.IsNullOrEmpty(known?.Category) ? "special" : known.Category,
                Kind = known?.DefaultKind ?? HandlerKind.Run,
                TypeName = known?.DisplayName.Tr ?? string.Empty,
                Enabled = false,
            };
            added = true;
            MarkDirty();
            RefreshCategoryRail();
        }

        var category = EffectiveMapping(extension).Category;
        var categoryIndex = _categoryList.Items.IndexOf(category);
        if (categoryIndex >= 0 && _categoryList.SelectedIndex != categoryIndex)
        {
            _categoryList.SelectedIndex = categoryIndex;
        }

        ClearSearch();
        RefreshExtensionGrid();
        SelectExtensionRow(extension);

        if (!IsRowSelected(extension))
        {
            _logger.Warn($"--select ile istenen uzantı ızgarada bulunamadı: {extension}");
            _progressLabel.Text = blocked
                ? Strings.Get("extension.blockedAdd")
                : Strings.Get("select.notFound").Replace("{extension}", extension, StringComparison.Ordinal);
            return;
        }

        _grid.Focus();
        _progressLabel.Text = Strings.Get(added ? "select.added" : "select.selected")
            .Replace("{extension}", extension, StringComparison.Ordinal);
        _logger.Info($"--select uygulandı: {extension} (listeye eklendi: {added})");
    }

    private bool IsRowSelected(string extension) =>
        _grid.SelectedRows.Count > 0 &&
        _grid.SelectedRows[0].Tag is ExtensionStatus status &&
        string.Equals(status.Extension, extension, StringComparison.OrdinalIgnoreCase);

    private void SelectExtensionRow(string extension)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is ExtensionStatus status &&
                string.Equals(status.Extension, extension, StringComparison.OrdinalIgnoreCase))
            {
                row.Selected = true;
                _grid.FirstDisplayedScrollingRowIndex = row.Index;
                return;
            }
        }
    }

    private void ClearSearch()
    {
        if (_searchBox.Text.Length > 0)
        {
            _searchBox.Text = string.Empty;
        }

        _searchBox.Focus();
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            ClearSearch();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode is Keys.Enter or Keys.Down && _grid.Rows.Count > 0)
        {
            _grid.Focus();
            _grid.Rows[0].Selected = true;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void OnMainFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Control || e.KeyCode != Keys.F)
        {
            return;
        }

        _searchBox.Focus();
        _searchBox.SelectAll();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void UpdateSearchResultLabel()
    {
        if (_searchBox.Text.Trim().Length == 0)
        {
            _searchResultLabel.Text = string.Empty;
            return;
        }

        _searchResultLabel.Font = Palette.MonoBody;
        _searchResultLabel.ForeColor = Palette.Renk2Text;
        _searchResultLabel.Text = _grid.Rows.Count == 0
            ? Strings.Get("catalog.searchNoResults")
            : Strings.Get("catalog.searchResults")
                .Replace("{count}", _grid.Rows.Count.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);
    }

    private void OnDetailAskButtonClicked(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Tag is not ExtensionStatus status)
        {
            return;
        }

        AskWindows(status.Extension);
    }

    /// <summary>
    /// Opens Runly's Default apps page for one extension. Windows only lists extensions that are
    /// present in <c>Capabilities\FileAssociations</c>, and only "Install / Update" writes that key —
    /// "Save" writes the config file and nothing else. Sending the user to a page that cannot contain
    /// their extension is what made the button look broken, so an unregistered extension is offered
    /// registration first.
    /// </summary>
    private async void AskWindows(string? extension)
    {
        if (extension is not null && !IsRegisteredWithWindows(extension))
        {
            var answer = NeonMessageBox.Show(this,
                Strings.Get("bind.needsInstall").Replace("{extension}", extension, StringComparison.Ordinal),
                Strings.Get("app.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                return;
            }

            if (_dirty)
            {
                SaveAll();
            }

            var (success, _) = await RunInstallAsync();
            if (!success)
            {
                return;
            }

            if (!IsRegisteredWithWindows(extension))
            {
                NeonMessageBox.Show(this,
                    Strings.Get("bind.notRegistered").Replace("{extension}", extension, StringComparison.Ordinal),
                    Strings.Get("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        if (OfferBulkAssignment(extension is null ? PendingExtensions() : [extension, .. PendingExtensions()]))
        {
            return;
        }

        // SHOpenWithDialog is intentionally not used here: Windows 11 exposes only "Just once"
        // through that API. The file-type deep link opens the list where a persistent choice can be
        // made; see OpenDefaultAppsForExtension for why the per-app page cannot serve this.
        if (extension is null)
        {
            OpenDefaultAppsSettings(forRunly: true);
            return;
        }

        OpenDefaultAppsForExtension(extension);
    }

    private IReadOnlyList<string> PendingExtensions() =>
        _shellRegistrar.GetStatus(_config)
            .Where(status => status.Bound == BindingState.NeedsUserChoice)
            .Select(status => status.Extension)
            .ToArray();

    private bool OfferBulkAssignment(IReadOnlyList<string> pending)
    {
        var assignable = BulkAssociationCommand.Assignable(pending);
        if (assignable.Count == 0)
        {
            return false;
        }

        var answer = NeonMessageBox.Show(this,
            Strings.Get("bulk.offer").Replace("{count}", assignable.Count.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal),
            Strings.Get("app.title"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (answer == DialogResult.Cancel)
        {
            return true;
        }

        if (answer != DialogResult.Yes)
        {
            return false;
        }

        try
        {
            Clipboard.SetText(BulkAssociationCommand.Build(assignable));
        }
        catch (Exception ex) when (ex is ExternalException or ThreadStateException)
        {
            _logger.Error("Toplu atama komutu panoya kopyalanamadı", ex);
            NeonMessageBox.Show(this,
                Strings.Get("bulk.clipboardFailed").Replace("{error}", ex.Message, StringComparison.Ordinal),
                Strings.Get("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return true;
        }

        _logger.Info($"Toplu atama komutu panoya kopyalandı: {string.Join(", ", assignable)}");
        NeonMessageBox.Show(this, Strings.Get("bulk.copied"), Strings.Get("app.title"),
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        return true;
    }

    private bool IsRegisteredWithWindows(string extension)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunlyRegistryLayout.FileAssociationsKey, writable: false);
            return key?.GetValue(RunlyConfig.NormalizeExtension(extension)) is not null;
        }
        catch (System.Security.SecurityException ex)
        {
            _logger.Error("Capabilities anahtarı okunamadı", ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error("Capabilities anahtarı okunamadı", ex);
            return false;
        }
    }

    private void UpdateDetailPanel()
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Tag is not ExtensionStatus status)
        {
            _detailPlaceholder.Visible = true;
            _detailText.Visible = false;
            _detailAskButton.Visible = false;
            _detailChooseButton.Visible = false;
            return;
        }

        _detailPlaceholder.Visible = false;
        _detailText.Visible = true;
        _detailChooseButton.Text = Strings.Get("catalog.chooseApp");
        _detailChooseButton.Visible = !IsBlocked(status.Extension);

        string body;
        var selectedMapping = EffectiveMapping(status.Extension);
        if (selectedMapping.Kind == HandlerKind.Open && string.IsNullOrWhiteSpace(selectedMapping.OpenWith))
        {
            // The cell is thirteen characters wide, so the suggestion can only fit the application name
            // there. The sentence that says where it came from and how to accept it belongs here, where
            // there is room for it.
            var suggestion = SuggestedHandler(status.Extension, selectedMapping.OpenWith);
            body = (suggestion is null
                    ? Strings.Get("handler.notSelectedDetail")
                    : Strings.Get("handler.suggestedDetail").Replace("{app}", Path.GetFileName(suggestion), StringComparison.Ordinal))
                .Replace("{extension}", status.Extension, StringComparison.Ordinal)
                .Replace("{button}", Strings.Get("catalog.chooseApp"), StringComparison.Ordinal);
            _detailAskButton.Visible = false;
        }
        else if (status.Bound == BindingState.NeedsUserChoice)
        {
            body = BuildNeedsChoiceExplanation(status.Extension, status.UserChoiceOwnerName);
            _detailAskButton.Text = Strings.Get("bind.openFileTypePage")
                .Replace("{extension}", status.Extension, StringComparison.Ordinal);
            _detailAskButton.Visible = true;
        }
        else if (status.Bound == BindingState.Bound)
        {
            body = Strings.Language == "en"
                ? $"✅ `{status.Extension}` is now bound to Runly."
                : $"✅ `{status.Extension}` artık Runly'ye bağlı.";
            _detailAskButton.Visible = false;
        }
        else
        {
            body = Strings.Language == "en"
                ? $"`{status.Extension}` is not bound to Runly yet. Use the ‘Install / Update’ button to bind it."
                : $"`{status.Extension}` henüz Runly'ye bağlı değil. Bağlamak için \"Kur / Güncelle\" düğmesini kullanın.";
            _detailAskButton.Visible = false;
        }

        if (status.InterpreterFound && !string.IsNullOrWhiteSpace(status.InterpreterPath))
        {
            body += "\n\n" + Strings.Get("found.path").Replace("{path}", status.InterpreterPath, StringComparison.Ordinal);
        }

        var risk = RiskNoteFor(status.Extension);
        if (risk is not null)
        {
            body += $"\n\n⚠ **{Strings.Get("catalog.riskNote")}** {MarkHostNames(risk)}";
        }

        RenderMarkdownLite(_detailText, body);
    }

    /// <summary>FillWeight decides how the spare width is shared, but a column never shrinks below its
    /// MinimumWidth — and those were sized against the Turkish headers, so ENABLED and EXTENSION arrived
    /// in English already ellipsised. The layout is sized against the longest translation instead.</summary>
    private static void WidenHeadersToLongestTranslation(DataGridView grid)
    {
        var header = grid.ColumnHeadersDefaultCellStyle.Font;
        var padding = Metrics.Px(TeknesyumTokens.Space2);
        foreach (var (column, keys, font) in new[]
                 {
                     (grid.Columns[ColEnabled], new[] { "enabled" }, header),
                     (grid.Columns[ColExtension], ["extension"], header),
                     (grid.Columns[ColKind], ["kind.column", "kind.run", "kind.open"], header),
                     (grid.Columns[ColInterpreter], ["interpreter"], header),
                     (grid.Columns[ColFound], ["found"], header),
                     (grid.Columns[ColArgs], ["arguments"], header),

                     // The status cell is the one column whose content is longer than its header, and it
                     // carries the only call to action in the table — an ellipsised "Set default" is the
                     // row telling the user to do something and hiding what.
                     (grid.Columns[ColStatus], ["status", "askWindows", "bound", "notBound", "needsApproval"], grid.Font),
                 })
        {
            var widest = keys
                .SelectMany(key => Strings.Languages.Select(language => Strings.GetIn(language, key)))
                .Max(text => TextRenderer.MeasureText(text, font).Width);
            column.MinimumWidth = Math.Max(column.MinimumWidth, widest + padding);
        }
    }

    internal static CatalogEntry? CatalogEntryFor(string extension) => CatalogSearchIndex.Find(extension);

    private static string? RiskNoteFor(string extension)
    {
        var note = CatalogEntryFor(extension)?.RiskNote;
        return note is null ? null : Strings.Language == "en" ? note.En : note.Tr;
    }

    /// <summary>Wraps every <c>name.exe</c> in the note as a code span so the host — the one claim the
    /// reader can check for themselves with <c>assoc</c> and <c>ftype</c> — is set apart from the prose
    /// instead of being spelled the same as it. The catalog keeps the notes as plain sentences so a
    /// translator never has to type markup.</summary>
    private static string MarkHostNames(string note) => HostExecutable().Replace(note, "`$0`");

    // Text taken from docs/reports/T4-COMPLETE.md ("T5 için GUI metni önerisi"), parameterised on the extension
    // so the same wording works for every extension that needs approval — which, after decision K19, is nearly
    // all of them and not just ".ps1" on this particular machine.
    private static string BuildNeedsChoiceExplanation(string extension, string? ownerName)
    {
        if (Strings.Language == "en")
        {
            var englishSituation = string.IsNullOrWhiteSpace(ownerName)
                ? "The registrations were written, but Windows has not yet decided which application opens this extension: " +
                  "other candidate applications exist for the same extension, so double-clicking shows " +
                  "**‘How do you want to open this file?’**."
                : $"Windows currently opens this extension with **{ownerName}**.";
            return englishSituation + $"\n\nTo bind **{extension}** permanently, right-click a `{extension}` file in Explorer → " +
                   "**Open with** → **Choose another app** → **Runly** → **Always**. " +
                   "The button below opens the Windows file-type page. Type " +
                   $"**{extension}** into the box at the top of that page — Windows only fills it in on a " +
                   "cold start of Settings, so type it yourself — then pick **Runly** in the row that appears.";
        }

        var situation = string.IsNullOrWhiteSpace(ownerName)
            ? "Kayıtlar yazıldı, ama uzantıyı hangi uygulamanın açacağına Windows henüz karar vermedi: " +
              "aynı uzantı için başka aday uygulamalar da var, bu yüzden çift tıkladığınızda " +
              "**\"Bu dosyayı nasıl açmak istersiniz?\"** penceresi çıkar."
            : $"Windows bu uzantıyı şu anda **{ownerName}** ile açıyor.";

        return
        $"**`{extension}` dosyalarını Runly'ye bağlamak için Windows'un onayı gerekiyor.**\n\n" +
        situation + " Windows, bir kullanıcı bir kez " +
        "\"bu dosyayı şununla aç\" dediğinde bu seçimi korumalı bir anahtarda saklar; hiçbir program bunu " +
        "kendi başına değiştiremez — Runly de değiştirmez, denemez.\n\n" +
        "Değiştirmenin tek yolu sizin onaylamanız. Windows 11'de en güvenilir yol:\n\n" +
        $"1. Bir `{extension}` dosyasına **sağ tıklayın** → **Birlikte aç** → **Başka bir uygulama seç**.\n" +
        "2. Açılan listeden **Runly**'yi seçin.\n" +
        "3. **\"Her zaman\"** düğmesine basın.\n\n" +
        "Aşağıdaki düğme Windows'un dosya türü sayfasını açar. Sayfanın en üstündeki kutuya " +
        $"**{extension}** yazın — Windows kutuyu yalnız Ayarlar kapalıyken açılırsa kendi dolduruyor, " +
        "o yüzden elle yazmak gerekiyor — sonra beliren satırdan **Runly**'yi seçin.\n\n" +
        $"Bu adımı atlarsanız Runly çalışmaya devam eder; yalnızca `{extension}` dosyalarına çift tıklamak " +
        "Runly'yi açmaz. Dosyaya sağ tıklayıp **\"Birlikte aç → Runly\"** diyerek yine de çalıştırabilirsiniz.";
    }

    private static void RenderMarkdownLite(RichTextBox box, string text)
    {
        box.Clear();
        var normalFont = box.Font;
        using var boldFont = new Font(normalFont, FontStyle.Bold);
        using var codeFont = new Font(Palette.MonoFamily, normalFont.Size, FontStyle.Bold);

        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    AppendSegment(box, text[i..], normalFont);
                    break;
                }

                AppendSegment(box, text.Substring(i + 2, end - (i + 2)), boldFont);
                i = end + 2;
            }
            else if (text[i] == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end < 0)
                {
                    AppendSegment(box, text[i..], normalFont);
                    break;
                }

                AppendSegment(box, text.Substring(i + 1, end - (i + 1)), codeFont, Palette.Renk2Text);
                i = end + 1;
            }
            else
            {
                var next = text.IndexOfAny(['*', '`'], i);
                if (next < 0)
                {
                    next = text.Length;
                }

                AppendSegment(box, text[i..next], normalFont);
                i = next;
            }
        }
    }

    private static void AppendSegment(RichTextBox box, string segment, Font font, Color? color = null)
    {
        if (segment.Length == 0)
        {
            return;
        }

        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        box.SelectionFont = font;
        box.SelectionColor = color ?? Palette.TextBody;
        box.AppendText(segment);
    }

    private void OnAddExtensionClicked(object? sender, EventArgs e)
    {
        using var dialog = new AddExtensionDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (_config.Extensions.ContainsKey(dialog.Extension))
        {
            NeonMessageBox.Show(this, $"'{dialog.Extension}' zaten tabloda var.", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (RunlyRegistryLayout.IsBlockedExtension(dialog.Extension))
        {
            NeonMessageBox.Show(this, Strings.Get("extension.blockedAdd"), Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.Extensions[dialog.Extension] = new ExtensionMapping
        {
            Interpreter = dialog.Interpreter,
            Args = dialog.Args,
            Category = "special",
            Enabled = true,
        };

        MarkDirty();
        RefreshExtensionGrid();
    }

    private void OnRemoveExtensionClicked(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Tag is not ExtensionStatus status)
        {
            NeonMessageBox.Show(this, "Silinecek bir uzantı seçin.", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = NeonMessageBox.Show(this,
            $"'{status.Extension}' uzantısını tablodan silmek istediğinize emin misiniz?",
            Strings.Get("app.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _config.Extensions.Remove(status.Extension);
        MarkDirty();
        RefreshExtensionGrid();
    }

    // ---- Security panel -------------------------------------------------------------------

    private void ApplySecurityRadio(SecurityMode mode)
    {
        switch (mode)
        {
            case SecurityMode.AlwaysAsk:
                _radioAlwaysAsk.Checked = true;
                break;
            case SecurityMode.NeverAsk:
                _radioNeverAsk.Checked = true;
                break;
            default:
                _radioTrustOnFirstUse.Checked = true;
                break;
        }
    }

    private SecurityMode GetSelectedSecurityMode() =>
        _radioAlwaysAsk.Checked ? SecurityMode.AlwaysAsk :
        _radioNeverAsk.Checked ? SecurityMode.NeverAsk :
        SecurityMode.TrustOnFirstUse;

    private void OnSecurityRadioChanged(object? sender, EventArgs e)
    {
        if (_initializing || sender is not RadioButton { Checked: true } radio)
        {
            return;
        }

        if (ReferenceEquals(radio, _radioNeverAsk))
        {
            var result = NeonMessageBox.Show(this,
                "Bu ayarla, çift tıkladığınız her script hiçbir soru sorulmadan çalışır. İnternetten\n" +
                "indirilmiş dosyalar yine de uyarı gösterir. Devam edilsin mi?",
                Strings.Get("app.title") + " — Güvenlik uyarısı",
                MessageBoxButtons.YesNo, MessageBoxIcon.Error, MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                ApplySecurityRadio(_lastGoodSecurityMode);
                return;
            }
        }

        _lastGoodSecurityMode = GetSelectedSecurityMode();
        MarkDirty();
    }

    private void RefreshTrustedFolders()
    {
        _trustedFoldersList.Items.Clear();
        foreach (var folder in _trustStore.Data.TrustedFolders)
        {
            _trustedFoldersList.Items.Add(folder);
        }
    }

    private void RefreshTrustedFilesLabel() =>
        _trustedFilesLabel.Text = Strings.Language == "en"
            ? $"{_trustStore.Data.TrustedFiles.Count} files"
            : $"{_trustStore.Data.TrustedFiles.Count} adet";

    private void OnAddTrustedFolder(ListBox foldersList)
    {
        using var picker = new FolderBrowserDialog { Description = "Güvenilecek klasörü seçin" };
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _trustStore.TrustFolder(picker.SelectedPath);
        RefreshTrustedFolders();
        MarkDirty();
        _ = foldersList;
    }

    private void OnRemoveTrustedFolder(ListBox foldersList)
    {
        if (foldersList.SelectedItem is not string folder)
        {
            return;
        }

        _trustStore.UntrustFolder(folder);
        RefreshTrustedFolders();
        MarkDirty();
    }

    private void OnClearTrustedFiles(object? sender, EventArgs e)
    {
        if (_trustStore.Data.TrustedFiles.Count == 0)
        {
            return;
        }

        var confirm = NeonMessageBox.Show(this, "Tüm güvenilen dosya kayıtları silinsin mi?", Strings.Get("app.title"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _trustStore.ClearTrustedFiles();
        RefreshTrustedFilesLabel();
        MarkDirty();
    }

    // ---- Behavior panel -------------------------------------------------------------------

    private void ApplyKeepWindowRadio(KeepWindowMode mode)
    {
        switch (mode)
        {
            case KeepWindowMode.Always:
                _radioKeepAlways.Checked = true;
                break;
            case KeepWindowMode.Never:
                _radioKeepNever.Checked = true;
                break;
            default:
                _radioKeepOnError.Checked = true;
                break;
        }
    }

    private KeepWindowMode GetSelectedKeepWindowMode() =>
        _radioKeepAlways.Checked ? KeepWindowMode.Always :
        _radioKeepNever.Checked ? KeepWindowMode.Never :
        KeepWindowMode.OnError;

    private static readonly HashSet<string> EditorExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "notepad.exe", "notepad++.exe", "code.exe", "code - insiders.exe", "cursor.exe", "windsurf.exe",
        "zed.exe", "sublime_text.exe", "atom.exe", "notepad2.exe", "notepad3.exe", "emeditor.exe",
        "uedit64.exe", "textpad.exe", "gvim.exe", "kate.exe", "wordpad.exe", "write.exe", "devenv.exe",
        "idea64.exe", "pycharm64.exe", "webstorm64.exe", "rider64.exe",
    };

    private static bool LooksLikeEditor(string? path) =>
        !string.IsNullOrWhiteSpace(path) && EditorExecutables.Contains(Path.GetFileName(path.Trim().Trim('"')));

    private void OnChooseEditorClicked(object? sender, EventArgs e)
    {
        using var dialog = new ChooseApplicationDialog(
            ".txt",
            HandlerKind.Open,
            _installedApplications,
            EditorExecutables,
            _editorCommandBox.Text.Trim(),
            UsageHistory.Rank(".txt", _config.Extensions),
            Strings.Get("chooseApp.promptEditor"));

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _editorCommandBox.Text = dialog.SelectedPath;
        _logger.Info($"Düzenleyici seçildi: {dialog.SelectedPath}");
    }

    private void OnTestEditorClicked(object? sender, EventArgs e)
    {
        var command = string.IsNullOrWhiteSpace(_editorCommandBox.Text) ? "notepad" : _editorCommandBox.Text.Trim();
        try
        {
            Process.Start(new ProcessStartInfo { FileName = command, UseShellExecute = true })?.Dispose();
            NeonMessageBox.Show(this, $"'{command}' başlatıldı.", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            NeonMessageBox.Show(this, $"'{command}' başlatılamadı: {ex.Message}", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnContextMenuClicked(object? sender, EventArgs e)
    {
        var config = _config;
        IReadOnlyList<ContextMenuItem> items;

        SetBusy(true, "Sağ menü taranıyor…");
        try
        {
            items = await Task.Run(() => _menuCleaner.Scan(config));
        }
        catch (Exception ex)
        {
            _logger.Error("Sağ menü taranamadı", ex);
            NeonMessageBox.Show(this, $"Sağ menü taranamadı: {ex.Message}", Strings.Get("menu.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            SetBusy(false, null);
        }

        using var dialog = new ContextMenuDialog(items, ContextMenuCleaner.DesiredIds(config, items),
            ContextMenuCleaner.EverywhereIds(config), config);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var chosen = dialog.SelectedIds;
        var recommended = items.Where(i => i.Recommended).Select(i => i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hidden = recommended.SetEquals(chosen) ? null : chosen.ToList();
        var everywhere = dialog.EverywhereIds.Count == 0 ? null : dialog.EverywhereIds.ToList();
        var elevationChanged = ApplyAdminOverrides(dialog.AdminOverrides);

        try
        {
            _configStore.Save(_configStore.Load() with
            {
                HiddenMenuItems = hidden,
                HiddenEverywhere = everywhere,
                Extensions = CreateSparseExtensions(),
            });
            _configStamp = ReadConfigStamp();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Sağ menü seçimi kaydedilemedi", ex);
            NeonMessageBox.Show(this, $"Ayarlar kaydedilemedi: {ex.Message}", Strings.Get("menu.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _config = _config with { HiddenMenuItems = hidden, HiddenEverywhere = everywhere };
        config = _config;

        MenuCleanupResult result;
        SetBusy(true, "Sağ menü uygulanıyor…");
        try
        {
            result = await Task.Run(() => _menuCleaner.Apply(config));
        }
        catch (Exception ex)
        {
            _logger.Error("Sağ menü uygulanamadı", ex);
            NeonMessageBox.Show(this, $"Sağ menü uygulanamadı: {ex.Message}", Strings.Get("menu.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            SetBusy(false, null);
        }

        foreach (var action in result.Actions)
        {
            _logger.Info(action);
        }

        _progressLabel.Text = Strings.Get("menu.applied");

        // The elevation choice lives in the ProgID verb itself, so it only reaches the menu on a rewrite.
        if (elevationChanged)
        {
            await RunInstallAsync();
        }

        if (result.ExplorerRestartNeeded)
        {
            OfferExplorerRestart();
        }
    }

    private bool ApplyAdminOverrides(IReadOnlyDictionary<string, bool?> overrides)
    {
        var changed = false;
        foreach (var (extension, choice) in overrides)
        {
            var mapping = EffectiveMapping(extension);
            if (mapping.RunAsAdmin == choice)
            {
                continue;
            }

            _config.Extensions[extension] = mapping with { RunAsAdmin = choice };
            changed = true;
        }

        return changed;
    }

    private void OfferExplorerRestart()
    {
        var answer = NeonMessageBox.Show(this, Strings.Get("menu.restartPrompt"), Strings.Get("menu.title"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            ExplorerRestarter.Restart();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            _logger.Error("Explorer yeniden başlatılamadı", ex);
            NeonMessageBox.Show(this, $"Explorer yeniden başlatılamadı: {ex.Message}", Strings.Get("menu.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---- Bottom bar: install / uninstall / restore / save ----------------------------------

    private static string ExePath => Path.Combine(AppContext.BaseDirectory, RunlyRegistryLayout.LauncherFileName);

    private static string ConsoleExePath => Path.Combine(AppContext.BaseDirectory, RunlyRegistryLayout.ConsoleLauncherFileName);

    private async void OnInstallClicked(object? sender, EventArgs e)
    {
        var (success, pending) = await RunInstallAsync();
        if (!success || pending.Count == 0)
        {
            return;
        }

        if (OfferBulkAssignment(pending))
        {
            return;
        }

        if (pending.Count == 1)
        {
            OpenDefaultAppsForExtension(pending[0]);
        }
        else
        {
            OpenDefaultAppsSettings();
        }
    }

    private async Task<(bool Success, IReadOnlyList<string> Pending)> RunInstallAsync()
    {
        var exePath = ExePath;
        var consoleExePath = ConsoleExePath;

        // Registration writes these paths into every ProgID's shell\open\command. Running the settings
        // window straight out of its build output has neither launcher beside it — they are separate
        // projects — so installing from there used to silently register a path that does not exist and
        // break every association it touched. K29: one missing binary is enough to break half the
        // mappings, so both are required before a single key is written.
        var missing = new[] { exePath, consoleExePath }.Where(path => !File.Exists(path)).ToArray();
        if (missing.Length > 0)
        {
            NeonMessageBox.Show(this,
                Strings.Get("install.launcherMissing").Replace("{path}", string.Join("\n", missing), StringComparison.Ordinal),
                Strings.Get("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            _logger.Error($"Kurulum reddedildi, başlatıcı yok: {string.Join(", ", missing)}", new FileNotFoundException(missing[0]));
            return (false, []);
        }

        SetBusy(true, "Kuruluyor…");
        try
        {
            var result = await Task.Run(() => _shellRegistrar.Install(_config, exePath, consoleExePath));

            if (!result.Success)
            {
                ResultDialog.Show(this, "Kurulum hatası", false, result.Actions, result.ErrorMessage);
                return (false, []);
            }

            if (result.ExplorerRestartNeeded)
            {
                OfferExplorerRestart();
            }

            return (true, result.Extensions
                .Where(x => x.Bound == BindingState.NeedsUserChoice)
                .Select(x => x.Extension)
                .ToArray());
        }
        catch (Exception ex)
        {
            _logger.Error("Kurulum sırasında hata", ex);
            NeonMessageBox.Show(this, $"Kurulum sırasında beklenmeyen bir hata oluştu: {ex.Message}", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return (false, []);
        }
        finally
        {
            SetBusy(false, null);
            RefreshExtensionGrid();
            RefreshStatusStrip();
        }

    }

    private async void OnUninstallClicked(object? sender, EventArgs e)
    {
        using var confirm = new UninstallConfirmDialog();
        if (confirm.ShowDialog(this) != DialogResult.Yes)
        {
            return;
        }

        List<OrphanedUserChoice>? pendingRepair = null;

        SetBusy(true, "Kaldırılıyor…");
        try
        {
            var options = new UninstallOptions { RestoreBackup = confirm.RestoreBackup };
            var result = await Task.Run(() => _shellRegistrar.Uninstall(options));

            var lines = new List<string>(result.Actions);
            if (result.RestoredBackupPath is not null)
            {
                lines.Add($"Geri yüklenen yedek: {result.RestoredBackupPath}");
            }

            var orphans = result.AffectedUserChoices.Where(o => !o.Removed).ToList();
            string? headline = null;

            if (orphans.Count > 0)
            {
                // Decision K20: never claim a clean removal while an extension still points at a deleted ProgID.
                headline = $"Runly kaldırıldı, ama {orphans.Count} uzantı geçersiz bir bağlantıyla kaldı.";

                lines.Add(string.Empty);
                lines.Add("Windows'un \"Birlikte aç\" seçimi (UserChoice) silinemeyen uzantılar:");
                foreach (var orphan in orphans)
                {
                    lines.Add($"  {orphan.Extension} → {orphan.ProgId} (artık yok) — {orphan.FailureReason}");
                }

                lines.Add(string.Empty);
                lines.Add("Bu uzantılara çift tıkladığınızda Windows \"Bu dosyayı nasıl açmak istersiniz?\"");
                lines.Add("diye soracak. Kalıcı olarak düzeltmek için her biri için bir uygulama seçin.");
            }

            ResultDialog.Show(this, "Kaldırma sonucu", result.Success, lines, result.ErrorMessage, headline);

            if (result.ExplorerRestartNeeded)
            {
                OfferExplorerRestart();
            }

            if (orphans.Count > 0)
            {
                pendingRepair = orphans;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Kaldırma sırasında hata", ex);
            NeonMessageBox.Show(this, $"Kaldırma sırasında beklenmeyen bir hata oluştu: {ex.Message}", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, null);
            RefreshExtensionGrid();
            RefreshStatusStrip();
        }

        if (pendingRepair is not null)
        {
            OfferOrphanRepair(pendingRepair);
        }
    }

    /// <summary>
    /// Opens Windows Default apps for orphaned protected choices after uninstall. Runly cannot fix these
    /// itself because the UserChoice key is protected and its hash must never be forged.
    /// </summary>
    private void OfferOrphanRepair(IReadOnlyList<OrphanedUserChoice> orphans)
    {
        var list = string.Join(", ", orphans.Select(o => o.Extension));

        var answer = NeonMessageBox.Show(
            this,
            $"Şu uzantılar hâlâ silinmiş bir Runly kaydına bağlı: {list}\n\n" +
            "Windows bu seçimi korumalı bir anahtarda tutuyor ve silinmesine izin vermiyor; " +
            "yalnızca siz değiştirebilirsiniz.\n\n" +
            "Bu uzantılara yeni bir varsayılan seçmek için Windows Varsayılan uygulamalar sayfası açılsın mı?",
            "Geçersiz kalan dosya ilişkileri",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        OpenDefaultAppsSettings();
    }

    /// <summary>
    /// Opens the Windows page where a per-extension default can actually be granted.
    /// <para>
    /// <c>registeredAppUser=Runly</c> is deliberately not used for a single extension. Measured on
    /// Windows 11: that page lists the file types Runly is <b>already</b> the default for, not the
    /// ones it declares in <c>Capabilities\FileAssociations</c> — this machine listed <c>.pl</c> and
    /// <c>.sh</c> (present only as a UserChoice) while omitting <c>.md</c> (present only in
    /// capabilities). An extension therefore appears there only after it is bound, which is exactly
    /// too late to be useful. <c>ftfilter</c> opens the "choose a default by file type" list already
    /// filtered to the extension, where the choice can be made.
    /// </para>
    /// </summary>
    private void OpenDefaultAppsForExtension(string extension) =>
        OpenSettingsUri("ms-settings:defaultapps?ftfilter=" + RunlyConfig.NormalizeExtension(extension));

    private void OpenDefaultAppsSettings(bool forRunly = false) =>
        OpenSettingsUri(forRunly ? "ms-settings:defaultapps?registeredAppUser=Runly" : "ms-settings:defaultapps");

    private void OpenSettingsUri(string settingsUri)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = settingsUri, UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error("\"Varsayılan uygulamalar\" ayarları açılamadı", ex);
            NeonMessageBox.Show(this, $"Ayarlar açılamadı: {ex.Message}", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        var backups = _registryBackup.ListBackups();
        if (backups.Count == 0)
        {
            NeonMessageBox.Show(this, "Hiç yedek bulunamadı.", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var picker = new RestoreBackupDialog(backups);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedBackup is null)
        {
            return;
        }

        var confirm = NeonMessageBox.Show(this,
            $"'{picker.SelectedBackup.FileName}' yedeği geri yüklensin mi? Bu, kayıt defterindeki Runly ile " +
            "ilgili anahtarları yedekteki hâline döndürür.",
            Strings.Get("app.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true, "Yedek geri yükleniyor…");
        try
        {
            var backupPath = picker.SelectedBackup.Path;
            await Task.Run(() => _registryBackup.RestoreBackup(backupPath));
            NeonMessageBox.Show(this, "Yedek geri yüklendi.", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.Error("Yedek geri yükleme hatası", ex);
            NeonMessageBox.Show(this, $"Yedek geri yüklenemedi: {ex.Message}", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, null);
            RefreshExtensionGrid();
            RefreshStatusStrip();
        }
    }

    private void SetBusy(bool busy, string? statusText)
    {
        _installButton.Enabled = !busy;
        _uninstallButton.Enabled = !busy;
        _restoreButton.Enabled = !busy;
        _saveButton.Enabled = !busy;
        _progressLabel.Text = statusText ?? string.Empty;
        UseWaitCursor = busy;
    }

    private void SaveAll()
    {
        var toSave = _config with
        {
            SecurityMode = GetSelectedSecurityMode(),
            KeepWindowOpen = GetSelectedKeepWindowMode(),
            EditorCommand = _editorCommandBox.Text.Trim(),
            LogEnabled = _logEnabledCheck.Checked,
            RunAsAdmin = _runAsAdminCheck.Checked,
            Language = Strings.Language,
            Extensions = CreateSparseExtensions(),
        };

        if (ReadConfigStamp() > _configStamp && !ConfirmOverwriteExternalEdit())
        {
            return;
        }

        try
        {
            _configStore.Save(toSave);
            _trustStore.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Kaydetme sırasında hata", ex);
            NeonMessageBox.Show(this, $"Ayarlar kaydedilemedi: {ex.Message}", Strings.Get("app.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _configStamp = ReadConfigStamp();
        _config = toSave;
        _dirty = false;
        UpdateTitle();
        _progressLabel.Text = "Kaydedildi ✓";
    }

    private Dictionary<string, ExtensionMapping> CreateSparseExtensions()
    {
        var result = RunlyConfig.CreateExtensionDictionary();
        foreach (var pair in _config.Extensions)
        {
            var entry = ExtensionCatalog.Entries.FirstOrDefault(item =>
                string.Equals(item.Extension, pair.Key, StringComparison.OrdinalIgnoreCase));
            if (entry is null || !MatchesCatalogDefault(pair.Value, entry)) result[pair.Key] = pair.Value;
        }
        return result;
    }

    private static bool MatchesCatalogDefault(ExtensionMapping mapping, CatalogEntry entry) =>
        !mapping.Enabled && mapping.Kind == entry.DefaultKind &&
        string.Equals(mapping.Category, entry.Category, StringComparison.Ordinal) &&
        (string.IsNullOrWhiteSpace(mapping.TypeName) || string.Equals(mapping.TypeName, entry.DisplayName.Tr, StringComparison.Ordinal)) &&
        string.IsNullOrWhiteSpace(mapping.Interpreter) && string.IsNullOrWhiteSpace(mapping.OpenWith) &&
        (string.IsNullOrWhiteSpace(mapping.Args) || string.Equals(mapping.Args, "\"{script}\" {args}", StringComparison.Ordinal)) &&
        string.IsNullOrWhiteSpace(mapping.Icon);

    private DateTime ReadConfigStamp()
    {
        try
        {
            return File.Exists(_configStore.ConfigPath) ? File.GetLastWriteTimeUtc(_configStore.ConfigPath) : DateTime.MinValue;
        }
        catch (IOException)
        {
            return _configStamp;
        }
        catch (UnauthorizedAccessException)
        {
            return _configStamp;
        }
    }

    private bool ConfirmOverwriteExternalEdit()
    {
        var answer = NeonMessageBox.Show(this,
            "Ayar dosyası bu pencere açıkken dışarıdan değiştirildi. Kaydederseniz o değişiklikler " +
            "bu pencerenin bildiği hâlle değiştirilir.\n\nYine de kaydedilsin mi?",
            Strings.Get("app.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        return answer == DialogResult.Yes;
    }

    private void ChangeLanguage(string language)
    {
        Strings.Language = language == "en" ? "en" : "tr";
        ApplyLanguage();
        SaveAll();
    }

    private void ChangeUiScale()
    {
        var next = UiScale.Next(_config.UiScale);
        _config = _config with { UiScale = next };
        ApplyLanguage();
        SaveAll();
        var question = Strings.Get("scale.restart").Replace("{percent}", next.ToString(CultureInfo.InvariantCulture));
        if (NeonMessageBox.Show(this, question, Strings.Get("app.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes && !_dirty)
        {
            Application.Restart();
        }
    }

    private void ApplyLanguage()
    {
        _suppressGridEvents = true;
        _grid.Rows.Clear();
        Strings.Apply(this);
        _categoryRailColumn.Width = CategoryRailWidth();
        Text = Strings.Get("app.title") + (_dirty ? " *" : string.Empty);
        _grid.Columns[ColEnabled].HeaderText = Strings.Get("enabled");
        _grid.Columns[ColExtension].HeaderText = Strings.Get("extension");
        _grid.Columns[ColKind].HeaderText = Strings.Get("kind.column");
        _grid.Columns[ColInterpreter].HeaderText = Strings.Get("interpreter");
        _grid.Columns[ColFound].HeaderText = Strings.Get("found");
        _grid.Columns[ColArgs].HeaderText = Strings.Get("arguments");
        _grid.Columns[ColStatus].HeaderText = Strings.Get("status");
        UpdateSearchResultLabel();
        _captionLanguage.Text = Strings.Language == "tr" ? "TR | en" : "tr | EN";
        _captionScale.Text = Strings.Get("scale.caption").Replace("{percent}", _config.UiScale.ToString(CultureInfo.InvariantCulture));
        _captionSponsor.Text = Strings.Get("sig.support");
        _captionHelp.Text = Strings.Get("caption.help");
        RenderUpdateBadge();
        RefreshExtensionGrid();
        RefreshTrustedFilesLabel();
        RefreshStatusStrip();
        UpdateDetailPanel();
        _suppressGridEvents = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _searchDebounce.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Hands the update channel to the caption badge. Kept out of the constructor so the UI audit
    /// and tests build the window without touching the network.</summary>
    public void AttachUpdates(UpdateController controller)
    {
        _updates = controller;
        controller.Changed += (_, _) => RenderUpdateBadge();
        RenderUpdateBadge();
    }

    private void RenderUpdateBadge()
    {
        var stage = _updates?.Stage ?? UpdateStage.None;
        _captionUpdate.Visible = stage != UpdateStage.None;
        _captionUpdate.Dot = stage switch
        {
            UpdateStage.Downloading => Palette.Renk1,
            UpdateStage.Ready or UpdateStage.Restarting => Palette.Success,
            UpdateStage.Failed => Palette.Renk2,
            _ => Palette.Warning,
        };
        _captionUpdate.Text = stage == UpdateStage.Downloading
            ? Strings.Get("update.label") + " %" + Math.Floor(_updates!.Percent).ToString(CultureInfo.InvariantCulture)
            : Strings.Get("update.label");
        RefreshCaptionItems();
    }

    private async void CheckForUpdatesNow()
    {
        if (_updates is null)
        {
            return;
        }

        if (_updates.Stage != UpdateStage.None)
        {
            OnUpdateBadgeClick();
            return;
        }

        var found = await _updates.CheckAsync();
        if (found == true)
        {
            OnUpdateBadgeClick();
            return;
        }

        _progressLabel.Text = Strings.Get(found == false ? "update.current" : "update.error");
    }

    private void OnUpdateBadgeClick()
    {
        if (_updates is null)
        {
            return;
        }

        if (_updates.Stage == UpdateStage.Available)
        {
            _updates.Download(installWhenReady: false);
        }
        else if (_updates.Stage == UpdateStage.Ready)
        {
            _updates.Install();
        }

        if (_updatePanel is null || _updatePanel.IsDisposed)
        {
            _updatePanel = new UpdatePanel(_updates);
            _updatePanel.Show(this);
        }
        else
        {
            _updatePanel.Activate();
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_dirty)
        {
            return;
        }

        var result = NeonMessageBox.Show(this,
            "Kaydedilmemiş değişiklikler var. Kapatmadan önce kaydetmek ister misiniz?",
            Strings.Get("app.title"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (result == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == DialogResult.Yes)
        {
            SaveAll();
        }
    }

    private void MarkDirty()
    {
        if (_dirty)
        {
            return;
        }

        _dirty = true;
        UpdateTitle();
    }

    private void MarkDirtyUnlessInitializing()
    {
        if (!_initializing)
        {
            MarkDirty();
        }
    }

    private void UpdateTitle() => Text = Strings.Get("app.title") + (_dirty ? " *" : string.Empty);

    // ---- Status strip -----------------------------------------------------------------------

    private void RefreshStatusStrip()
    {
        var exePath = ExePath;
        // K29: the strip has room for one path, so it shows the missing binary when there is one —
        // naming the launcher that is present would hide exactly the fault the user needs to see.
        var missingLauncher = new[] { exePath, ConsoleExePath }.FirstOrDefault(path => !File.Exists(path));
        var statuses = _shellRegistrar.GetStatus(_config);
        var bound = statuses.Count(s => s.Bound == BindingState.Bound);
        var pending = statuses.Count(s => s.Bound == BindingState.NeedsUserChoice);

        // "Kurulu ✅" on its own would repeat the old lie: registered is not the same as double-click works.
        if (bound + pending == 0)
        {
            _statusLabel.Text = Strings.Get("notInstalled");
            _statusLabel.ForeColor = Palette.TextHint;
        }

        else if (pending == 0)
        {
            _statusLabel.Text = Strings.Language == "en"
                ? $"Runly installed ✅ — {bound} extensions bound"
                : $"Runly kurulu ✅ — {bound} uzantı bağlı";
            _statusLabel.ForeColor = Palette.Success;
        }
        else
        {
            _statusLabel.Text = Strings.Language == "en"
                ? $"Runly installed ⚠ — {bound} extensions bound, {pending} awaiting Windows approval"
                : $"Runly kurulu ⚠ — {bound} uzantı bağlı, {pending} uzantı Windows onayı bekliyor";
            _statusLabel.ForeColor = NeedsChoiceFore;
        }

        _captionStatus.Dot = bound + pending == 0 ? Palette.TextHint : Palette.Success;
        _captionStatus.Text = bound + pending == 0 ? Strings.Get("notInstalled") : Strings.Get("installed");

        var exeFullText = missingLauncher is null ? exePath : $"{missingLauncher} (bulunamadı)";
        _exePathLabel.Text = ShortenPathMiddle(exeFullText, 42);
        _statusTip.SetToolTip(_exePathLabel, exeFullText);

        _captionVersion.Text = "v" + GetVersionText(exePath).TrimStart('v');

        var configFullText = "config: " + _configStore.ConfigPath;
        _configPathLink.Text = ShortenPathMiddle(configFullText, 42);
        _statusTip.SetToolTip(_configPathLink, configFullText);

        RefreshCaptionItems();
    }

    // D3 fix (R5 yönetici incelemesi): tam yol, durum şeridinin dar Fill alanına sığmayınca pencere
    // kenarında görünmez şekilde kırpılıyordu. Orta kısmı elenmiş kısaltılmış metin gösterilir,
    // tam yol ise ToolTip ile erişilebilir kalır.
    private static string ShortenPathMiddle(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        var tail = text.Length > 24 ? text[^24..] : text;
        var headBudget = maxChars - tail.Length - 1;
        if (headBudget < 4)
        {
            return "…" + tail;
        }

        return text[..headBudget] + "…" + tail;
    }

    private static string GetVersionText(string exePath)
    {
        try
        {
            if (File.Exists(exePath))
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                {
                    return info.ProductVersion.Split('+', 2)[0];
                }
            }
        }
        catch (Exception ex) when (ex is IOException or Win32Exception)
        {
            // Fall through to the assembly version below.
        }

        return typeof(MainForm).Assembly.GetName().Version?.ToString() ?? "bilinmiyor";
    }

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true })?.Dispose();

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true })?.Dispose();
    }

    private static void OpenContainingFolder(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{filePath}\"", UseShellExecute = true })?.Dispose();
    }
}
