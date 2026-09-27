# Theme

`TeknesyumTokens` is generated at build time; nothing here is hand-written.

- Source: `teknesyum-ui/theme.tokens.json` (refreshed by teknesyum-ui `setup.js --apply`).
- Generator: `gen-tokens.ps1`, run by `TeknesyumTokens.targets` before CoreCompile.
- Output: `obj/<config>/TeknesyumTokens.g.cs` — hex strings, COLORREF (`*Ref`) for the AOT
  launcher, tone fills composited over surface, alpha bytes, sizes, durations, font chains.
- Change a colour or size in the token file, never in C#.
