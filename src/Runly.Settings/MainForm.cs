using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using Microsoft.Win32;
using System.Reflection;
using Runly.Core.Abstractions;
using Runly.Core.Defaults;
using Runly.Core.Models;
using Runly.Core.Paths;
using Runly.Core.Shell;
using Runly.Core.Services;
using Runly.Settings.Dialogs;
using Runly.Settings.Catalog;
using Runly.Settings.Discovery;

namespace Runly.Settings;

/// <summary>The single settings window: status strip, extension table, security/behavior panels, bottom bar (SPEC 10).</summary>
internal sealed class MainForm : NeonForm
{
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
    private static readonly Color BoundFore = Palette.Success;
    private static readonly Color NeedsChoiceBack = Tint(Palette.NeonPink, 40);
    private static readonly Color NeedsChoiceFore = Palette.NeonPink;
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
    private static Label SectionLabel(string text, Padding margin) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Palette.LabelFont,
        ForeColor = Palette.TextLabel,
        Margin = margin,
    };

    // Absolute rows only exist where AutoSize has already misjudged the content once (see the comments at
    // each use). They stay absolute, but the number is composed from what has to fit, so a taller font
    // grows the slot instead of clipping inside it.
    private static int FolderButtonHeight => Metrics.ButtonMinHeight;

    private static int FolderButtonWidth => Metrics.Px(78);

    /// Three stacked radios; 72 design pixels fitted only two and painted the folders label over the third.
    private static int RadioStackHeight => Metrics.Stack(Palette.Body, 3, 13);

    /// Two stacked folder buttons plus the margin between them; one button used to be cut in half here.
    private static int FoldersAreaHeight => (FolderButtonHeight * 2) + Metrics.Px(18);

    private static int TrustedFilesRowHeight => Metrics.ButtonHeight + Metrics.Px(4);

    private static int EditorRowHeight => Metrics.ButtonHeight + Metrics.Px(12);

    /// The strip's own slot plus the 12-pixel gap its bottom margin opens to the table. The margin is
    /// part of the row: an Absolute row does not grow for it, and the difference lands as a clipped
    /// button rather than as a shorter gap.
    private static int SearchStripHeight => Metrics.ButtonHeight + Metrics.Px(16) + Metrics.Px(12);

    private static int ExtensionButtonsHeight => Metrics.ButtonHeight + Metrics.Px(16);

    /// The security panel is the taller of the two, so it sets the row both of them share.
    /// The security panel is the taller of the two, so it sets the row both of them share. The leading
    /// term is the gap above the pair, which belongs to the row as well: the panels are docked into it,
    /// so padding the container without paying for it here just eats the panel's own bottom padding.
    private static int PanelsRowHeight =>
        Metrics.Px(24) + Metrics.GroupTitleBand + Metrics.Px(16) + RadioStackHeight +
        Metrics.SectionLabelHeight + FoldersAreaHeight + TrustedFilesRowHeight + Metrics.Px(24);

    /// One button row inside the strip's 16/16 padding. The footer that used to sit under it moved into
    /// the caption band, so nothing else shares this row any more.
    private static int BottomBarHeight => Metrics.ButtonHeight + Metrics.Px(32);

    private readonly IConfigStore _configStore;
    private readonly ITrustStore _trustStore;
    private readonly IShellRegistrar _shellRegistrar;
    private readonly RegistryBackup _registryBackup;
    private readonly ILogger _logger;
    private RunlyConfig _config;

    private bool _dirty;
    private bool _initializing = true;
    private bool _suppressGridEvents;
    private bool _autoRefreshInFlight;
    private DateTime _lastAutoRefresh = DateTime.MinValue;

    /// <summary>B6: the config file's timestamp when this window last read or wrote it. A newer stamp
    /// on disk means someone edited the file behind our back, and saving would silently revert them.</summary>
    private DateTime _configStamp = DateTime.MinValue;

    private readonly DataGridView _grid;
    private readonly ListBox _categoryList;
    private readonly TextBox _searchBox;
    private readonly Label _searchResultLabel;
    private readonly Button _chooseAppButton;
    private readonly System.Windows.Forms.Timer _searchDebounce;
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

    private readonly ToolTip _statusTip = new();

    private readonly Button _installButton;
    private readonly Button _uninstallButton;
    private readonly Button _restoreButton;
    private readonly Button _saveButton;
    private readonly Label _progressLabel;
    private readonly CaptionItem _captionStatus;
    private readonly CaptionItem _captionVersion;
    private readonly CaptionItem _captionLanguage;
    private readonly CaptionItem _captionSponsor;

    /// <summary>Builds the whole window from code; see SPEC 10 for the layout this follows.</summary>
    public MainForm(
        IConfigStore configStore,
        RunlyConfig config,
        ITrustStore trustStore,
        IShellRegistrar shellRegistrar,
        RegistryBackup registryBackup,
        ILogger logger)
    {
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
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Palette.AppBg };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // A flat 270 could not hold the security panel once the third radio and the second folder button
        // were given real room, which is why this is now the sum of those parts rather than a number.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, PanelsRowHeight));
        // One button row plus the signature line; keeps the two visually joined.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, BottomBarHeight));

        // The status strip was removed: it repeated the footer indicator and its 13.5pt line clipped
        // descenders. These four stay unparented — code paths still set their Text without a UI slot.
        var buttonGap = new Padding(Metrics.Px(12), 0, 0, 0);
        _refreshButton = new NeonButton { Text = "Yenile", Primary = false, AutoSize = true, Margin = buttonGap };
        _refreshButton.Click += (_, _) => RefreshStatusOnly(force: true);
        _statusLabel = new Label { Visible = false };
        _exePathLabel = new Label { Visible = false };
        _configPathLink = new LinkLabel { Visible = false };
        _configPathLink.LinkClicked += (_, _) => OpenContainingFolder(_configStore.ConfigPath);

        // ---- 2. Extension table + detail panel -------------------------------------------
        var gridArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, BackColor = Palette.AppBg, Padding = new Padding(Metrics.Px(24), Metrics.Px(16), Metrics.Px(24), 0) };
        gridArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Metrics.Px(210)));
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
            Margin = new Padding(0, 0, Metrics.Px(16), 0),
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

        var detailPanel = new NeonGroupPanel(Strings.Get("details")) { Dock = DockStyle.Fill, Margin = new Padding(Metrics.Px(16), 0, 0, 0) };
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

        var extButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        var extButtonMargin = new Padding(0, Metrics.Px(4), Metrics.Px(12), Metrics.Px(4));
        var selectAllButton = new NeonButton { Text = "Tümünü seç", Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        var addExtButton = new NeonButton { Text = "Uzantı ekle", Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        var removeExtButton = new NeonButton { Text = "Seçili uzantıyı sil", Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        var exportButton = new NeonButton { Text = Strings.Get("profile.export"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        var importButton = new NeonButton { Text = Strings.Get("profile.import"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        selectAllButton.Click += (_, _) => SetAllExtensionsEnabled();
        addExtButton.Click += OnAddExtensionClicked;
        removeExtButton.Click += OnRemoveExtensionClicked;
        exportButton.Click += (_, _) => ExportProfile();
        importButton.Click += (_, _) => ImportProfile();
        extButtons.Controls.Add(selectAllButton);
        extButtons.Controls.Add(addExtButton);
        extButtons.Controls.Add(removeExtButton);
        extButtons.Controls.Add(exportButton);
        extButtons.Controls.Add(importButton);
        _chooseAppButton = new NeonButton { Text = Strings.Get("catalog.chooseApp"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = extButtonMargin };
        _chooseAppButton.Click += (_, _) => ChooseApplicationForSelectedRow();
        extButtons.Controls.Add(_chooseAppButton);

        // Two columns, not one flow: the bulk-assign pair is AutoSize on the right and the search
        // group absorbs the slack, so neither can be pushed off the edge at MinimumSize.
        var searchStrip = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Metrics.Px(12)) };
        searchStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchStrip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var searchGroup = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
        var searchLabel = new Label { Text = Strings.Get("catalog.searchLabel"), AutoSize = true, Font = Palette.H3, ForeColor = Palette.NeonBlue, Margin = new Padding(0, Metrics.Px(11), Metrics.Px(8), Metrics.Px(4)) };
        _searchBox = new NeonTextBox { Width = Metrics.Px(280), PlaceholderText = Strings.Get("catalog.searchPlaceholder"), Margin = new Padding(0, Metrics.Px(8), Metrics.Px(8), Metrics.Px(4)) };

        // The catalog carries 400+ rows and every refresh reprojects and refills the whole grid, so
        // rebuilding on each keystroke makes typing stutter. Only typing is delayed: ApplyLanguage and
        // the other call sites keep calling RefreshExtensionGrid directly and stay immediate.
        _searchDebounce = new System.Windows.Forms.Timer { Interval = SearchDebounceMs };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            RefreshExtensionGrid();
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _searchDebounce.Stop();
            _searchDebounce.Start();
        };
        _searchBox.KeyDown += OnSearchBoxKeyDown;
        var clearSearchButton = new NeonButton { Text = Strings.Get("catalog.searchClear"), Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = new Padding(0, Metrics.Px(5), Metrics.Px(12), Metrics.Px(4)) };
        clearSearchButton.Click += (_, _) => ClearSearch();
        _searchResultLabel = new Label { AutoSize = true, Font = Palette.MonoBody, ForeColor = Palette.NeonPink, Margin = new Padding(0, Metrics.Px(11), 0, Metrics.Px(4)) };
        searchGroup.Controls.Add(searchLabel);
        searchGroup.Controls.Add(_searchBox);
        searchGroup.Controls.Add(clearSearchButton);
        searchGroup.Controls.Add(_searchResultLabel);

        var bulkGroup = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
        _bulkAppBox = new NeonComboBox { Width = Metrics.Px(220), Margin = new Padding(Metrics.Px(8), Metrics.Px(8), Metrics.Px(8), Metrics.Px(4)) };
        foreach (var app in _installedApplications) _bulkAppBox.Items.Add(app);
        _bulkAppBox.DisplayMember = nameof(InstalledApplication.DisplayName);
        var bulkButton = new NeonButton { Text = Strings.Get("catalog.bulkOpen"), Primary = true, AutoSize = true, Margin = new Padding(0, Metrics.Px(5), 0, Metrics.Px(4)) };
        bulkButton.Click += (_, _) => AssignCategoryToSelectedApplication();
        bulkGroup.Controls.Add(_bulkAppBox);
        bulkGroup.Controls.Add(bulkButton);

        searchStrip.Controls.Add(searchGroup, 0, 0);
        searchStrip.Controls.Add(bulkGroup, 1, 0);

        _categoryList.SelectedIndex = 0;
        gridArea.Controls.Add(searchStrip, 0, 0);
        gridArea.SetColumnSpan(searchStrip, 3);
        gridArea.Controls.Add(extButtons, 0, 2);
        gridArea.SetColumnSpan(extButtons, 3);

        Shown += (_, _) => _searchBox.Focus();
        KeyPreview = true;
        KeyDown += OnMainFormKeyDown;

        root.Controls.Add(gridArea, 0, 0);

        // ---- 3 & 4. Security + behavior panels --------------------------------------------
        var panelsRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Palette.AppBg, Padding = new Padding(Metrics.Px(24), Metrics.Px(24), Metrics.Px(24), 0) };
        panelsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panelsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        (_radioAlwaysAsk, _radioTrustOnFirstUse, _radioNeverAsk, _trustedFoldersList, _trustedFilesLabel, var securityGroup) = BuildSecurityPanel();
        (_radioKeepAlways, _radioKeepOnError, _radioKeepNever, _editorCommandBox, _logEnabledCheck, var behaviorGroup) = BuildBehaviorPanel();

        panelsRow.Controls.Add(securityGroup, 0, 0);
        panelsRow.Controls.Add(behaviorGroup, 1, 0);
        root.Controls.Add(panelsRow, 0, 1);

        // ---- 5. Bottom bar --------------------------------------------------------------------
        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Padding = new Padding(Metrics.Px(24), Metrics.Px(16), Metrics.Px(24), Metrics.Px(16)), BackColor = Palette.Surface };
        bottomBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Two columns instead of a Dock=Left label: a fixed 320px label starved the RightToLeft button
        // flow at MinimumSize and clipped the leftmost button ("Kur / Güncelle" rendered as "Güncelle").
        // The buttons now take the width they need and the progress label absorbs whatever is left.
        var buttonsRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _progressLabel = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = Palette.MonoBody, ForeColor = Palette.NeonBlue, Margin = Padding.Empty };
        // Both layout panels default to a 3px margin. Nested three deep under a strip sized to exactly
        // one button, that is what pushed the row past the window edge and sliced the buttons in half.
        var buttonsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };

        var closeButton = new NeonButton { Text = "Kapat", Primary = false, AutoSize = true, Margin = buttonGap };
        _saveButton = new NeonButton { Text = "Kaydet", Primary = false, AutoSize = true, Margin = buttonGap };
        _restoreButton = new NeonButton { Text = "Yedekten geri yükle", Primary = false, AutoSize = true, Margin = buttonGap };
        _uninstallButton = new NeonButton { Text = "Kaldır", Primary = false, AutoSize = true, Margin = buttonGap };
        _installButton = new NeonButton { Text = "Kur / Güncelle", Primary = true, AutoSize = true, Margin = buttonGap };

        closeButton.Click += (_, _) => Close();
        _saveButton.Click += (_, _) => SaveAll();
        _restoreButton.Click += OnRestoreClicked;
        _uninstallButton.Click += OnUninstallClicked;
        _installButton.Click += OnInstallClicked;

        buttonsFlow.Controls.Add(closeButton);
        buttonsFlow.Controls.Add(_refreshButton);
        buttonsFlow.Controls.Add(_saveButton);
        buttonsFlow.Controls.Add(_restoreButton);
        buttonsFlow.Controls.Add(_uninstallButton);
        buttonsFlow.Controls.Add(_installButton);

        buttonsRow.Controls.Add(_progressLabel, 0, 0);
        buttonsRow.Controls.Add(buttonsFlow, 1, 0);
        bottomBar.Controls.Add(buttonsRow, 0, 0);

        root.Controls.Add(bottomBar, 0, 2);

        Controls.Add(root);

        // ---- 6. Caption band: status, version, language, support link, signature ----------------
        // R5 4 and 5.3: the support link and the signature belong immediately left of the window
        // buttons, and the strip they used to live in is gone. Items are handed over right to left.
        _captionStatus = new CaptionItem { Font = Palette.Body, Color = Palette.TextStrong, Dot = Palette.TextHint };
        _captionVersion = new CaptionItem { Font = Palette.MonoBody, Color = Palette.NeonBlue };
        _captionLanguage = new CaptionItem
        {
            Style = CaptionItemStyle.Link,
            Font = Palette.Mono,
            Color = Palette.NeonBlue,
            Accent = Palette.NeonPink,
            Click = () => ChangeLanguage(Strings.Language == "tr" ? "en" : "tr"),
        };
        _captionSponsor = new CaptionItem
        {
            Text = Strings.Get("caption.sponsor"),
            Style = CaptionItemStyle.Outline,
            Icon = CaptionItemIcon.Coffee,
            Font = Palette.Body,
            Accent = Palette.NeonPurple,
            Click = () => OpenUrl(Palette.SponsorUrl),
        };
        var captionSignature = new CaptionItem
        {
            Text = "Teknesyum",
            Style = CaptionItemStyle.Link,
            Font = Palette.Body,
            Color = Palette.NeonBlue,
            Accent = Palette.NeonPink,
            Click = () => OpenUrl(Palette.GitHubUrl),
        };
        SetCaptionItems(captionSignature, _captionSponsor, _captionLanguage, _captionVersion, _captionStatus);

        FormClosing += OnFormClosing;
        Activated += (_, _) => RefreshStatusOnly(force: false);

        // ---- Initial state ------------------------------------------------------------------
        ApplySecurityRadio(_config.SecurityMode);
        ApplyKeepWindowRadio(_config.KeepWindowOpen);
        _editorCommandBox.Text = string.IsNullOrWhiteSpace(_config.EditorCommand) ? DefaultConfig.DefaultEditorCommand : _config.EditorCommand;
        _logEnabledCheck.Checked = _config.LogEnabled;
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
            // Neither the row template nor the header below follows the DPI on its own, and both hold a
            // line of text; a literal here is what clips the grid at 125% and 150%.
            RowTemplate = { Height = Metrics.GridRowHeight },
            BackgroundColor = Palette.Surface,
            GridColor = ColorTranslator.FromHtml("#152229"), // opaque, dim blue-tinted line (GridColor rejects alpha)
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            EnableHeadersVisualStyles = false,
            Font = Palette.MonoBody,
        };

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Palette.Surface,
            ForeColor = Palette.NeonBlue,
            Font = Palette.H3,
            SelectionBackColor = Palette.Surface,
            SelectionForeColor = Palette.NeonBlue,
            Alignment = DataGridViewContentAlignment.MiddleCenter,
        };
        grid.ColumnHeadersHeight = Metrics.GridHeaderHeight;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.RowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Palette.Surface,
            ForeColor = Palette.TextBody,
            SelectionBackColor = ColorTranslator.FromHtml("#123238"),
            SelectionForeColor = Palette.TextBody,
            Alignment = DataGridViewContentAlignment.MiddleCenter,
        };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Palette.FieldBg,
            SelectionBackColor = ColorTranslator.FromHtml("#123238"),
        };

        grid.Columns.Add(new NeonCheckColumn { Name = "Enabled", HeaderText = "ETKİN", FillWeight = 8, MinimumWidth = Metrics.Px(60), Resizable = DataGridViewTriState.False });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Extension", HeaderText = "UZANTI", FillWeight = 10, MinimumWidth = Metrics.Px(72), ReadOnly = true });
        grid.Columns.Add(new NeonChipColumn { Name = "Kind", HeaderText = "TÜR", FillWeight = 13, MinimumWidth = Metrics.Px(90), OffTextKey = "kind.run", OnTextKey = "kind.open" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Interpreter", HeaderText = "İŞLEYİCİ", FillWeight = 20, MinimumWidth = Metrics.Px(130) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Found", HeaderText = "BULUNDU", FillWeight = 18, MinimumWidth = Metrics.Px(130), ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Args", HeaderText = "ARGÜMANLAR", FillWeight = 12, MinimumWidth = Metrics.Px(90) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "DURUM", FillWeight = 19, MinimumWidth = Metrics.Px(110), ReadOnly = true });

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
        var group = new NeonGroupPanel(Strings.Get("security")) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, Metrics.Px(12), 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent };
        // Row0 is an Absolute height, not AutoSize: three stacked NeonRadioButtons inside a nested
        // AutoSize FlowLayoutPanel is exactly the "AutoSize row + Dock=Fill child" trap R5 already hit once
        // (see docs/tasks/R5.md, UninstallConfirmDialog). AutoSize on this row mismeasured the true content
        // height and let the row3 (filesRow) content paint on top of row0/row1 — a fixed slot removes the guess.
        // Fixed, but not a constant: the slot is three radio rows, so it follows the body font.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, RadioStackHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Sized to two stacked folder buttons and the margin between them; a flat 66 cut "Çıkar" in half.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, FoldersAreaHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, TrustedFilesRowHeight));

        var radios = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        var alwaysAsk = new NeonRadioButton { Text = "Her seferinde sor", AutoSize = true };
        var trustOnFirstUse = new NeonRadioButton { Text = "İlk seferde sor, sonra güven", AutoSize = true };
        var neverAsk = new NeonRadioButton { Text = "Hiç sorma", AutoSize = true };
        radios.Controls.Add(alwaysAsk);
        radios.Controls.Add(trustOnFirstUse);
        radios.Controls.Add(neverAsk);
        layout.Controls.Add(radios, 0, 0);

        layout.Controls.Add(SectionLabel(Strings.Get("trustedFolders"), new Padding(0, Metrics.Px(6), 0, Metrics.Px(2))), 0, 1);

        var foldersArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        foldersArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foldersArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FolderButtonWidth + Metrics.Px(4)));
        var foldersList = new NeonListBox { Dock = DockStyle.Fill };
        // No margin of its own: the default 3px inset shrinks the cell below the fixed button width and
        // GDI clips the right half of the outline away, which is invisible in a build log.
        var folderButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        var folderButtonSize = new Size(FolderButtonWidth, FolderButtonHeight);
        var folderButtonPadding = new Padding(Metrics.Px(6), Metrics.Px(2), Metrics.Px(6), Metrics.Px(2));
        var addFolderButton = new NeonButton { Text = "Ekle", Primary = false, AutoSize = false, Size = folderButtonSize, Padding = folderButtonPadding, Margin = new Padding(Metrics.Px(4), 0, 0, Metrics.Px(4)) };
        var removeFolderButton = new NeonButton { Text = "Çıkar", Primary = false, AutoSize = false, Size = folderButtonSize, Padding = folderButtonPadding, Margin = new Padding(Metrics.Px(4), 0, 0, 0) };
        addFolderButton.Click += (_, _) => OnAddTrustedFolder(foldersList);
        removeFolderButton.Click += (_, _) => OnRemoveTrustedFolder(foldersList);
        folderButtons.Controls.Add(addFolderButton);
        folderButtons.Controls.Add(removeFolderButton);
        foldersArea.Controls.Add(foldersList, 0, 0);
        foldersArea.Controls.Add(folderButtons, 1, 0);
        layout.Controls.Add(foldersArea, 0, 2);

        var filesRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, Metrics.Px(6), 0, 0), BackColor = Color.Transparent };
        var filesLabel = new Label { AutoSize = true, Font = Palette.MonoBody, ForeColor = Palette.TextDim, Margin = new Padding(0, Metrics.Px(6), Metrics.Px(12), 0) };
        var clearFilesButton = new NeonButton { Text = "Tümünü temizle", Primary = false, AutoSize = true };
        clearFilesButton.Click += OnClearTrustedFiles;
        filesRow.Controls.Add(filesLabel);
        filesRow.Controls.Add(clearFilesButton);
        layout.Controls.Add(filesRow, 0, 3);

        group.Controls.Add(layout);
        return (alwaysAsk, trustOnFirstUse, neverAsk, foldersList, filesLabel, group);
    }

    private (RadioButton always, RadioButton onError, RadioButton never, TextBox editor, CheckBox logEnabled, Panel group) BuildBehaviorPanel()
    {
        var group = new NeonGroupPanel(Strings.Get("behavior")) { Dock = DockStyle.Fill, Margin = new Padding(Metrics.Px(12), 0, 0, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Absolute, not AutoSize — same fix as BuildSecurityPanel's radios row (see comment there), and the
        // same three-radio slot, so the two panels cannot drift apart when the font changes.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, RadioStackHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, EditorRowHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(SectionLabel(Strings.Get("windowOpen"), new Padding(0, 0, 0, Metrics.Px(2))), 0, 0);
        var keepRadios = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        var always = new NeonRadioButton { Text = "Her zaman", AutoSize = true };
        var onError = new NeonRadioButton { Text = "Sadece hata olursa", AutoSize = true };
        var never = new NeonRadioButton { Text = "Hiçbir zaman", AutoSize = true };
        keepRadios.Controls.Add(always);
        keepRadios.Controls.Add(onError);
        keepRadios.Controls.Add(never);
        layout.Controls.Add(keepRadios, 0, 1);

        var editorRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, Metrics.Px(10), 0, 0), BackColor = Color.Transparent };
        editorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var editorLabel = SectionLabel(Strings.Get("editorCommand"), new Padding(0, Metrics.Px(8), Metrics.Px(6), 0));
        editorLabel.Anchor = AnchorStyles.Left;
        var editorBox = new NeonTextBox { Dock = DockStyle.Fill };
        var testButton = new NeonButton { Text = "Test et", Primary = false, AutoSize = true, Margin = new Padding(Metrics.Px(6), 0, 0, 0) };
        testButton.Click += OnTestEditorClicked;
        editorRow.Controls.Add(editorLabel, 0, 0);
        editorRow.Controls.Add(editorBox, 1, 0);
        editorRow.Controls.Add(testButton, 2, 0);
        layout.Controls.Add(editorRow, 0, 2);

        var logRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, Metrics.Px(10), 0, 0), BackColor = Color.Transparent };
        var logCheck = new NeonCheckBox { Text = "Günlük tut", AutoSize = true, Margin = new Padding(0, Metrics.Px(4), Metrics.Px(12), 0) };
        var openLogButton = new NeonButton { Text = "Günlük klasörünü aç", Primary = false, AutoSize = true };
        openLogButton.Click += (_, _) => OpenFolder(RunlyPaths.AppDataDir);
        logRow.Controls.Add(logCheck);
        logRow.Controls.Add(openLogButton);
        layout.Controls.Add(logRow, 0, 3);

        group.Controls.Add(layout);
        return (always, onError, never, editorBox, logCheck, group);
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
        using var background = new SolidBrush(selected ? Palette.Surface : Palette.AppBg);
        e.Graphics.FillRectangle(background, e.Bounds);
        if (selected)
        {
            using var strip = new SolidBrush(Palette.NeonBlue);
            e.Graphics.FillRectangle(strip, e.Bounds.Left, e.Bounds.Top, Metrics.Px(3), e.Bounds.Height);
        }

        var iconSize = Metrics.CategoryIconSize;
        var iconLeft = e.Bounds.Left + Metrics.Px(8);
        if (_categoryIcons.TryGetValue(category, out var icon))
            e.Graphics.DrawIcon(icon, new Rectangle(iconLeft, e.Bounds.Top + ((e.Bounds.Height - iconSize) / 2), iconSize, iconSize));

        var entries = ExtensionCatalog.Entries.Where(entry => entry.Category == category).ToArray();
        var catalogExtensions = ExtensionCatalog.Entries.Select(entry => entry.Extension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var custom = _config.Extensions.Where(pair => !catalogExtensions.Contains(pair.Key) && pair.Value.Category == category).ToArray();
        var enabled = entries.Count(entry => EffectiveMapping(entry.Extension).Enabled) + custom.Count(pair => pair.Value.Enabled);
        var total = entries.Length + custom.Length;
        var label = Strings.Get("category." + category);
        var fore = selected ? Palette.NeonBlue : Palette.TextBody;
        var countWidth = Metrics.Px(46);
        var labelLeft = iconLeft + iconSize + Metrics.Px(8);
        TextRenderer.DrawText(e.Graphics, label, Font,
            new Rectangle(labelLeft, e.Bounds.Top, Math.Max(0, e.Bounds.Right - labelLeft - countWidth - Metrics.Px(6)), e.Bounds.Height), fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, $"{enabled}/{total}", Palette.LabelFont,
            new Rectangle(e.Bounds.Right - countWidth - Metrics.Px(8), e.Bounds.Top, countWidth, e.Bounds.Height),
            selected ? Palette.NeonBlue : Palette.TextHint,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }

    private void RefreshCategoryRail() => _categoryList.Invalidate();

    private ExtensionMapping EffectiveMapping(string extension)
    {
        if (_config.Extensions.TryGetValue(extension, out var configured)) return configured;
        var entry = ExtensionCatalog.Entries.FirstOrDefault(item =>
            string.Equals(item.Extension, extension, StringComparison.OrdinalIgnoreCase));
        return entry is null ? new ExtensionMapping { Category = "special" } : CatalogDefault(entry);
    }

    private IEnumerable<string> VisibleExtensions()
    {
        var category = _categoryList.SelectedItem as string;
        var query = _searchBox.Text.Trim();
        return CatalogGridProjection.GetExtensions(ExtensionCatalog.Entries, _config, category, query);
    }

    private RunlyConfig VisibleStatusConfig()
    {
        var visible = VisibleExtensions().ToHashSet(StringComparer.OrdinalIgnoreCase);

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

    private void RefreshExtensionGrid()
    {
        var refreshTimer = Stopwatch.StartNew();
        _suppressGridEvents = true;
        string? selectedExtension = null;
        try
        {
            if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ExtensionStatus selected)
            {
                selectedExtension = selected.Extension;
            }

            _grid.Rows.Clear();

            var statuses = _shellRegistrar.GetStatus(VisibleStatusConfig());
            _bindingProgress.SetProgress(statuses.Count(status => status.Bound == BindingState.Bound), statuses.Count);
            foreach (var status in statuses)
            {
                var mapping = EffectiveMapping(status.Extension);

                var row = new DataGridViewRow();
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
                    row.Cells[ColInterpreter].Style.NullValue = Strings.Get("handler.choosePrompt");
                    row.Cells[ColInterpreter].Style.ForeColor = Palette.TextDim;
                }
                row.Cells[ColArgs].Value = mapping.Args;
                ApplyStatusToRow(row, status);
                var catalogEntry = ExtensionCatalog.Entries.FirstOrDefault(entry => string.Equals(entry.Extension, status.Extension, StringComparison.OrdinalIgnoreCase));
                if (catalogEntry?.Blocked == true)
                {
                    row.Cells[ColEnabled].ReadOnly = true;
                    row.Cells[ColKind].ReadOnly = true;
                    row.Cells[ColInterpreter].ReadOnly = true;
                    row.Cells[ColArgs].ReadOnly = true;
                    row.Cells[ColStatus].Value = catalogEntry.RiskNote is null ? Strings.Get("catalog.blocked") : (Strings.Language == "en" ? catalogEntry.RiskNote.En : catalogEntry.RiskNote.Tr);
                }

                _grid.Rows.Add(row);

                if (selectedExtension is not null && string.Equals(selectedExtension, status.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    row.Selected = true;
                }
            }
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
            : status.InterpreterFound ? $"✓ {status.InterpreterPath}" : "✗";

        var (text, back, fore) = DescribeStatus(status.Bound);

        if (status.Bound == BindingState.NeedsUserChoice)
        {
            if (row.Cells[ColStatus] is not DataGridViewButtonCell)
            {
                row.Cells[ColStatus] = new DataGridViewButtonCell();
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

    private void SetAllExtensionsEnabled()
    {
        foreach (var extension in VisibleExtensions())
        {
            if (RunlyRegistryLayout.IsBlockedExtension(extension)) continue;
            _config.Extensions[extension] = EffectiveMapping(extension) with { Enabled = true };
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

        if (_grid.Rows[e.RowIndex].Cells[ColStatus] is not DataGridViewButtonCell)
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
            runMode ? mapping.Interpreter : mapping.OpenWith);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
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

        var selectedMapping = EffectiveMapping(status.Extension);
        if (selectedMapping.Kind == HandlerKind.Open && string.IsNullOrWhiteSpace(selectedMapping.OpenWith))
        {
            RenderMarkdownLite(_detailText, Strings.Get("handler.notSelectedDetail")
                .Replace("{extension}", status.Extension, StringComparison.Ordinal)
                .Replace("{button}", Strings.Get("catalog.chooseApp"), StringComparison.Ordinal));
            _detailAskButton.Visible = false;
            return;
        }

        if (status.Bound == BindingState.NeedsUserChoice)
        {
            RenderMarkdownLite(_detailText, BuildNeedsChoiceExplanation(status.Extension, status.UserChoiceOwnerName));
            _detailAskButton.Text = Strings.Get("bind.openFileTypePage")
                .Replace("{extension}", status.Extension, StringComparison.Ordinal);
            _detailAskButton.Visible = true;
        }
        else if (status.Bound == BindingState.Bound)
        {
            RenderMarkdownLite(_detailText, Strings.Language == "en"
                ? $"✅ `{status.Extension}` is now bound to Runly."
                : $"✅ `{status.Extension}` artık Runly'ye bağlı.");
            _detailAskButton.Visible = false;
        }
        else
        {
            RenderMarkdownLite(_detailText, Strings.Language == "en"
                ? $"`{status.Extension}` is not bound to Runly yet. Use the ‘Install / Update’ button to bind it."
                : $"`{status.Extension}` henüz Runly'ye bağlı değil. Bağlamak için \"Kur / Güncelle\" düğmesini kullanın.");
            _detailAskButton.Visible = false;
        }
    }

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

                AppendSegment(box, text.Substring(i + 1, end - (i + 1)), codeFont, Palette.NeonPink);
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
            NeonMessageBox.Show(this, $"'{dialog.Extension}' zaten tabloda var.", "Runly Ayarları",
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
            NeonMessageBox.Show(this, "Silinecek bir uzantı seçin.", "Runly Ayarları",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = NeonMessageBox.Show(this,
            $"'{status.Extension}' uzantısını tablodan silmek istediğinize emin misiniz?",
            "Runly Ayarları", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
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
                "Runly Ayarları — Güvenlik uyarısı",
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
            ? $"Trusted files: {_trustStore.Data.TrustedFiles.Count}"
            : $"Güvenilen dosyalar: {_trustStore.Data.TrustedFiles.Count} adet";

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

        var confirm = NeonMessageBox.Show(this, "Tüm güvenilen dosya kayıtları silinsin mi?", "Runly Ayarları",
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

    private void OnTestEditorClicked(object? sender, EventArgs e)
    {
        var command = string.IsNullOrWhiteSpace(_editorCommandBox.Text) ? "notepad" : _editorCommandBox.Text.Trim();
        try
        {
            Process.Start(new ProcessStartInfo { FileName = command, UseShellExecute = true })?.Dispose();
            NeonMessageBox.Show(this, $"'{command}' başlatıldı.", "Runly Ayarları",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            NeonMessageBox.Show(this, $"'{command}' başlatılamadı: {ex.Message}", "Runly Ayarları",
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

        // Registration succeeded. Windows protects the final UserChoice value, so continue directly
        // in the settings list where the choice is made. A single pending extension goes straight to
        // its own row; several of them get the unfiltered list, since ftfilter takes one extension.
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

            return (true, result.Extensions
                .Where(x => x.Bound == BindingState.NeedsUserChoice)
                .Select(x => x.Extension)
                .ToArray());
        }
        catch (Exception ex)
        {
            _logger.Error("Kurulum sırasında hata", ex);
            NeonMessageBox.Show(this, $"Kurulum sırasında beklenmeyen bir hata oluştu: {ex.Message}", "Runly Ayarları",
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

            if (orphans.Count > 0)
            {
                pendingRepair = orphans;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Kaldırma sırasında hata", ex);
            NeonMessageBox.Show(this, $"Kaldırma sırasında beklenmeyen bir hata oluştu: {ex.Message}", "Runly Ayarları",
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
            NeonMessageBox.Show(this, $"Ayarlar açılamadı: {ex.Message}", "Runly Ayarları",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        var backups = _registryBackup.ListBackups();
        if (backups.Count == 0)
        {
            NeonMessageBox.Show(this, "Hiç yedek bulunamadı.", "Runly Ayarları",
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
            "Runly Ayarları", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true, "Yedek geri yükleniyor…");
        try
        {
            var backupPath = picker.SelectedBackup.Path;
            await Task.Run(() => _registryBackup.RestoreBackup(backupPath));
            NeonMessageBox.Show(this, "Yedek geri yüklendi.", "Runly Ayarları",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.Error("Yedek geri yükleme hatası", ex);
            NeonMessageBox.Show(this, $"Yedek geri yüklenemedi: {ex.Message}", "Runly Ayarları",
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
            NeonMessageBox.Show(this, $"Ayarlar kaydedilemedi: {ex.Message}", "Runly Ayarları",
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
            "Runly Ayarları", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        return answer == DialogResult.Yes;
    }

    private void ChangeLanguage(string language)
    {
        Strings.Language = language == "en" ? "en" : "tr";
        ApplyLanguage();
        SaveAll();
    }

    private void ApplyLanguage()
    {
        _suppressGridEvents = true;
        _grid.Rows.Clear();
        Strings.Apply(this);
        Text = Strings.Get("app.title") + (_dirty ? " *" : string.Empty);
        _grid.Columns[ColEnabled].HeaderText = Strings.Get("enabled");
        _grid.Columns[ColExtension].HeaderText = Strings.Get("extension");
        _grid.Columns[ColKind].HeaderText = Strings.Get("kind.column");
        _grid.Columns[ColInterpreter].HeaderText = Strings.Get("interpreter");
        _grid.Columns[ColFound].HeaderText = Strings.Get("found");
        _grid.Columns[ColArgs].HeaderText = Strings.Get("arguments");
        _grid.Columns[ColStatus].HeaderText = Strings.Get("status");
        _searchBox.PlaceholderText = Strings.Get("catalog.searchPlaceholder");
        _captionLanguage.Text = Strings.Language == "tr" ? "TR | en" : "tr | EN";
        _captionSponsor.Text = Strings.Get("caption.sponsor");
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

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_dirty)
        {
            return;
        }

        var result = NeonMessageBox.Show(this,
            "Kaydedilmemiş değişiklikler var. Kapatmadan önce kaydetmek ister misiniz?",
            "Runly Ayarları", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

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

        _captionVersion.Text = Strings.Get("version") + ": v" + GetVersionText(exePath).TrimStart('v');

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
