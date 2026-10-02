# Changelog

All notable changes to Runly are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.2] — 2026-10-02

### Added

- **Bulk default assignment.** After install, and from "Make default", Runly offers to copy one
  PowerShell command that binds every extension awaiting Windows approval at once. The command
  downloads PS-SFTA from a pinned commit, stops if its SHA256 differs, assigns each extension to its
  own `Runly.Script.<ext>` ProgID and checks the result. Runly never runs it; the user pastes it into
  their own shell. Blocked system types are never included.

### Changed

- SPEC no longer forbids writing a hashed `UserChoice` (decision K34).
- **Main window layout reworked** after a design review (`docs/danisma/001`). The search box sits
  on the table's left edge under a "Categories" heading, with a placeholder that stays visible
  while focused and a clear glyph inside the box; the separate label, "Clear" button and example
  line are gone. Bulk "open category with" moved under the table, beside the other actions, as
  label + list + "Apply". The duplicate "Choose application…" button under the table is gone (the
  detail panel keeps it). The bottom row is three groups: setup (Install / Update, Restore,
  Uninstall set apart without a frame), tools (Context menu…, Refresh), and the form's Save
  (primary) / Close. Free-text grid columns are left-aligned; "Found" shows the file name and
  the detail panel the full path; "✗" reads "Not found".
- Title bar (teknesyum-ui 0.34): the version sits beside the name as a grey button that checks for
  updates; the right-hand items run tools → update badge → TR/EN → Support → Teknesyum.
- **One Runly in "Open with".** Run-type extensions such as `.js` showed both "Runly" and
  "RunlyConsole"; the console launcher is now named "Runly" (`FriendlyAppName`, `AssemblyTitle`)
  and `Runly.exe` lists only open-type extensions under `SupportedTypes`.
- The edit verb reads "Edit with Runly" / "Runly ile düzenle" (was "Runly: Edit").
- Text size setting: the "Aa" title-bar link cycles 100% / 125% / 150% (`uiScale`); new installs
  start on the middle step.
  Text and spacing grow together; the token type scale stays shared and untouched.
- Settings opens maximized (teknesyum-ui 0.28 main-window rule).
- The "Support" chip in the title bar has no outline; like every other caption link it shows an
  underline and the accent colour on hover (teknesyum-ui 0.27).
- The UI audit now measures title-bar links at rest and on hover, and covers the bulk-assignment
  dialogs, and fails when a button is clipped by its container.

### Fixed

- **Grid rows ignored the row height.** Rows were added with `new DataGridViewRow()`, which does
  not take `RowTemplate.Height`, so every row stayed 28 px at any DPI; they now use
  `Metrics.GridRowHeight`.
- **Clipped buttons.** The bottom button strip and the application picker's button row lost
  their lower edge to the stock WinForms layout margin.
- **Cut category names.** The category rail was a fixed 210 px, so "Kod/Geliştirme" and
  others ended in an ellipsis; it now widens to its longest label.

## [0.3.1] — 2026-09-27

### Added

- **Video hand-off to VidShrink.** When VidShrink is installed, a plain open of a video file (an
  extension VidShrink lists under its registered `Capabilities\FileAssociations`, or any mapping in
  the `video` category) is passed straight to VidShrink with no dialog, and Runly exits. The
  executable is read from the `Teknesyum.VidShrink.Video` open command, never a fixed path. Without
  VidShrink, or if it fails to start, the old behaviour runs unchanged.

### Fixed

- **Release publish no longer fails on a pre-existing release.** The v0.3.0 Release run failed
  because a release for tag `v0.3.0` already existed when `gh release create` ran (`a release
  with the same tag name already exists`). The publish step now checks for an existing release
  first and uploads the built assets in place with `gh release upload --clobber` instead of
  erroring out.

## [0.3.0] — 2026-09-27

### Changed

- **Teknesyum "benim" layout.** Every colour, radius, border width, type step, spacing step and
  icon size is generated at build time from `teknesyum-ui/theme.tokens.json`
  (`src/Runly.Core/Theme/gen-tokens.ps1`); the hand-written `TeknesyumTokens.cs` is gone. The
  palette is now renk-1 #4DA6FF, renk-2 #DE7EF1, renk-3 #B68FFF with a 3 px corner and no glow.
  Settings, the launcher, `install.ps1` and `make-icons.ps1` all read the same file.
- **Shared chrome copy.** The brand, support, site, update and sync labels come from
  `teknesyum-ui/winforms/labels.*.json`. The product name in window titles has one source,
  `RunlyRegistryLayout.ApplicationName`, and the Settings title is locale `app.title`.
- **Install window size.** The installer window is sized from `metric.installer-w/h` (720×540)
  and wraps long log lines, so a full error message stays readable.
- **Icon.** `runly.ico` and the category icons are recoloured from the tokens.

### Added

- **Install window.** `install.ps1` now opens the teknesyum-ui install panel: five steps with
  ✓ and !, a gradient progress bar that never stops or goes back, a fading log, the install
  folder with Değiştir, and Kur / Yeniden dene / Programı aç. `-Silent` (`RUNLY_OTOMATIK=1`)
  keeps the console path, `-Rehearsal` (`RUNLY_PROVA=1`) installs into a temporary folder. The
  package is verified against its `.sha256` before it is unpacked, and when the GitHub API is
  rate-limited the installer finds the release through its `releases/latest` redirect.
- **Update badge and panel.** Settings checks for a newer release on start. A yellow
  "Güncelleme" badge downloads it at low priority, turns green when it is ready, and a second
  click swaps the files (`.old` rename) and restarts Settings. The panel shows the same stages
  with İndir, İndir ve yükle, İptal and Yükle.
- **Two-colour title.** "Runly" is drawn in blue and the window name in pink on every Settings
  window.

- **Context-menu cleanup.** A "Sağ menü…" button lists the entries other programs add to the
  right-click menu of Runly's script types and hides the checked ones. Editor duplicates that
  stay off other files (VS Code's static verb, Python's "Edit in IDLE", PowerShell ISE's "Edit")
  are hidden by default;
  entries that would vanish from every file (Notepad++, Windows Notepad) are opt-in. Static verbs
  are narrowed with an HKCU `AppliesTo` overlay, packaged handlers go on the per-user blocked
  list, and a ledger under `Software\Runly\MenuCleanup` lets Uninstall put everything back.

- **Context-menu preview.** The "Sağ menü…" button now opens a preview: pick a script type and see
  the menu Runly will produce for it, toggle every entry it can reach, and choose per type whether
  a double-click runs elevated. Entries Runly cannot switch off keep their place with an "Open its
  settings" button to the owning program, so the preview never hides what the menu holds.
- **Entries removable from every file type.** A static verb can now be dropped from all files, not
  only Runly's types: the HKCU copy gets `ProgrammaticAccessOnly` and the change is written to the
  `MenuCleanup\Verbs` ledger, so Uninstall puts it back.
- **Classic shell extensions are scanned too.** `*\shellex\ContextMenuHandlers` and
  `AllFilesystemObjects\shellex\ContextMenuHandlers` are read alongside the packaged handlers.
  Windows' own entries (Open With, Send To, Sharing, pinning, Copy as path, Encryption) are never
  offered. When Defender's real-time protection is off, its scan entry is marked as recommended to
  hide, with a line saying why.
- **Locked script files.** When a script cannot be read because another process holds it, Runly asks
  the Restart Manager who that is and offers to end those processes and retry, instead of sending
  the user to File Locksmith.

### Removed

- **"Runly ile yönetici olarak çalıştır"** is no longer a menu verb. Elevation is a setting now: a
  global switch in the behaviour panel, overridable per file type in the context-menu preview. The
  UAC prompt still appears on every elevated run.
- **"Runly ile argümanlarla çalıştır…"** is no longer in the context menu. `--verb prompt-args`
  still works from the command line. Install now rewrites each ProgID's `shell` tree from
  scratch, so verbs an older version wrote do not linger.
- **Bindings of disabled extensions.** Switching a mapping off in the settings now also unbinds it:
  install deletes its ProgID, its `OpenWithProgids` entry and any default that still pointed at
  Runly. A disabled extension used to keep opening with a launcher whose interpreter was off, and
  the context-menu cleanup skipped that type because it only covers what Runly actually runs.

### Changed

- **Context menu.** The edit verb now names the editor it opens — "Runly: Düzenle (Notepad++)" — so
  it is neither confused with Windows' own "Edit" nor a mystery about where the file will land.
- **Editor picker.** The editor command gets a "Choose…" button that opens the application
  picker instead of requiring a typed command.

### Fixed

- **Editor chosen as interpreter.** Picking a known editor (Notepad++, VS Code, …) for a Run
  mapping now offers to set it as the editor instead, so double-click keeps running the script.
- **Raw label keys.** `labels.tr.json` was built into a `tr` satellite assembly, so Settings
  showed keys such as `sig.brand`; the label files are now embedded in the main assembly.
- **Rehearsal target.** `-Rehearsal` always installs into the temporary `Runly-prova` folder.
  An `-InstallPath` given with it is ignored and says so, and Değiştir is hidden, so a rehearsal
  run from an admin shell can no longer write into a real folder such as `System32`.

## [0.2.1] — 2026-09-08

### Fixed

- **Window frame.** The settings window now strips WS_CAPTION, WS_DLGFRAME and WS_BORDER
  back off itself whenever they appear, so shell extensions that redraw window frames can no
  longer paint a classic light title bar over the neon caption band. The dark title bar
  attribute is applied as a second line of defence.

### Removed

- Dead `ApplicationHighDpiMode` property that was never applied, since
  `ApplicationConfiguration.Initialize()` is not called.

## [0.2.0] — 2026-08-24

Handlers are no longer limited to interpreters: an extension can be opened with any installed
application, chosen from a picker instead of typed as an absolute path.

### Added

- **Open handlers.** A mapping now has a kind. `Run` passes the file to an interpreter as before;
  `Open` hands it to an application such as an editor or viewer. Existing configurations migrate
  automatically to the v2 model.
- **Extension catalog.** 408 extensions ship embedded, grouped into 14 categories, each with a
  localized display name, suggested applications and, where relevant, a risk note. System types
  that Windows protects are marked as unmanageable rather than silently failing.
- **Application picker.** Double-clicking a row opens a searchable list of installed applications
  with their real icons, the extensions's suggested handlers pinned to the top, and a Browse
  fallback for anything the scan missed. Runly excludes itself from that list.
- **Categorized workspace.** The settings window gained a category rail, a search box that spans
  every category, and a binding progress ring.
- **Profiles.** Configurations can be exported and imported as JSON.
- **Risk notes.** `.hta`, `.vbs`, `.wsf`, `.js`, `.ps1` and `.jar` carry a note explaining what
  running them actually allows. They stay usable — the note informs, it does not block.
- **Continuous integration.** Build, test and format checks run on every push and pull request.
  Tagged releases verify that the tag matches the project version before building, and publish a
  `.sha256` file next to the archive.
- **Package verification.** The installer downloads the checksum, verifies the archive before
  extracting it, and stops on a mismatch.

### Changed

- **Store alias detection.** Interpreter discovery no longer decides by file size. App execution
  aliases are read as reparse points: an `APPEXECLINK` target pointing at a redirector is a dead
  Store stub and is skipped, anything else is a working alias and is accepted.
- **Per-extension binding.** The "Set default" button opens the Windows file-type page instead of
  Runly's own default-apps page, which only ever listed extensions Runly already owned.
- **Grid layout.** Columns fill the available width, so the status column and its button stay
  reachable instead of hiding behind a horizontal scrollbar.
- **Search.** Typing is debounced, and results are limited to matches rather than being unioned
  with every enabled extension.
- **License.** The project is now released under AGPL-3.0-or-later. Copyright (C) 2026 Teknesyum.

### Fixed

- Installing from a directory without `Runly.exe` beside the settings window wrote a launcher path
  that did not exist and silently broke every association it touched. That install is now refused.
- Sixteen blocked extensions shipped with their Turkish risk note encoded twice, which reached the
  settings window verbatim.
- The dark title bar stayed light on Windows 10 builds before 19041, where the immersive dark mode
  attribute is 19 rather than 20.
- `SHOpenWithDialog` was sent registration flags that Windows has ignored since Windows 10.
- A stray tooltip showed `False` over the enabled checkbox.

## 0.1.3 — 2026-08-14

### Fixed

- Extension installation flow corrections.

## 0.1.2 — 2026-08-14

### Fixed

- Installation and packaging corrections.

## 0.1.1 — 2026-08-13

### Fixed

- Junction handling in trusted folder matching, and script corrections found during the first
  release round.

## 0.1.0 — 2026-08-13

First public release.

### Added

- Interpreter mappings for script extensions, written to the current user's registry hive.
- A security gate with three modes — ask every time, ask once then trust, never ask — plus Mark of
  the Web detection, script inspection and a trusted-folder list.
- A NativeAOT launcher that resolves the interpreter and starts the process.
- A settings window in the Teknesyum neon theme, with registry backup, restore and uninstall.

[Unreleased]: https://github.com/Teknesyum/Runly/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/Teknesyum/Runly/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/Teknesyum/Runly/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/Teknesyum/Runly/releases/tag/v0.2.0
