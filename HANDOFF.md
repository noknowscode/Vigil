# HANDOFF — Vigil: Clover fixes, runtime pass
Updated: 2026-10-28 (America/Montreal)

## Where we are
Working the impeccable/uno-audit recommendations in order on `archive/local-2026-10-28` (clone at
`C:\Users\Platform006\Vigil`; the old `UnoProjects\Vigil` clone was removed). Done and pushed: dark-mode tokens
for photo panels, live band and reminder card (afbfa09), 48 px record nav targets + hero MinHeight (6ce9259),
12-13 px type floor (ab087ba), 32 strings localized (a2a13ae), composed accessible names (5384d27), board
breakpoints to `{utu:Responsive}` with `BoardLayout` 600/660/760/1120 (a1e73a7). None of it has been run yet.

## Last verified state
- Build: desktop 0 warnings, 0 errors. Tests 68/68. WASM/Android/iOS not rebuilt since the fixes.
- Runtime: not verified. The previous session's App MCP was rooted in the deleted folder.
- Git: `archive/local-2026-10-28`, pushed.
- Lint: gating 128 (CARD 21 · HEX 73 · BACKBAR 3 · CODEBEHIND 10 · OVERLAY 1 · RESPONSIVE 1 · ICON 19), none allowed.

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| uno-material: ThemeResource tokens with Light/Dark values | `Vigil/Themes/Clover.xaml` (CloverPhotoBase, CloverLiveBand) |
| Skia theme snapshot on ActualThemeChanged (ThemePalette) | `Vigil/Controls/PetBackdrop.cs` (ResolveInks) |
| uno-scaffolding Breakpoints: Responsive + page ResponsiveLayout | `Vigil/Presentation/Board/BoardPage.xaml` (BoardLayout) |
| Loc.T/F + tools/fr.json | `Vigil/Presentation/Parent/ParentPreviewModel.cs` |

## Next actions (in order)
1. Runtime pass (uno-verify; user approved ~15 min of window time): `uno_app_start` with `--clover-preview`.
   Board, record (open a card), parent preview; light and dark; widths 1600, 1100, 700, 640, 393. Check:
   - dark: no-photo initials and placeholder are dark, reminder card text readable, live band dark gradient;
   - Responsive bands: frame margin <600, live band stacks <660, tabs on second row <760, agenda docks >=1120;
   - type floor: ruler blocks under 56 px show the name only, nothing clipped;
   - record hero: five side buttons at 48 px, hero grows instead of clipping;
   - French: switch language in Settings, check record pane titles and parent preview.
2. M3 remainder: agenda as a `!Agenda` flyout route (needs a Page + model split; ask the user first).
3. Normalize, one page at a time: FontSize literals to Clover type styles, hex to tokens, CARD to CardContentControl.
4. Hold /quieter (gradient, glass, blur, taglines, mascot) for the user's decision. M4/M5 perf after.

## Open questions
- 48 px side buttons vs the project's 56 px glove rule (56 overflows the 520 px hero).
- Record hero stays light in dark theme (fixed dark inks). Give it dark inks too?
- French strings are a Québec-register draft; native review before shipping?
- 128 lint hits carry no `allow` reasons.

## Relaunch
```
cd C:\Users\Platform006\Vigil ; git checkout archive/local-2026-10-28
dotnet build Vigil/Vigil.csproj -p:VigilTargetFrameworks=net10.0-desktop ; dotnet test Vigil.Tests/Vigil.Tests.csproj
uno_app_start  projectPath=C:\Users\Platform006\Vigil\Vigil\Vigil.csproj  tfm=net10.0-desktop  args=--clover-preview
Strings: python tools/localize.py --write   (fails on missing French)
```
