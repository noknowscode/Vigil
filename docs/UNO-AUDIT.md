# uno-audit (2026-11-04)

Scope: branch `feat/production-readiness` from `add78e7`. The pass covers hygiene, pattern conformance, the semantic
lint gate, performance, the build gate and a runtime re-check of every touched surface on the Debug desktop head
(Xvfb, software renderer). It is not a feature pass: functional gaps stay in `docs/COMPLETENESS.md`. The End
anesthesia header button and the "No monitor connected" pill (7741ede, 69d1a6f) are kept.

## Skills applied

| Skill | Pattern it governs here |
|---|---|
| `uno-platform-agent` | Single project on Uno.Sdk, UnoFeatures, conditional code (`#if DEBUG` hooks), Uno0001 as an error |
| `uno-scaffolding` (+ runtime gotchas) | `{Binding}` on MVUX pages, the code-behind budget, semantic-first styling, the lint gate, the workaround protocol |
| `mvux` | Feeds and states, ErrorTemplate Retry by `ElementName`, commands from public model methods, `ForEach` saves |
| `uno-navigation` | Region navigation through `ViewMap`/`DataViewMap`/`RouteMap`, `!` sheets, `./-/` shell routes |
| `uno-material` | Role brushes and the app's type ramp (`Themes/Tokens.xaml`), Material `InfoBar`/buttons |
| `uno-toolkit` | `utu:Responsive`, `CardContentControl`, `ShadowContainer`, `AncestorBinding`, `ItemsRepeater` lists |
| `winui-xaml` | Virtualization, `KeyboardAccelerator`, `x:Bind` only in typed item templates, accessibility names, `x:Uid` |
| `dotnet-csharp` | Async guards (`async void`), exception filters, atomic file writes, no blocking waits |

## Semantic lint (gating rules must be 0)

`xaml-semantic-lint.ps1 -Path Vigil`

| Rule | Before (add78e7) | After | How |
|---|---|---|---|
| CARD | 0 | 0 | |
| HEX | 0 | 0 | |
| TOKENTHEME | 0 | 0 | |
| BACKBAR | 0 | 0 | |
| CODEBEHIND | 2 | 0 | Escape: `KeyboardAccelerator` on the pane's Close button (handler deleted). Pane width clamp: kept, `xaml-lint: allow codebehind` with its reason (`Presentation/Record/RecordPage.xaml.cs:11`) |
| OVERLAY | 0 | 0 | |
| RESPONSIVE | 0 | 0 | |
| BUILTIN | 0 | 0 | |
| ICON | 2 | 0 | CareMark geometry keyed in `Themes/Icons.xaml` (`CareMarkLeaves`, `CareMarkCentre`, `CareMarkStem`) |
| TOKEN (info) | 464 | 463 | refs to 62 app brush keys (the chart label brush moved into the type ramp) |
| ADAPTIVE (info) | 0 | 0 | `utu:Responsive`: 231 |
| TEMPLATE (info) | 16 | 18 | +2: the CareMark fill and stroke layers (`Controls/CareMark.xaml`, reason in the file) |
| WORKAROUND (info) | 1 | 1 | |

## Commits

| Commit | Category | Change |
|---|---|---|
| 7e819d1 | hygiene | Template AppConfig, ReadMe and log comments removed; 30 redundant usings; stale vitals-strip comments |
| b78b481 | lint ICON | Care mark drawn from keyed path data in `Themes/Icons.xaml` |
| f627caf | lint CODEBEHIND | Escape closes the record pane through a KeyboardAccelerator; clamp kept with an allow; check header at 390 px |
| 1b44591 | failure handling | `CrashReporter`: global handlers, guarded `OnLaunched`, `crash.log` |
| f69a057 | failure handling | Atomic preference writes; Settings "Not saved" bar; tests |
| e545c57 | conformance | Chart label size from the type ramp (`TypeMonoChart`) |
| cee083f | performance | Readings history virtualized (`ItemsRepeater`) |
| 8d4006d | hygiene | One visual-tree helper for the Debug hooks and the timing run |

## Findings and fixes

### Code hygiene (7e819d1, 8d4006d)
- Template `AppConfig` section was registered and never read: `Models/AppConfig.cs`, `.Section<AppConfig>()`
  (`App.xaml.cs`), its template test `Vigil.Tests/AppInfoTests.cs`, `appsettings.development.json` (held only that
  section) removed. `appsettings.json` keeps the localization cultures.
- Template leftovers: `Vigil/ReadMe.md`, the commented-out log-level lines and step narration in `App.xaml.cs`,
  the `App()` template doc comment.
- 30 `using` directives covered by global usings, across 20 files; `Microsoft.Extensions.Options` and the
  `ApplicationExecutionState` alias in `GlobalUsings.cs` were unused (measured with IDE0005/IDE0051/IDE0052/IDE0060
  raised to warnings in a one-off build).
- Stale comments describing the deleted Skia vitals strip: `RecordModel.cs` (ReduceMotion), `Styles/VitalsPalette.xaml`,
  `Controls/ThemePalette.cs`, `Controls/VitalStepper.xaml`, `Vigil.csproj` (the x64/ARM64 reason is now ShadowContainer's SkiaSharp).
- Duplicate visual-tree helpers in `App.xaml.cs` and `Services/PageTimer.cs` now share `Services/VisualTree.cs` (Debug and timing builds only).
- Kept on purpose: `RecordModel._alive` (IDE0052) is the Debug live-instance counter for `--vigil-memlog`; the unused
  `sender`/`args` parameters (IDE0060) are event-handler signatures.

### Lint gate (b78b481, f627caf)
- `Controls/CareMark.xaml`: five `Ellipse`s and an inline `Path` became three layers drawn from keyed path data
  (`F1` so the overlapping leaves fill as one shape). Same 36 x 40 geometry; checked in both themes at 1280 and 390.
- `Presentation/Record/RecordPage.xaml(.cs)`: the Escape `KeyDown` handler is replaced by
  `<KeyboardAccelerator Key="Escape" />` on `DrawerClose`, which runs `ClosePane`. Measured: without the handler or the
  accelerator, Escape left the pane open (the SplitView does not light-dismiss on Escape here); with the accelerator it closes
  and focus returns to the nav item that opened it.
- The `OpenPaneLength = min(640, workspace width)` clamp stays in code-behind with an allow comment: it is a continuous
  clamp to the workspace (window minus sidebar), not a band `{utu:Responsive}` can express. The replacement the skill
  names (a `!Route` drawer) is under Remaining.
- Found at runtime, fixed: the Preparation pane's "Pre-anesthetic check" title ran under "5 of 5 checks" at 390 px
  (single-cell Grid). It now has a `*,Auto` grid and wraps.

### Failure handling (1b44591, f69a057)
- `App.OnLaunched` (`async void`) had no guard and nothing listened for unhandled exceptions. `Services/CrashReporter.cs`
  attaches to `Application.UnhandledException`, `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`;
  the launch runs in `LaunchAsync` inside a try that reports and rethrows. Each exception is reported once, at Critical to
  the app log (once the host exists, console before) and to `crash.log` in the data folder. Behaviour is otherwise unchanged.
- Measured on desktop (Debug hook `--vigil-crash=launch|ui|task`): all three paths write `crash.log` and the log.
  Uno 7 desktop does not raise `Application.UnhandledException` for exceptions thrown in key handlers or in
  `DispatcherQueue` work (its dispatcher catches and only traces them, `NativeDispatcher.LogExceptionEvent`); a Loaded
  handler does raise it. A launch failure that is rethrown leaves the process running without a window on desktop.
- Preference saves ran in state `ForEach` callbacks with nothing to catch a failed write, and the file was written in
  place. `Services/Preferences.cs` now applies the value, then writes a temp file and moves it over the saved one;
  `SettingsModel` catches IO and access failures on every preference save, logs them and shows a "Not saved" bar
  (English and French) with Dismiss. Measured: with the write blocked, the bar shows, the value applies for the session
  and `preferences.json` is unchanged; after unblocking, the next change saves and clears the bar.
- Tests added (`Vigil.Tests/ResilienceTests.cs`): crash file written once and never throws; failed preference write
  keeps the file and the value; Settings shows and dismisses the bar.

### Pattern conformance
- MVUX pages bind with `{Binding}`; `x:Bind` appears only in item templates typed with `x:DataType`
  (`Presentation/Dose/DosePage.xaml:59`, `Presentation/Record/RecordPage.xaml:416, 599, 787, 822`). Pass.
- Region navigation: routes in `App.xaml.cs` `RegisterRoutes`; navigation from model commands; no page code-behind
  navigates. Pass.
- Literal font sizes outside the token files: the record chart's `ChartLabel` set 10/12 on the page; it is now the
  `TypeMonoChart` role in `Themes/Tokens.xaml` (e545c57). No other literal sizes or hex outside palette/token files.
- FeedView error templates bind Retry through `ElementName` to the model's `Retry` (D3). Pass.
- `{utu:Responsive}` is never set on a `ColumnDefinition`/`RowDefinition`; no cached pages (`NavigationCacheMode`). Pass.
- Not changed (info): 144 converter-based `Visibility` bindings have no `FallbackValue=Collapsed` (runtime gotcha: on
  Android, panels can flash visible before the DataContext arrives). Most sit inside FeedView value templates, which only
  exist once data does.

### Performance
Measured on the Debug desktop head under Xvfb with the software renderer, on a shared 4-core box (load average about
4.5), so treat the CPU figures as relative.

- **Lists.** Patient cards (Today, Patients) and the Agenda's preparing list are `ItemsRepeater`s. The Medical history
  readings were an `ItemsControl` that built every row; now an `ItemsRepeater` (cee083f), scrolled first to last row on desktop.
  Doses, lab panels and the Gantt rows stay `ItemsControl`s: a handful of rows each.
- **Care ruler.** The previous audit inferred that `CareRuler.Draw` rebuilt every second. Measured with a temporary
  trace on Agenda: 7 draws while the page loads and sizes, then one per minute (the feed is cut to the minute). No change needed.
- **Live pulse.** Today with a case under anesthesia: 25.5 % of one core over 15 s; the same day with Reduce motion on
  (no pulse, same clocks): 5.8 %; without a live case: 3.4 %. The `RepeatBehavior="Forever"` storyboard on the live card's
  dot (`Presentation/Board/PatientCardView.xaml:82`) costs about 20 % of a core while it runs (runtime gotcha: a Forever
  storyboard keeps the compositor producing frames). It is the design's pulse and Reduce motion removes it; see Remaining.
- **Clocks.** One 1 Hz `Feed.AsyncEnumerable` per page; the record view is not rebuilt by it (`CaseTimers`, covered by
  `RecordModelTests.TheClockMovesTheTimersNotTheRecordView`).
- **Startup.** Store reads are async file IO; `Preferences` reads one small JSON file synchronously when DI first builds it.
  No blocking waits (`.Result`, `.Wait()`) anywhere. No Skia drawing remains (the vitals strip was removed earlier),
  so there are no per-frame Skia objects to cache.

## Stack currency (reported, not changed)

| Package | Pinned | Newest | Note |
|---|---|---|---|
| Uno.Sdk | 7.0.0-dev.115 | 7.0.0-dev.132 (dev), 6.7.30 (stable) | Owner decision to stay on dev.115. Brings Uno.WinUI 7.0.0-dev.2509, Extensions 8.0.0-dev.150, Toolkit 11.0.0-dev.157, Material/Themes 9.0.0-dev.61, SkiaSharp 4.151.1 |
| coverlet.collector | 6.0.2 | 10.1.0 | major |
| FluentAssertions | 6.12.0 | 8.11.0 | v8 requires a commercial licence for commercial use; stay on 6.x or move to AwesomeAssertions |
| Microsoft.NET.Test.Sdk | 18.10.1 | 18.11.0 | minor |
| NUnit | 4.6.1 | 5.0.0 | major |
| NUnit3TestAdapter | 6.3.0 | 6.3.1 | patch |

.NET SDK 10.0.113, TFMs net10.0-*. Toolkit 11.0.0-dev includes the `ResponsiveExtension` reconnect fix (#1647).

## Build, tests, runtime

| Gate | Result |
|---|---|
| Release desktop (`-p:VigilTargetFrameworks=net10.0-desktop`) | 0 warnings, 0 errors (also with `-p:VigilPageTimer=true`) |
| Release WebAssembly (`-f net10.0-browserwasm`) | 0 warnings, 0 errors |
| Release Android (`-f net10.0-android`, local SDK paths) | 0 warnings, 0 errors |
| Tests (`dotnet test`, Debug and Release) | 159/159 (was 156: the template AppConfig test removed, 4 added). One run in 29 failed `RecordModelTests.AFailedWriteSaysSoWithoutPathsOrIds` right after a build and did not recur |
| WinAppSDK, iOS | not buildable on Linux; CI covers them |
| CI | Last green run is at `add78e7` (run 27). The push of this pass was refused by the session's permission check, so the commits are local and CI has not run on them |

Runtime (Debug desktop, `APP_NO_HOTDESIGN=1`, `VIGIL_X11_RENDERER=software`, Xvfb, xdotool):

- Record, 1280 light: Preparation pane opened by `--vigil-route=Record:Preop`, Escape closes it; Medical history opened
  by a click, Escape closes it and focus returns to the nav item. The End anesthesia button and the "No monitor connected"
  pill are present.
- Record, 390 dark: the pane fills the width (clamp), Escape closes it, the check header no longer overlaps.
- Care mark: pet-parent preview, 1280 light and 390 dark (header and card marks).
- Crash reporting: `--vigil-crash=launch`, `ui` and `task` each wrote `crash.log` and the log.
- Settings, 1280 light and dark, 390 dark in French: with `preferences.json.tmp` blocked the "Not saved" bar shows and
  the value applies for the session while `preferences.json` stays unchanged; Dismiss hides it with no gap left; after
  unblocking, the next change saves (no temp file left) and the bar stays closed.
- French loads after the AppConfig removal (appsettings still carries the cultures).
- Record chart labels (French, 1280 light) and the Medical history repeater (first to last row).
- Agenda: care ruler redraw count (above). Today: CPU (above).

## Remaining

| Item | Why it is not in this pass | Size |
|---|---|---|
| Split the record workspace: `RecordModel` drives seven panes (Preop, Dose, Drugs, Monitor, Recovery, History, Labs) and `RecordPage.xaml` is about 900 lines. Each pane becomes a `!Route` page in a drawer (`DrawerFlyoutPresenter`, proportional `DrawerLength`) with its own model (`DataViewMap<…, CaseRef>`), which also retires the `OpenPaneLength` clamp and the pane focus code in `RecordPage.xaml.cs` | Structural refactor; the lint gate is met with one justified allow | 1.5 to 2 days |
| Model tests: 4 of 11 models have them (Record, Dose, Board, Settings). Missing: Shell, Patients, Agenda, NewCase, Menu, ParentPreview, SignOff | Reported, not built, in this pass | about 1 day |
| Live pulse on Today costs about 20 % of a core while a case is under anesthesia (software GL) | Design decision: keep it, or stop the Forever storyboard after a few cycles | 1 hour once decided |
| A launch failure is now recorded, but on desktop the rethrown exception leaves a process with no window (Uno's dispatcher swallows it) | Behaviour decision: exit after reporting; `Application.Exit()` was seen to exit 0xE0434352 (HANDOFF) | 1 hour |
| 144 converter-based `Visibility` bindings without `FallbackValue=Collapsed` (Android gotcha) | Needs an Android pass to see which ones flash | 2 hours with a device |
| `VitalStepper` builds "Increase …"/"Decrease …" accessible names in English (`Controls/VitalStepper.xaml.cs`) while the vital names are French | Localization, not hygiene | 30 minutes |
| French: the Light theme reads "Léger" (should be "Clair", `tools/fr.json`) | Native French review is already open (HANDOFF) | 5 minutes |
| Test packages behind (table above); FluentAssertions 8 changes licence | Owner decision | 1 hour |
| Android and WebAssembly runtime proof | This pass built both heads; no device or browser run | half a day |

## Follow-up: the A pass (2026-11-04)

What happened to each Remaining item above, on `feat/production-readiness` (commits f50c14b to the pass's last one).

| Item | Outcome |
|---|---|
| Split the record workspace | Done (f50c14b, ca7c793, 7731f4b). Each pane is a `!` route to a Flyout view in the Toolkit `DrawerFlyoutPresenter` (`RecordDrawerPresenterStyle`, `DrawerLength` 640 px, which the presenter takes as min(640, window): the old clamp) with its own model: `PreopModel`, `DoseModel` (shared with the Redose page), `MedicationsModel`, `VitalsModel`, `RecoveryModel`, `HistoryModel`, `LabsModel`, over a shared `CaseDrawer` (case feed, Not saved bar, close, hand back). `RecordModel` went from 306 to about 150 lines and keeps the page; `RecordPage.xaml` from 903 to 439 lines; `RecordPage.xaml.cs` is `InitializeComponent` only. Escape and the scrim close a drawer (the flyout's light dismiss); focus goes back to the opener through `Controls/FlyoutFocus.cs`, an attached property, because Uno's flyout left it on the page (measured with `--vigil-focuslog`). Decision D8. Lint CODEBEHIND stays 0 with no allow left. |
| Model tests for Shell, Patients, Agenda, NewCase, Menu, ParentPreview, SignOff | Done (7a928e7): 30 tests over a recording navigator (`Vigil.Tests/TestNavigation.cs`). The drawers' models add 23 (`DrawerModelTests.cs`, `ModelTests.cs`) and the case form 10 (`CaseEditTests.cs`). Every page and drawer model now has tests. |
| Live pulse on Today (about 20 % of a core while a case is under anesthesia) | Not changed: an open owner decision (keep the design's pulse, or stop the Forever storyboard after a few cycles). |
| A rethrown launch failure leaves a process without a window on desktop | Not changed: behaviour decision, unchanged since this audit. |
| 144 converter `Visibility` bindings without `FallbackValue=Collapsed` | Done (fe1ecb5): all 144 carry it, ahead of the emulator run. |
| `VitalStepper` names in English | Done (aa4ce16): `Stepper_Increase` / `Stepper_Decrease` through `Loc`, French "Augmenter : …" / "Diminuer : …". |
| French "Léger" for the Light theme | Done (e1e253c): "Clair". `tools/fr.json` gained an `@keys` object for a key that needs its own French (the plane stays "Léger"). |
| Test packages behind | Not changed (owner decision on FluentAssertions' licence). |
| Android and WebAssembly runtime proof | WebAssembly: run in headless Chromium this pass (docs/evidence/a-pass-2026-11-04). Android: the emulator smoke job in CI (7ba746e, 045e80f). |

Found and fixed at runtime during the pass: the Dose drawer and the Redose page showed type names in their fields
(their routes held new ViewMaps without the bindable model, ca7c793); a drawer that handed over to a sheet could see
its focus restore close that sheet (7731f4b); the vitals pad charged 80 px of spacing for its unused rows.
