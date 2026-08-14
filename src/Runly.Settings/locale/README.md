# Runly UI translations

Every string shown in Runly Settings lives in this folder, one JSON file per language.

## Adding a language

1. Copy `en.json` to `<code>.json`, where `<code>` is the two-letter language code (`de.json`, `fr.json`).
2. Translate **values only**. Never translate, rename, reorder, or remove a key.
3. Keep every key. A missing key falls back to the source text, which leaves the UI half-translated.
4. Register the file in `Runly.Settings.csproj` next to the existing `locale\*.json` entries.

## Rules

- **Meaning must not drift.** Security and confirmation texts especially: a translation that softens
  or strengthens a warning is a bug, not a style choice.
- **Keep `\n` line breaks** where the source has them.
- **Avoid giving two keys the same value.** Where a value repeats, the language switch resolves it
  to whichever key was declared first. That is harmless only when the duplicate is identical in
  every language, which is true of the few that exist today.
- **Do not shorten a string to make it fit.** If a translation overflows its control, report it —
  the layout gets fixed, not the wording.

## Testing

Build and run `RunlySettings.exe`, then use the `TR | EN` switch in the footer. The switch applies
instantly; no restart is needed.
