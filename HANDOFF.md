# HANDOFF — Vigil: Clover fixes, after the runtime pass
Updated: 2026-10-28 (America/Montreal)

## Where we are
The runtime pass over the audit fixes is done on `archive/local-2026-10-28` (clone at `C:\Users\Platform006\Vigil`).
Board, record and parent preview were checked through the App MCP in light and dark at 1600, 1100, 700, 640 and 393, and in
French at 393. Every Responsive band, the 48 px side buttons, the hero growth, the dark placeholders, the live band, the
reminder card and the French strings passed. One defect turned up and was fixed in 62be117: in dark mode the agenda ruler's
block names drew Light ink, because `CareRuler` resolved unthemed `*Brush` keys. Results table and captures:
`docs/evidence/runtime-2026-10-28/RESULTS.md`. App preferences were reset to System theme and English afterwards.

## Last verified state
- Build: desktop 0 warnings, 0 errors. Tests 68/68. WASM/Android/iOS not rebuilt since the fixes.
- Runtime: verified via uno-app (see RESULTS.md). Hot Reload was unavailable: the Uno Platform account is signed out.
- Git: `archive/local-2026-10-28`, 62be117 + docs commit; not yet pushed.
- Lint: gating 128 (CARD 21 · HEX 73 · BACKBAR 3 · CODEBEHIND 10 · OVERLAY 1 · RESPONSIVE 1 · ICON 19), none allowed.

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| Ink resolved per element theme from themed `*Color` keys (gotchas, Theming) | `Vigil/Controls/CareRuler.cs:57` |
| Skia theme snapshot on ActualThemeChanged (ThemePalette) | `Vigil/Controls/PetBackdrop.cs:36` |
| uno-scaffolding Breakpoints: page ResponsiveLayout | `Vigil/Presentation/Board/BoardPage.xaml:4` |

## Next actions (in order)
1. Ask the user: M3 agenda as a `!Agenda` flyout route (needs a Page + model split). This also clears the OVERLAY,
   RESPONSIVE and most CODEBEHIND hits in `BoardPage.xaml.cs`.
2. Normalize, one page at a time: FontSize literals to Clover type styles, hex to tokens, CARD to CardContentControl.
3. Hold /quieter (gradient, glass, blur, taglines, mascot) for the user's decision. M4/M5 perf after.
4. Rebuild WASM/Android/iOS heads once the next batch lands.

## Open questions
- Below 600 px the record's five side buttons collapse by design. Are Vitals, Medical history, Lab results and Medications still reachable on a phone?
- Switching the language back to English saves correctly but shows no "Language saved" notice (French shows it).
- 48 px side buttons vs the project's 56 px glove rule (56 overflows the 520 px hero).
- Record hero keeps fixed light inks in dark theme while its stats card goes dark. Give the hero dark inks too?
- French strings are a Québec-register draft; native review before shipping?
- 128 lint hits carry no `allow` reasons.
- Sign in to Uno Platform to restore Hot Reload for the next UI session.

## Relaunch
```
cd C:\Users\Platform006\Vigil ; git checkout archive/local-2026-10-28
dotnet build Vigil/Vigil.csproj -p:VigilTargetFrameworks=net10.0-desktop ; dotnet test Vigil.Tests/Vigil.Tests.csproj
uno_app_start  projectPath=C:\Users\Platform006\Vigil\Vigil\Vigil.csproj  tfm=net10.0-desktop  args=--clover-preview --clover-size=1600x1000
Before a rebuild: uno_app_close, then Get-Process Vigil | Stop-Process -Force (exe lock).
Strings: python tools/localize.py --write   (fails on missing French)
```
