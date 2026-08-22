# Changelog

All notable changes to Runly are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] — 0.2.0

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

### Fixed

- Installing from a directory without `Runly.exe` beside the settings window wrote a launcher path
  that did not exist and silently broke every association it touched. That install is now refused.
- Sixteen blocked extensions shipped with their Turkish risk note encoded twice, which reached the
  settings window verbatim.
- The dark title bar stayed light on Windows 10 builds before 19041, where the immersive dark mode
  attribute is 19 rather than 20.
- `SHOpenWithDialog` was sent registration flags that Windows has ignored since Windows 10.
- A stray tooltip showed `False` over the enabled checkbox.

## [0.1.3] — 2026-08-14

### Fixed

- Extension installation flow corrections.

## [0.1.2] — 2026-08-14

### Fixed

- Installation and packaging corrections.

## [0.1.1] — 2026-08-13

### Fixed

- Junction handling in trusted folder matching, and script corrections found during the first
  release round.

## [0.1.0] — 2026-08-13

First public release.

### Added

- Interpreter mappings for script extensions, written to the current user's registry hive.
- A security gate with three modes — ask every time, ask once then trust, never ask — plus Mark of
  the Web detection, script inspection and a trusted-folder list.
- A NativeAOT launcher that resolves the interpreter and starts the process.
- A settings window in the Teknesyum neon theme, with registry backup, restore and uninstall.

[Unreleased]: https://github.com/Teknesyum/Runly/compare/v0.1.3...HEAD
[0.1.3]: https://github.com/Teknesyum/Runly/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/Teknesyum/Runly/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/Teknesyum/Runly/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/Teknesyum/Runly/releases/tag/v0.1.0
