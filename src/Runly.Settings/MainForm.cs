using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using Runly.Core.Abstractions;
using Runly.Core.Defaults;
using Runly.Core.Models;
using Runly.Core.Paths;
using Runly.Core.Shell;
using Runly.Settings.Dialogs;

namespace Runly.Settings;

/// <summary>The single settings window: status strip, extension table, security/behavior panels, bottom bar (SPEC 10).</summary>
internal sealed class MainForm : NeonForm
{
    private const int ColEnabled = 0;
    private const int ColExtension = 1;
    private const int ColInterpreter = 2;
    private const int ColFound = 3;
    private const int ColArgs = 4;
    private const int ColStatus = 5;

    private static readonly Color BoundBack = Color.FromArgb(40, Palette.Success);
    private static readonly Color BoundFore = Palette.Success;
    private static readonly Color NeedsChoiceBack = Color.FromArgb(40, Palette.NeonPink);
    private static readonly Color NeedsChoiceFore = Palette.NeonPink;
    private static readonly Color NotBoundBack = Palette.FieldBg;
    private static readonly Color NotBoundFore = Palette.TextHint;

    /// <summary>
    /// Section label in the Teknesyum "Etiket" role: small, bold, uppercase, letter-spaced, dim.
    /// WinForms has no letter-spacing property, so the spacing is baked into the text.
    /// </summary>
    private static Label SectionLabel(string text, Padding margin) => new()
    {
        Text = string.Join(" ", text.ToCharArray()),
        AutoSize = true,
        Font = Palette.LabelFont,
        ForeColor = Palette.TextLabel,
        Margin = margin,
    };

    private readonly IConfigStore _configStore;
    private readonly ITrustStore _trustStore;
    private readonly IShellRegistrar _shellRegistrar;
    private readonly RegistryBackup _registryBackup;
    private readonly ILogger _logger;
    private readonly RunlyConfig _config;

    private bool _dirty;
    private bool _initializing = true;
    private bool _suppressGridEvents;
    private bool _autoRefreshInFlight;
    private DateTime _lastAutoRefresh = DateTime.MinValue;

    private readonly DataGridView _grid;
    private readonly Label _statusLabel;
    private readonly Label _exePathLabel;
    private readonly Label _versionLabel;
    private readonly LinkLabel _configPathLink;
    private readonly Button _refreshButton;
    private readonly RichTextBox _detailText;
    private readonly Button _detailAskButton;
    private readonly Label _detailPlaceholder;

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
    private readonly Label _footerDot;
    private readonly Label _footerStatusLabel;
    private readonly LinkLabel _languageToggle;

    /// <summary>Builds the whole window from code; see SPEC 10 for the layout this follows.</summary>
    public MainForm(
        IConfigStore configStore,
        RunlyConfig config,
        ITrustStore trustStore,
        IShellRegistrar shellRegistrar,
        RegistryBackup registryBackup,
        ILogger logger)
    {
        _configStore = configStore;
        _trustStore = trustStore;
        _shellRegistrar = shellRegistrar;
        _registryBackup = registryBackup;
        _logger = logger;
        _config = config;
        Strings.Language = string.Equals(config.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "tr";
        _lastGoodSecurityMode = config.SecurityMode;

        Text = Strings.Get("app.title");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        var workArea = Screen.PrimaryScreen?.WorkingArea.Size ?? new Size(1280, 900);
        Size = new Size(Math.Min(1280, (int)(workArea.Width * 0.85)), Math.Min(900, (int)(workArea.Height * 0.85)));
        // Height dropped 74px with the status strip removed and the footer strip tightened.
        MinimumSize = new Size(Math.Min(1180, workArea.Width), Math.Min(750, workArea.Height));
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Palette.AppBg;
        ForeColor = Palette.TextBody;
        Font = Palette.Body;
        // Native scrollbars (grid, list boxes, result text) render white by default and break the
        // theme. Applied on Shown so every child handle already exists.
        Shown += (_, _) => NeonTheme.ApplyDarkScrollBars(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Palette.AppBg };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // 270 could not hold the security panel once the third radio and the second folder button
        // were given real room (96 + label + 78 + 36 + panel chrome).
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 306));
        // Buttons (32) + signature (18) + padding; keeps the two rows visually joined.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

        // The status strip was removed: it repeated the footer indicator and its 13.5pt line clipped
        // descenders. These four stay unparented — code paths still set their Text without a UI slot.
        _refreshButton = new NeonButton { Text = "Yenile", Primary = false, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        _refreshButton.Click += (_, _) => RefreshStatusOnly(force: true);
        _statusLabel = new Label { Visible = false };
        _exePathLabel = new Label { Visible = false };
        _versionLabel = new Label { AutoSize = true, Font = Palette.MonoBody, ForeColor = Palette.NeonBlue };
        _configPathLink = new LinkLabel { Visible = false };
        _configPathLink.LinkClicked += (_, _) => OpenContainingFolder(_configStore.ConfigPath);

        // ---- 2. Extension table + detail panel -------------------------------------------
        var gridArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Palette.AppBg, Padding = new Padding(0, 8, 0, 0) };
        gridArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gridArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        gridArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        gridArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        _grid = BuildExtensionGrid();
        gridArea.Controls.Add(_grid, 0, 0);

        var detailPanel = new NeonGroupPanel(Strings.Get("details")) { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0) };
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
        _detailAskButton.Click += OnDetailAskButtonClicked;
        detailPanel.Controls.Add(_detailText);
        detailPanel.Controls.Add(_detailAskButton);
        detailPanel.Controls.Add(_detailPlaceholder);
        gridArea.Controls.Add(detailPanel, 1, 0);

        var extButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
        var selectAllButton = new NeonButton { Text = "Tümünü seç", Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = new Padding(0, 4, 8, 4) };
        var addExtButton = new NeonButton { Text = "Uzantı ekle", Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = new Padding(0, 4, 8, 4) };
        var removeExtButton = new NeonButton { Text = "Seçili uzantıyı sil", Primary = false, BackColor = Palette.AppBg, AutoSize = true, Margin = new Padding(0, 4, 8, 4) };
        selectAllButton.Click += (_, _) => SetAllExtensionsEnabled();
        addExtButton.Click += OnAddExtensionClicked;
        removeExtButton.Click += OnRemoveExtensionClicked;
        extButtons.Controls.Add(selectAllButton);
        extButtons.Controls.Add(addExtButton);
        extButtons.Controls.Add(removeExtButton);
        gridArea.Controls.Add(extButtons, 0, 1);
        gridArea.SetColumnSpan(extButtons, 1);

        root.Controls.Add(gridArea, 0, 0);

        // ---- 3 & 4. Security + behavior panels --------------------------------------------
        var panelsRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Palette.AppBg, Padding = new Padding(0, 8, 0, 0) };
        panelsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panelsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        (_radioAlwaysAsk, _radioTrustOnFirstUse, _radioNeverAsk, _trustedFoldersList, _trustedFilesLabel, var securityGroup) = BuildSecurityPanel();
        (_radioKeepAlways, _radioKeepOnError, _radioKeepNever, _editorCommandBox, _logEnabledCheck, var behaviorGroup) = BuildBehaviorPanel();

        panelsRow.Controls.Add(securityGroup, 0, 0);
        panelsRow.Controls.Add(behaviorGroup, 1, 0);
        root.Controls.Add(panelsRow, 0, 1);

        // ---- 5. Bottom bar (+ İmza bloğu, R5 zorunlu: ayarlar penceresinin en altında sağda) ----
        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12, 6, 12, 3), BackColor = Palette.Surface };
        bottomBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // 18 is the measured height of LabelFont plus its descender — smaller clips, larger opens a gap.
        bottomBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));

        // Two columns instead of a Dock=Left label: a fixed 320px label starved the RightToLeft button
        // flow at MinimumSize and clipped the leftmost button ("Kur / Güncelle" rendered as "Güncelle").
        // The buttons now take the width they need and the progress label absorbs whatever is left.
        var buttonsRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _progressLabel = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = Palette.MonoBody, ForeColor = Palette.NeonBlue };
        var buttonsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent };

        var closeButton = new NeonButton { Text = "Kapat", Primary = false, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        _saveButton = new NeonButton { Text = "Kaydet", Primary = false, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        _restoreButton = new NeonButton { Text = "Yedekten geri yükle", Primary = false, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        _uninstallButton = new NeonButton { Text = "Kaldır", Primary = false, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        _installButton = new NeonButton { Text = "Kur / Güncelle", Primary = true, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };

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

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var footerLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        _footerDot = new Label { Text = "●", AutoSize = true, Font = Palette.LabelFont, ForeColor = Palette.TextHint, Margin = new Padding(0, 1, 4, 0) };
        _footerStatusLabel = new Label { AutoSize = true, Font = Palette.LabelFont, ForeColor = Palette.NeonBlue, Margin = new Padding(0, 1, 12, 0) };
        var footerVersion = _versionLabel;
        footerVersion.Margin = new Padding(0, 1, 12, 0);
        footerVersion.Font = Palette.LabelFont;
        footerVersion.Visible = true;
        _languageToggle = new LinkLabel { Text = "TR | EN", AutoSize = true, Font = Palette.LabelFont, LinkColor = Palette.NeonBlue, ActiveLinkColor = Palette.NeonPink, BackColor = Color.Transparent, Margin = new Padding(0, 1, 12, 0) };
        _languageToggle.LinkClicked += (_, _) => ChangeLanguage(Strings.Language == "tr" ? "en" : "tr");
        footerLeft.Controls.Add(_footerDot);
        footerLeft.Controls.Add(_footerStatusLabel);
        footerLeft.Controls.Add(footerVersion);
        footerLeft.Controls.Add(_languageToggle);
        footer.Controls.Add(footerLeft, 0, 0);

        // Support link and signature stay together on the right, sponsor first.
        var footerRight = new FlowLayoutPanel { Anchor = AnchorStyles.Top | AnchorStyles.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        footerRight.Controls.Add(new NeonLink("Buy me a coffee", Palette.SponsorUrl) { Margin = new Padding(0, 1, 12, 0) });
        footerRight.Controls.Add(new SignatureBlock { AutoSize = true, Margin = new Padding(0, 1, 0, 0) });
        footer.Controls.Add(footerRight, 1, 0);
        bottomBar.Controls.Add(footer, 0, 1);

        root.Controls.Add(bottomBar, 0, 2);

        Controls.Add(root);

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
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            RowTemplate = { Height = 26 },
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
        grid.ColumnHeadersHeight = 30;
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

        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "ETKİN", Width = 82, Resizable = DataGridViewTriState.False });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Extension", HeaderText = "UZANTI", Width = 102, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Interpreter", HeaderText = "YORUMLAYICI", Width = 130 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Found", HeaderText = "BULUNDU", Width = 200, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Args", HeaderText = "ARGÜMANLAR", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "DURUM", Width = 150, ReadOnly = true });

        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        grid.CellValueChanged += OnGridCellValueChanged;
        grid.CellContentClick += OnGridCellContentClick;
        grid.SelectionChanged += (_, _) => UpdateDetailPanel();

        return grid;
    }

    private (RadioButton alwaysAsk, RadioButton trustOnFirstUse, RadioButton neverAsk, ListBox folders, Label filesLabel, Panel group) BuildSecurityPanel()
    {
        var group = new NeonGroupPanel(Strings.Get("security")) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent };
        // Row0 is an Absolute height, not AutoSize: three stacked NeonRadioButtons inside a nested
        // AutoSize FlowLayoutPanel is exactly the "AutoSize row + Dock=Fill child" trap R5 already hit once
        // (see docs/tasks/R5.md, UninstallConfirmDialog). AutoSize on this row mismeasured the true content
        // height and let the row3 (filesRow) content paint on top of row0/row1 — a fixed slot removes the guess.
        // 72px fitted only two of the three radios; "Hiç sorma" was clipped and the folders label
        // painted over it. 96px = 3 x 32 (NeonRadioButton at Palette.Body + margins).
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // 66px clipped the second stacked folder button ("Çıkar"): 30 + 4 margin + 30 needs 78.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        var radios = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        var alwaysAsk = new NeonRadioButton { Text = "Her seferinde sor", AutoSize = true };
        var trustOnFirstUse = new NeonRadioButton { Text = "İlk seferde sor, sonra güven", AutoSize = true };
        var neverAsk = new NeonRadioButton { Text = "Hiç sorma", AutoSize = true };
        radios.Controls.Add(alwaysAsk);
        radios.Controls.Add(trustOnFirstUse);
        radios.Controls.Add(neverAsk);
        layout.Controls.Add(radios, 0, 0);

        layout.Controls.Add(SectionLabel("GÜVENİLEN KLASÖRLER", new Padding(0, 6, 0, 2)), 0, 1);

        var foldersArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        foldersArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foldersArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        var foldersList = new ListBox { Dock = DockStyle.Fill, BackColor = Palette.FieldBg, ForeColor = Palette.TextBody, Font = Palette.MonoBody, BorderStyle = BorderStyle.FixedSingle };
        var folderButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        var addFolderButton = new NeonButton { Text = "Ekle", Primary = false, AutoSize = false, Size = new Size(78, 30), Padding = new Padding(6, 2, 6, 2), Margin = new Padding(4, 0, 0, 4) };
        var removeFolderButton = new NeonButton { Text = "Çıkar", Primary = false, AutoSize = false, Size = new Size(78, 30), Padding = new Padding(6, 2, 6, 2), Margin = new Padding(4, 0, 0, 0) };
        addFolderButton.Click += (_, _) => OnAddTrustedFolder(foldersList);
        removeFolderButton.Click += (_, _) => OnRemoveTrustedFolder(foldersList);
        folderButtons.Controls.Add(addFolderButton);
        folderButtons.Controls.Add(removeFolderButton);
        foldersArea.Controls.Add(foldersList, 0, 0);
        foldersArea.Controls.Add(folderButtons, 1, 0);
        layout.Controls.Add(foldersArea, 0, 2);

        var filesRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 6, 0, 0), BackColor = Color.Transparent };
        var filesLabel = new Label { AutoSize = true, Font = Palette.MonoBody, ForeColor = Palette.TextDim, Margin = new Padding(0, 6, 12, 0) };
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
        var group = new NeonGroupPanel(Strings.Get("behavior")) { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Absolute, not AutoSize — same fix as BuildSecurityPanel's radios row (see comment there).
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(SectionLabel("PENCEREYİ AÇIK TUT", new Padding(0, 0, 0, 2)), 0, 0);
        var keepRadios = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        var always = new NeonRadioButton { Text = "Her zaman", AutoSize = true };
        var onError = new NeonRadioButton { Text = "Sadece hata olursa", AutoSize = true };
        var never = new NeonRadioButton { Text = "Hiçbir zaman", AutoSize = true };
        keepRadios.Controls.Add(always);
        keepRadios.Controls.Add(onError);
        keepRadios.Controls.Add(never);
        layout.Controls.Add(keepRadios, 0, 1);

        var editorRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 10, 0, 0), BackColor = Color.Transparent };
        editorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var editorLabel = SectionLabel("DÜZENLEYİCİ KOMUTU", new Padding(0, 8, 6, 0));
        editorLabel.Anchor = AnchorStyles.Left;
        var editorBox = new TextBox { Dock = DockStyle.Fill, BackColor = Palette.FieldBg, ForeColor = Palette.NeonBlue, Font = Palette.MonoBody, BorderStyle = BorderStyle.FixedSingle };
        var testButton = new NeonButton { Text = "Test et", Primary = false, AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        testButton.Click += OnTestEditorClicked;
        editorRow.Controls.Add(editorLabel, 0, 0);
        editorRow.Controls.Add(editorBox, 1, 0);
        editorRow.Controls.Add(testButton, 2, 0);
        layout.Controls.Add(editorRow, 0, 2);

        var logRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 10, 0, 0), BackColor = Color.Transparent };
        var logCheck = new NeonCheckBox { Text = "Günlük tut", AutoSize = true, Margin = new Padding(0, 4, 12, 0) };
        var openLogButton = new NeonButton { Text = "Günlük klasörünü aç", Primary = false, AutoSize = true };
        openLogButton.Click += (_, _) => OpenFolder(RunlyPaths.AppDataDir);
        logRow.Controls.Add(logCheck);
        logRow.Controls.Add(openLogButton);
        layout.Controls.Add(logRow, 0, 3);

        group.Controls.Add(layout);
        return (always, onError, never, editorBox, logCheck, group);
    }

    // ---- Extension grid -----------------------------------------------------------------

    private void RefreshExtensionGrid()
    {
        _suppressGridEvents = true;
        string? selectedExtension = null;
        try
        {
            if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ExtensionStatus selected)
            {
                selectedExtension = selected.Extension;
            }

            _grid.Rows.Clear();

            foreach (var status in _shellRegistrar.GetStatus(_config))
            {
                if (!_config.Extensions.TryGetValue(status.Extension, out var mapping))
                {
                    continue;
                }

                var row = new DataGridViewRow();
                row.CreateCells(_grid);
                row.Cells[ColEnabled].Value = mapping.Enabled;
                row.Cells[ColExtension].Value = status.Extension;
                row.Cells[ColInterpreter].Value = mapping.Interpreter;
                row.Cells[ColArgs].Value = mapping.Args;
                ApplyStatusToRow(row, status);

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

        UpdateDetailPanel();
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

        Task.Run(() => _shellRegistrar.GetStatus(_config)).ContinueWith(t =>
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

    private static void ApplyStatusToRow(DataGridViewRow row, ExtensionStatus status)
    {
        row.Cells[ColFound].Value = status.InterpreterFound ? $"✓ {status.InterpreterPath}" : "✗";

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

        if (e.ColumnIndex is not (ColEnabled or ColInterpreter or ColArgs))
        {
            return;
        }

        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not ExtensionStatus status || !_config.Extensions.TryGetValue(status.Extension, out var mapping))
        {
            return;
        }

        var enabled = row.Cells[ColEnabled].Value is bool b ? b : mapping.Enabled;
        var interpreter = row.Cells[ColInterpreter].Value as string ?? mapping.Interpreter;
        var args = row.Cells[ColArgs].Value as string ?? mapping.Args;

        _config.Extensions[status.Extension] = mapping with { Enabled = enabled, Interpreter = interpreter, Args = args };
        MarkDirty();
        UpdateSingleRowStatus(e.RowIndex, status.Extension);
    }

    private void SetAllExtensionsEnabled()
    {
        foreach (var extension in _config.Extensions.Keys.ToList())
        {
            _config.Extensions[extension] = _config.Extensions[extension] with { Enabled = true };
        }

        RefreshExtensionGrid();
        MarkDirty();
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

        AskWindows();
    }

    private void OnDetailAskButtonClicked(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Tag is not ExtensionStatus status)
        {
            return;
        }

        AskWindows();
    }

    private void AskWindows()
    {
        // SHOpenWithDialog is intentionally not used here: Windows 11 exposes only "Just once"
        // through that API. The registered-app deep link opens Runly's Default apps page, where
        // the user can make a persistent per-extension choice.
        OpenDefaultAppsSettings(forRunly: true);
    }

    private void UpdateDetailPanel()
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Tag is not ExtensionStatus status)
        {
            _detailPlaceholder.Visible = true;
            _detailText.Visible = false;
            _detailAskButton.Visible = false;
            return;
        }

        _detailPlaceholder.Visible = false;
        _detailText.Visible = true;

        if (status.Bound == BindingState.NeedsUserChoice)
        {
            RenderMarkdownLite(_detailText, BuildNeedsChoiceExplanation(status.Extension, status.UserChoiceOwnerName));
            _detailAskButton.Text = Strings.Language == "en" ? "Open Runly default-app settings" : "Runly varsayılan uygulama ayarlarını aç";
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
                   "The button below opens Runly’s Windows Default apps page, where you can make the same " +
                   "persistent choice for each extension.";
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
        "Aşağıdaki düğme Runly'nin Windows **Varsayılan uygulamalar** sayfasını açar; " +
        "buradan her uzantı için kalıcı seçimi yapabilirsiniz.\n\n" +
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

        _config.Extensions[dialog.Extension] = new ExtensionMapping
        {
            Interpreter = dialog.Interpreter,
            Args = dialog.Args,
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

    private static string ExePath => Path.Combine(AppContext.BaseDirectory, "Runly.exe");

    private async void OnInstallClicked(object? sender, EventArgs e)
    {
        SetBusy(true, "Kuruluyor…");
        try
        {
            var exePath = ExePath;
            var result = await Task.Run(() => _shellRegistrar.Install(_config, exePath));

            var pending = result.Extensions.Where(x => x.Bound == BindingState.NeedsUserChoice).ToList();

            if (!result.Success)
            {
                ResultDialog.Show(this, "Kurulum hatası", false, result.Actions, result.ErrorMessage);
            }
            else if (pending.Count > 0)
            {
                // Registration succeeded. Windows protects the final UserChoice value, so continue
                // directly in the Runly-specific settings page without another result/prompt dialog.
                OpenDefaultAppsSettings(forRunly: true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Kurulum sırasında hata", ex);
            NeonMessageBox.Show(this, $"Kurulum sırasında beklenmeyen bir hata oluştu: {ex.Message}", "Runly Ayarları",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    private void OpenDefaultAppsSettings(bool forRunly = false)
    {
        try
        {
            var settingsUri = forRunly
                ? "ms-settings:defaultapps?registeredAppUser=Runly"
                : "ms-settings:defaultapps";
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
        };

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

        _dirty = false;
        UpdateTitle();
        _progressLabel.Text = "Kaydedildi ✓";
    }

    private void ChangeLanguage(string language)
    {
        Strings.Language = language == "en" ? "en" : "tr";
        ApplyLanguage();
        SaveAll();
    }

    private void ApplyLanguage()
    {
        Strings.Apply(this);
        Text = Strings.Get("app.title") + (_dirty ? " *" : string.Empty);
        _grid.Columns[ColEnabled].HeaderText = Strings.Get("enabled");
        _grid.Columns[ColExtension].HeaderText = Strings.Get("extension");
        _grid.Columns[ColInterpreter].HeaderText = Strings.Get("interpreter");
        _grid.Columns[ColFound].HeaderText = Strings.Get("found");
        _grid.Columns[ColArgs].HeaderText = Strings.Get("arguments");
        _grid.Columns[ColStatus].HeaderText = Strings.Get("status");
        _languageToggle.Text = Strings.Language == "tr" ? "TR | en" : "tr | EN";
        RefreshExtensionGrid();
        RefreshTrustedFilesLabel();
        RefreshStatusStrip();
        UpdateDetailPanel();
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
        var exeExists = File.Exists(exePath);
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

        _footerDot.ForeColor = bound + pending == 0 ? Palette.TextHint : Palette.Success;
        _footerStatusLabel.Text = bound + pending == 0 ? Strings.Get("notInstalled") : Strings.Get("installed");

        var exeFullText = exeExists ? exePath : $"{exePath} (bulunamadı)";
        _exePathLabel.Text = ShortenPathMiddle(exeFullText, 42);
        _statusTip.SetToolTip(_exePathLabel, exeFullText);

        _versionLabel.Text = Strings.Get("version") + ": v" + GetVersionText(exePath).TrimStart('v');

        var configFullText = "config: " + _configStore.ConfigPath;
        _configPathLink.Text = ShortenPathMiddle(configFullText, 42);
        _statusTip.SetToolTip(_configPathLink, configFullText);
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
