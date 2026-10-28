# HANDOFF — Vigil: Clover redesign, greeting and agenda route
Updated: 2026-10-28 (America/Montreal)

## Where we are
On `archive/local-2026-10-28`. This session: runtime pass (results `docs/evidence/runtime-2026-10-28/RESULTS.md`),
dark ruler fix (62be117), agenda moved to its own `AgendaPage`/`AgendaModel` route, docked from 1120 and a `!Agenda`
flyout below (599e73d), then the redesign (c3bc0ea): "Good morning, Dr. <surname>" greeting with a recap subtitle
(surname in Settings), outer shell 24 px in, agenda as a card after the user's "Patient care" popover reference,
illustration removed. All verified through the App MCP at 1600 and 1100, light and dark.

## Last verified state
- Build: desktop 0 warnings, 0 errors. Tests 72/72 (GreetingTests added). WASM/Android/iOS not rebuilt.
- Runtime: greeting with/without surname, recap, shell, docked and flyout agenda, record opened from both. Not run at 393.
- Git: c3bc0ea local, not pushed (37a375e and earlier pushed).
- Lint: gating 113 (CARD 16 · HEX 73 · BACKBAR 3 · CODEBEHIND 2 · OVERLAY 0 · RESPONSIVE 0 · ICON 19).

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| uno-navigation: `!` route to a Page opens a flyout; nested default route fills a content region | `Vigil/App.xaml.cs` (Board Nested Agenda), `BoardPage.xaml` AgendaToggle |
| Workaround protocol: collapsed region blocks startup navigation | `Vigil/Presentation/Board/BoardPage.xaml` (WORKAROUND above AgendaDock) |
| Themed `*Color` keys for drawn ink | `Vigil/Controls/CareRuler.cs:57` |
| Responsive on elements, not code-behind | `BoardPage.xaml` HomeShell Grid.Column/ColumnSpan, AgendaDock Width/IsEnabled |

## Next actions (in order)
1. Lint quick wins (77 of 113): delete the now-unused `Controls/CareGreeting.xaml(.cs)` (41; ask first, it deletes
   files) and move the hex tokens from `Themes/Clover.xaml` into `Styles/CloverPalette.xaml` (36).
2. Normalize RecordPage (23 hits), then Board/ParentPreview (11). Mark CareMark (brand mark) as an allowed exception.
3. Runtime pass at 393 and in French for the greeting, recap and agenda card.
4. Hold /quieter (gradient, glass, blur, taglines, mascot) for the user. M4/M5 perf after.

## Open questions
- Add the collapsed-region gotcha to `~/.claude/rules/uno-runtime-gotchas.md` with a reduced repro, and file upstream?
- The navigation flyout has no scrim; style it as a side drawer (DrawerFlyoutPresenter), or keep the default?
- Agenda CTA is 56 px tall (glove rule) where the reference is about 44.
- Record side buttons collapse under 600: are Vitals/History/Labs/Meds reachable on a phone?
- "Language saved" notice missing when switching back to English.
- 48 px record side buttons vs the 56 px glove rule. Record hero keeps light inks in dark theme.
- French strings (incl. the greeting) are a Québec-register draft; native review before shipping?
- Sign in to Uno Platform to restore Hot Reload.

## Relaunch
```
cd C:\Users\Platform006\Vigil ; git checkout archive/local-2026-10-28
dotnet build Vigil/Vigil.csproj -p:VigilTargetFrameworks=net10.0-desktop ; dotnet test Vigil.Tests/Vigil.Tests.csproj
uno_app_start  projectPath=C:\Users\Platform006\Vigil\Vigil\Vigil.csproj  tfm=net10.0-desktop  args=--clover-preview --clover-size=1600x1000
Before a rebuild: uno_app_close, then Get-Process Vigil | Stop-Process -Force (exe lock).
Strings: python tools/localize.py --write   (fails on missing French)
```
