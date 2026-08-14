# Runly — Claude Handoff

Last updated: 2026-08-14

## Product

Runly is a Windows x64 launcher that makes script files behave like normal double-clickable applications. It supports JavaScript, PowerShell, Python, shell, TypeScript, and configurable extensions; detects interpreters; performs trust/MOTW checks; and manages reversible Windows shell registrations.

Repository: `https://github.com/Teknesyum/Runly`

## Current release work

- Source version is `0.1.3` in `Directory.Build.props`.
- `v0.1.0`, `v0.1.1` and `v0.1.2` are public on GitHub.
- `v0.1.3` is published on GitHub with its package attached.
- Release build command: `./scripts/build.ps1` — the version is read from `Directory.Build.props`;
  pass `-Version` only to override it deliberately.
- Expected package: `Runly-v0.1.3-win-x64.zip`

## Default-app decision — important

Windows 11 protects per-user defaults in the `UserChoice` registry key. Runly must never delete, write, or forge that protected choice/hash.

`SHOpenWithDialog` is not a permanent-binding solution on current Windows 11: when launched through that API it offers only **Just once**. Do not restore it to the Settings UI.

The primary flow is:

1. Runly writes `RegisteredApplications`, capabilities, ProgIDs, and OpenWith registrations.
2. The UI opens Runly's application-specific Windows page:
   `ms-settings:defaultapps?registeredAppUser=Runly`
3. The user selects Runly for the desired extensions.
4. When Runly Settings receives `Activated` again, `RefreshStatusOnly(false)` re-reads status and updates the grid/footer automatically.

Explorer fallback, only as documentation:
`Right click → Open with → Choose another app → Runly → Always`.

Relevant code:

- `src/Runly.Settings/MainForm.cs`
  - `AskWindows()` opens the Runly-specific Default Apps page.
  - A successful install never shows a result dialog. If protected choices remain, Settings opens the Runly-specific Default Apps page directly.
  - `OfferOrphanRepair(...)` opens the general Default Apps page after uninstall.
  - `Activated` triggers the throttled automatic refresh.
- `src/Runly.Core/Shell/OpenWithDialog.cs` remains historical/core code but is not a Settings binding path.

## Icon fix

- Master artwork: `scripts/runly-master.png`
- ICO output: `assets/runly.ico`
- Generator: `scripts/make-icons.ps1`
- Both Launcher and Settings use `ApplicationIcon`.
- Settings also embeds `runly.ico` as `Runly.Settings.runly.ico` and `NeonForm` loads it explicitly. This is required because the borderless custom caption draws `Form.Icon`; relying only on the EXE resource previously showed a generic icon.

The new cyan/pink icon was visually verified in the live Release Settings title bar.

## Installation and license

- One-line install:
  `irm https://raw.githubusercontent.com/Teknesyum/Runly/main/scripts/install.ps1 | iex`
- Installs under `%LOCALAPPDATA%\Programs\Runly`.
- Creates `Runly.lnk` on the desktop.
- Repository now has an MIT `LICENSE`.
- `scripts/build.ps1` copies `LICENSE` into the release package.

## README

- README is English.
- Current screenshot: `docs/screenshots/runly-settings-v0.1.3.png` — captured from the 0.1.3 build (transparent icon, footer-only status strip, rounded window, neon panel titles).
- Support block intentionally matches the Adamantium Base repository design and links to `https://github.com/sponsors/Teknesyum`.

## Verification baseline

- Test suite: 190 tests.
- Launcher publish: NativeAOT, `win-x64`.
- Settings publish: self-contained WinForms, `win-x64`.
- Before publishing, run the full release build, inspect the package hash, launch `dist/RunlySettings.exe`, and visually verify the title icon and Default Apps button copy.

## Do not regress

- Do not silently claim Windows defaults.
- Do not use `SHOpenWithDialog` as the permanent-binding CTA.
- Do not mutate `UserChoice`.
- Keep successful installation silent; only an actual installation failure gets a result dialog.
- Keep the localized `Tümünü seç` / `Select all` extension action.
- Display only the human-readable semantic version; strip build metadata after `+`.
- Do not remove the explicit embedded Settings icon.
- Do not omit `LICENSE` from the release ZIP.
- Do not drop `WS_THICKFRAME`/`WS_MAXIMIZEBOX` from `NeonForm.CreateParams`, the `WM_NCCALCSIZE`
  handler, or the 7px gutter in `DisplayRectangle`. Together they are what make edge resizing,
  Aero Snap and double-click-to-maximize work on a borderless window; removing any one silently
  kills all three.
- Do not give a `DataGridView` cell a translucent `BackColor`. Cell fills ignore alpha and render
  white; pre-blend the tint against the surface colour instead.

