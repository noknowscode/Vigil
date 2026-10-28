# Vigil: spec

> **v2 redesign (2026-10-28):** the Today dashboard, Patient care screen, app shell and design tokens are specified in `SPEC-v2.md`, which supersedes the Board and Record layouts below.

Vigil is the anesthesia record for surgical vet techs in a small-animal hospital. The tech sets it on
a tablet beside the patient, records the pre-anesthetic check and weight-based doses, enters vitals
every 5 minutes during the procedure, logs recovery, and the veterinarian signs the record. It
replaces the paper anesthesia sheet clipped to the anesthesia machine.

- Concept choice and validation: `docs/VALIDATION.md`, `docs/DECISIONS.md` (D1).
- Visual direction source: `docs/design/DESIGN.md` → DesignMd2Uno output in `docs/design/generated/`.
- Date: 2026-10-06. Uno.Sdk 6.7.30 (latest stable on NuGet today), .NET 10.0.303.

**Who, where, what.** Who: a registered vet tech (RVT) running anesthesia, plus the supervising
veterinarian who signs. Where: an operating room, often dim, tablet on a stand ~1 m away, gloved
hands. What: one record per anesthetic procedure, several per day.

**Riskiest assumption the build tests** (from VALIDATION.md): a full vitals set can be recorded in
under 15 s with gloves on. The Monitor screen is designed around that number, and the build log
measures it (taps per reading, seconds per reading by peer actions).

## Tools from `~/uno-tooling.md` this app uses

| Tool | Used for | Phase |
|---|---|---|
| DesignMd2Uno | `DESIGN.md` → `ColorPaletteOverride.xaml`, `Typography.xaml`, `Tokens.xaml`, contrast report (24/24 pass) | 1 (done) |
| uno-scaffold rules + semantic XAML lint (PostToolUse hook) | Every `.xaml`/`.xaml.cs` edit; `xaml-semantic-lint.ps1` before each screen is called done | 2, 3 |
| uno-fleet-kit skills (standalone): `uno-build-log`, `uno-break-it` | `docs/fleet/BUILD-LOG.md`, `docs/fleet/FINDINGS.md` | 2, 3 |
| AppMap / Atlas (`atlas extract`) | Diff the built route tree against the route tree below | 3 |
| Workflow skills: `uno-verify`, `uno-component-states`, `gold-standard-pass`, `uno-audit`, `uno-completeness-audit` | Build gates | 2, 3 |
| MotionTokens | Source for `Themes/MotionTokens.xaml` if its vocabulary matches the house values; otherwise the file is written from `xaml-design-polish` | 2 |

Not used, with reason: UnoAnnotation (backlogged, not installed), Design Graph plugin (the Spec Graph
Brief below carries the design graph as text), Tactile (physical shader surfaces do not fit a clinical
record), ThemeStudio (one design system), Lapse (optional per-commit visual history; not needed to
meet any gate), Uno Previews MCP (never built).

---

## Architecture Brief

### Targets and versions

| Head | TFM | Role |
|---|---|---|
| Desktop (Skia: Windows, Linux, macOS) | `net10.0-desktop` | Development head; App MCP verification |
| Android (tablet first) | `net10.0-android` | Primary production form factor |
| iOS / iPadOS | `net10.0-ios` | Production form factor; CI simulator build only (no Mac here) |
| WebAssembly (PWA) | `net10.0-browserwasm` | Front-desk / review station |
| (tests) | `net10.0` | Unit tests reference the app |

No WinAppSDK head: the desktop Skia head covers Windows, and dropping it removes a fifth CI head with
nothing app-specific to prove. Uno.Sdk 6.7.30 is pinned in `global.json`; packages in
`Directory.Packages.props`.

### UnoFeatures

Template: `Material; Hosting; Toolkit; Logging; MVUX; Configuration; Localization; Navigation;
ThemeService; SkiaRenderer`. Add `Skia` (Toolkit `ShadowContainer` for the floating Record button,
and `SKCanvasElement` for the strip on every head). No `Http` (no remote source in v1, see the
capability inventory), no `Authentication` (see Unresolved Questions).

### MVUX

Decision: MVUX for every page.
Reason: the record is durable async state (a store that loads from disk, can fail, and changes while
the page is open), and FeedView gives Loading/Empty/Error per surface. Project convention for app-level
state.
Tradeoff: MVUX binding traps (`{Binding}` only, `Refresh` by ElementName in ErrorTemplate,
`Parent.Draft.X` for edits). Each is named in the Interaction Brief where it applies.

### Shell and regions

No Shell control: the template's `InitializeNavigationAsync(initialRoute: "Board")` hosts a root Frame.
`RecordPage` owns an inline-content Visibility region for its four tabs (Pre-op, Drugs, Monitor,
Recovery): they share `RecordModel`, so inline mode (named child Grids, no nested routes) is the
documented fit. Sheets (`NewCase`, `Dose`, `SignOff`) are `!` routes to Pages styled with a
`DrawerFlyoutPresenter` style (right drawer on wide, bottom on narrow).

### Routes (spec for `RegisterRoutes`)

```csharp
views.Register(
    new ViewMap<BoardPage, BoardModel>(),
    new ViewMap<NewCasePage, NewCaseModel>(),
    new DataViewMap<RecordPage, RecordModel, CaseRef>(),
    new DataViewMap<DosePage, DoseModel, CaseRef>(),
    new DataViewMap<SignOffPage, SignOffModel, CaseRef>(),
    new ViewMap<SettingsPage, SettingsModel>());

routes.Register(
    new RouteMap("Board", View: views.FindByViewModel<BoardModel>(), IsDefault: true),
    new RouteMap("NewCase", View: views.FindByViewModel<NewCaseModel>()),
    new RouteMap("Record", View: views.FindByViewModel<RecordModel>()),
    new RouteMap("Dose", View: views.FindByViewModel<DoseModel>()),
    new RouteMap("SignOff", View: views.FindByViewModel<SignOffModel>()),
    new RouteMap("Settings", View: views.FindByViewModel<SettingsModel>()));
```

`CaseRef` is `partial record CaseRef(Guid Id)`: navigation passes an id, and each model reads the case
from the store, so a sheet and the page behind it never hold diverging copies.

### Services (DI in `ConfigureServices`)

| Service | Lifetime | Job |
|---|---|---|
| `ICaseStore` → `JsonCaseStore` | Singleton | Load/save cases as JSON (one file per case) under `ApplicationData.Current.LocalFolder/cases`; in-memory cache; `IAsyncEnumerable<CaseChange> Watch(ct)` change stream |
| `IFormulary` → `EmbeddedFormulary` | Singleton | Drug list, concentrations, mg/kg ranges, species alarm thresholds from an embedded `formulary.json` (sample reference data, labelled in the UI) |
| `IClock` → `SystemClock` | Singleton | `Now` + a 1 Hz `IAsyncEnumerable<DateTimeOffset> Ticks(ct)`; replaced by a fake clock in tests |
| `IRecordExporter` → `CsvRecordExporter` | Singleton | Signed record → CSV + plain-text summary |
| `IFaultInjection` | Singleton | Reads `VIGIL_FAULT` / `VIGIL_SLOW_MS` env vars (desktop/Android) to prove Error/Loading states at runtime. Inert when unset; never shipped as a feature |

Domain logic (dose math, range classification, reading-due computation, MAP derivation, CSV) lives in
`Vigil/Domain/` as plain C# with no Uno dependency, tested from `Vigil.Tests`.

### Domain records

```csharp
enum Species { Canine, Feline, Other }
enum CaseStatus { Scheduled, Anesthetized, Recovery, Signed }
enum Plane { Light, Surgical, Deep }
enum DoseRoute { IV, IM, SC, PO }

partial record Patient(string Name, Species Species, string Breed, decimal WeightKg, decimal AgeYears, string Owner);
partial record PreopCheck(bool Fasted, bool IvCatheter, bool MachineChecked, bool ConsentSigned, bool BloodworkReviewed, int Asa, bool AsaEmergency, string Notes);
partial record VitalsReading(DateTimeOffset At, int? Hr, int? Rr, int? SpO2, int? EtCo2, int? Sys, int? Dia, int? Map, decimal? TempC, decimal? VaporizerPct, decimal? O2LMin, Plane Plane, string Note);
partial record DoseGiven(Guid Id, DateTimeOffset At, string DrugId, string DrugName, decimal MgPerKg, decimal MgPerMl, decimal VolumeMl, DoseRoute Route, bool OutOfRangeConfirmed);
partial record RecoveryLog(DateTimeOffset? ExtubatedAt, DateTimeOffset? SternalAt, int? PainScore, decimal? TempC, string Notes);
partial record Case(Guid Id, Patient Patient, string Procedure, string Veterinarian, string Technician,
    DateTimeOffset CreatedAt, CaseStatus Status, PreopCheck Preop, DateTimeOffset? InducedAt,
    ImmutableList<VitalsReading> Readings, ImmutableList<DoseGiven> Doses, RecoveryLog Recovery,
    string? SignedBy, DateTimeOffset? SignedAt);
```

`MAP` is entered when the monitor shows it; when blank it is derived `(Sys + 2·Dia)/3` and shown with
a "calc" label. A signed case is immutable: the store rejects writes to it.

### Models

| Model | Feeds / states | Commands |
|---|---|---|
| `BoardModel` | `IListFeed<CaseSummary> Today` (store watch, today's cases, status order); `IFeed<LiveCase> Live` (the anesthetized case + clock: elapsed, next-due countdown, overdue flag; None when no case is under anesthesia); `IState<BoardFilter> Filter` (Today / All) | `LoadSampleDay` (only from Settings), navigation is XAML |
| `NewCaseModel` | `IState<CaseDraft> Draft`; `IFeed<IImmutableList<string>> Errors` | `Create` → store, then navigate `Record` with `CaseRef` (`-` first so Back returns to Board) |
| `RecordModel(CaseRef)` | `IFeed<CaseView> Case` (store watch + clock, read-only projection incl. alarms); `IState<VitalsDraft> Draft` (pre-filled from the last reading); `IState<PreopCheck> Preop`; `IState<RecoveryLog> Recovery` | `RecordReading`, `Induce`, `EndAnesthesia`, `SavePreop`, `SaveRecovery`, `Export` |
| `DoseModel(CaseRef)` | `IListFeed<Drug> Drugs`; `IState<Drug> Selected`; `IState<decimal> MgPerKg`; `IFeed<DoseCalc> Calc` (Combine: weight, drug, mg/kg → mL, range class, formula text) | `Give` (confirms out-of-range first) |
| `SignOffModel(CaseRef)` | `IFeed<SignOffSummary> Summary`; `IState<string> Veterinarian`; `IState<bool> Confirmed` | `Sign` |
| `SettingsModel` | `IState<int> ThemeIndex`; `IState<string> Language`; `IState<bool> ReduceMotion`; `IState<int> IntervalMinutes` (3/5/10) | `LoadSampleDay`, `ClearAll` (confirm dialog) |

### Platform constraints and fallbacks

| Concern | Fact | Plan |
|---|---|---|
| Reduced motion | `UISettings.AnimationsEnabled` is hard-coded true on Skia desktop | In-app Reduce motion setting, OR'd with the platform value |
| Language switch | `SetCurrentCultureAsync` applies on next start | Settings says "applies after restart"; try the hot-swap guide in a spike, keep the restart note if it fails |
| Theme on launch | `IThemeService` hangs if called before the window root exists | Theme only set from Settings (post-load) |
| Android back | Android 16 ignores `BackRequested.Handled` | Back is `utu:NavigationBar` MainCommand everywhere |
| Fonts on Android | Hyphenated asset names are renamed | All font files use underscores only |
| WASM storage | `LocalFolder` on WASM is IndexedDB-backed | Verify persistence across reload in the WASM pass |
| Responsive on cached pages | Toolkit 9.1 Responsive does not reconnect | No page sets `NavigationCacheMode="Required"` |

### Testing and validation

- Unit tests (`Vigil.Tests`, NUnit + FluentAssertions): dose math and rounding, range classification
  per species, MAP derivation, due/overdue computation, store round-trip and signed-record immutability,
  CSV export, model commands with a fake clock and in-memory store.
- Runtime: `uno-verify` through the App MCP on desktop for every screen; Android emulator and WASM
  passes at milestones; iOS is a CI simulator build only.

### Capability inventory

| Capability the design/domain implies | Status | Choice / note |
|---|---|---|
| Vitals trend chart in anesthesia notation | **Substituted** | Custom `SKCanvasElement` strip (`VitalsStrip`). LiveCharts2 is the ladder's first choice; the paper-record notation (BP chevrons, dot/ring glyphs, 5-min columns, drug row, due column) is the signature and is bespoke. Raised in Unresolved Questions |
| Accessible chart alternative | Implemented | "Table" toggle shows the readings as a list (also the narrow default) |
| Weight-based dose calculator | Implemented | `DoseModel` + formulary; formula shown |
| Reading-due countdown | Implemented | 1 Hz clock feed |
| Audible reminder when a reading is due | **Omitted** | Visual only (countdown, overdue state with icon + text). `MediaPlayerElement` on Skia desktop needs native media deps; raised in Unresolved Questions |
| Local persistence | Implemented | JSON files in `LocalFolder` |
| Export / handoff to the practice management system (PMS) | **Substituted** | CSV + text export via `FileSavePicker`; no PMS integration. Raised in Unresolved Questions |
| Remote sync between tablets | **Omitted** | Single-device store; raised in Unresolved Questions |
| Per-user sign-in / signature | **Substituted** | Typed name + confirm checkbox on a shared device; no authentication. Raised in Unresolved Questions |
| Monitor integration (auto-capture vitals) | **Omitted** | Manual entry; raised in Unresolved Questions |
| Localization EN/FR | Implemented | `.resw` + `ILocalizationService` |
| Light/Dark theme | Implemented | `IThemeService`, Theatre (dark) and Paper (light) |
| Icons | Implemented | Fluent symbol font (`SymbolIcon`/`FontIcon`), one set |
| Patient photo | **Omitted** | Not part of the paper sheet; no camera use. Raised in Unresolved Questions |

---

## Design Brief

### Visual direction

The paper anesthesia sheet, made legible at arm's length. Category check: competitors split between
clinical white-and-teal SaaS and black patient-monitor screens with neon traces. Vigil is neither:
graphite and paper grounds, a single sevoflurane-yellow primary that only ever means "due now", and
the chart drawn in the paper record's own notation.

- **Archetype: the anesthesia sheet.** A patient band across the top (wristband: name, species,
  weight, ASA, procedure), the five-minute strip, the entry pad.
- **Signature: the five-minute strip.** Time columns every 5 minutes, heart rate as dots, systolic ∨
  and diastolic ∧ chevrons, SpO2 rings, a drug-event row along the top, and the current column drawn
  as an outlined yellow slot that fills like a vaporizer sight glass as the next reading comes due.
  At 24 px (app icon): three ruled columns with one ∨∧ pair and a yellow column.
- **Boldness spent once:** the strip and the yellow due slot. Everything else is hairlines, outlined
  cards, one filled-primary action per view.

Anchors (each screen names one; type scale contrast 64/16 = 4×):

| Screen | Anchor | Rule cleared |
|---|---|---|
| Board | Live case band: elapsed `01:12` at 64 px + next-reading countdown; full-bleed | full-bleed |
| Record › Monitor | The strip | ≥ 40% of the screen |
| Record › Pre-op | Patient weight at 64 px (every dose depends on it) | uncontained, 4× |
| Record › Drugs | Computed volume `0.37 mL` at 64 px with its formula | 4× |
| Record › Recovery | Time since extubation at 64 px | 4× |
| Dose sheet | Volume at 64 px | 4× |
| Sign-off | Signed summary: duration + readings count at display scale | 4× |
| Settings | None: a plain list (no anchor needed; utility screen) | n/a: utility |

### Wireframes

```
Wide (≥1080, tablet landscape)                       Narrow (<600, phone)
┌───────────────────────────────────────────────┐     ┌───────────────────────┐
│ ← Bella · Canine · 18.4 kg · ASA II · Spay  ⋯ │     │ ← Bella · 18.4 kg  ⋯  │
│ [Pre-op][Drugs][Monitor][Recovery]  01:12 ▮▮▯ │     │ 01:12   next 2:41     │
├──────────────────────────────┬────────────────┤     │ [Pre][Drug][Mon][Rec] │
│ drug row  ▲Dex ▲Prop         │ HR   [-] 92 [+]│     ├───────────────────────┤
│ 200 ┼──┼──┼──┼──┼──┼─[yel]   │ RR   [-] 12 [+]│     │ strip (h-scroll)      │
│     ∨  ∨  ∨  ∨               │ SpO2 [-] 98 [+]│     ├───────────────────────┤
│ 100 ●  ●  ●  ●   ○○○○        │ SYS/DIA 110/62 │     │ HR [-] 92 [+]         │
│     ∧  ∧  ∧  ∧               │ ...            │     │ ... steppers 2-col    │
│  0  :00 :05 :10 :15 :20      │ [ Record 0:15 ]│     │ [   Record reading   ]│
└──────────────────────────────┴────────────────┘     └───────────────────────┘
```

### Tokens

From DesignMd2Uno (`Styles/ColorPaletteOverride.xaml`, `Styles/Typography.xaml`, `Styles/Tokens.xaml`,
regenerated, never hand-edited). Roles (light / dark):

| Role | Light (Paper) | Dark (Theatre) | Use |
|---|---|---|---|
| Primary | `#7A5A00` | `#F2C230` | due slot, Record button, current tab |
| Secondary | `#3E5F73` | `#9DB8C9` | drug row, secondary actions |
| Tertiary | `#00707F` | `#5CC8D7` | SpO2 |
| Error | `#B3261E` | `#FF6B5E` | out of range, overdue |
| Background | `#F3F5F2` | `#0F1312` | canvas |
| Surface / SurfaceVariant | `#FFFFFF` / `#E6EBE6` | `#161B1A` / `#1E2523` | cards / patient band |
| OnSurface / OnSurfaceVariant | `#121A17` / `#4E5B55` | `#E4EAE6` / `#94A39C` | ink |
| Outline | `#C4CEC8` | `#2C3532` | hairlines, chart grid |

Rung-4 custom tokens (`Styles/VitalsPalette.xaml`, ThemeDictionaries, Light/Dark, each with a glyph):
`VitalHrBrush` (green, ● dot), `VitalBpBrush` (red, ∨ ∧ chevrons), `VitalSpO2Brush` (= Tertiary, ○
ring), `VitalEtCo2Brush` (grey, × cross), `VitalTempBrush` (orange, ■ square). Reason: no Material
role for monitor-convention series. The Skia strip reads them from a palette snapshot taken on
`ActualThemeChanged` (theme gotcha: never a converter).

Type: Archivo Narrow 600 (display 64, headline 40/28), Public Sans 400/600 (title 20, body 16,
body-small 14, label 13), IBM Plex Mono 500 (times, doses 15). Static per-weight TTFs in
`Assets/Fonts`, underscore names (`ArchivoNarrow_SemiBold.ttf`, `PublicSans_Regular.ttf`,
`PublicSans_SemiBold.ttf`, `IBMPlexMono_Medium.ttf`), wired with Uno `.ttf.manifest` files so weights
resolve, and Material's `MaterialRegular/Medium/LightFontFamily` set through `FontOverrideDictionary`.

Spacing 4/8/12/16/24/32/48; radii 6/12/24. Touch-target minimums no longer apply (2026-10-28: the app will not ship on
tablets); size controls to the design, e.g. 40 px icon buttons and 44 px primary buttons in the agenda card.

Motion (values inline where used; the unused `MotionTokens.xaml` was removed 2026-10-28): `EaseSmooth 0.22,1 0.36,1`, `EaseOut 0.17,1 0.32,1`;
durations 150/200/280 ms; entrance = opacity 0→1 + Translation.Y 6→0 over 280 ms; press scale 0.98.

### Dictionary scopes

`App.xaml`: Material theme with ColorPaletteOverride, Typography, VitalsPalette,
Icons (none needed if the Fluent font covers all icons), control lightweight keys (Card radius 12,
Button min height 56). Page-scoped resources: `RecordPage.Resources` (stepper template, strip
sizing). Nothing page-scoped lands in App.xaml.

### Breakpoints

Default Toolkit layout (150/300/600/800/1080). On Record: strip and pad are two fixed star columns; at
Widest (≥1080) the pad is `Grid.Column=1, Grid.Row=0`; below that it is `Grid.Column=0, Grid.Row=1,
ColumnSpan=2` (Responsive on the panels, never on `ColumnDefinition`). Board: Live band full width at
every size; case list one column below 800, two columns from 800 (Responsive on an `ItemsRepeater`
`UniformGridLayout.MaximumRowsOrColumns`).

---

## Interaction Brief

### Flows

1. **Start a case:** Board → `!NewCase` (patient, species, weight, procedure, vet, tech) → Create →
   `Record` (data `CaseRef`), Pre-op tab. Back from Record → Board.
2. **Pre-op:** checklist toggles, ASA chips (I–V + E), premed doses via `!Dose`. Induce (filled
   primary) stamps `InducedAt`, status Anesthetized, switches to Monitor.
3. **Monitor (every 5 min):** steppers are pre-filled from the last reading; adjust what changed;
   Record reading (one tap). Target: ≤ 4 taps and < 15 s when values barely change.
4. **Drugs:** `!Dose` from Drugs tab or Pre-op: pick drug → mg/kg (pre-filled with formulary default)
   → volume shown with formula → Give. Out-of-range asks "Give 2.0 mg/kg? The reference range is
   0.2–0.4." (ContentDialog) before recording.
5. **Recovery:** End anesthesia stamps recovery; log extubation, sternal, pain score (0–4 chips),
   temp. Sign-off → `!SignOff` (summary, vet name, confirm) → Sign → record read-only, Export enabled.
6. **Settings:** theme, language (restart note), reduce motion, interval, sample day, clear all.

### Input

- Steppers: − / + buttons 64 px; long-press repeats (Toolkit `CommandExtensions` not needed: `RepeatButton`);
  tapping the value focuses a numeric `TextBox` (`InputScope="Number"`, `UpdateSourceTrigger=PropertyChanged`).
- Keyboard (desktop): Tab order follows the pad top-to-bottom; Enter in a value box records the reading;
  Ctrl+R records; Escape closes sheets.
- No code-behind except `SKCanvasElement` invalidation (marked `xaml-lint: allow codebehind`).

### FeedView states per async node

| Node | Value | Progress | None | Error |
|---|---|---|---|---|
| Board `Today` | case rows | skeleton rows (3) | "No cases today. Start the first one." + New case | "Couldn't read saved records." + Retry (ElementName Refresh) |
| Board `Live` | live band | skeleton band | quiet band: "No patient under anesthesia" | inherits `Today` error (same store) |
| Record `Case` | tabs | skeleton band + strip | "This record no longer exists." + Back | message + Retry |
| Dose `Calc` | volume + formula | n/a: synchronous math | "Pick a drug" | n/a: pure function, invalid input shown inline |
| SignOff `Summary` | summary | skeleton | n/a: route only reachable with a case | message + Retry |

Retry is proven at runtime with `VIGIL_FAULT=store-read-once` (first read throws, second succeeds).

### Dialogs and flyouts

`!NewCase`, `!Dose`, `!SignOff`: Pages in a `DrawerFlyoutPresenter` style. Out-of-range dose and
Clear all: `ContentDialog` via `INavigator.ShowMessageDialogAsync` with `DialogAction` callbacks
(gotcha: read the captured flag, not the awaited result).

### VisualStates

Stepper value: Normal / OutOfRange (Error brush + ▲/▼ glyph + "High"/"Low" text). Due slot: Waiting /
Due (≤ 30 s, primary fill) / Overdue (Error outline + "Overdue 0:40" text + icon), driven by
`CaseView.DueState` through `utu:VisualStateManagerExtensions.States`. Buttons: Material states + 0.98
press.

### Animations

| Animation | Duration / easing | Reduced motion |
|---|---|---|
| Page content entrance | 280 ms, EaseSmooth, opacity + 6 px rise | none (instant) |
| New strip column after Record | 200 ms, EaseOut, glyph opacity 0→1 drawn in Skia from a one-shot tween | drawn final |
| Record button confirm (check glyph cross-fade) | 150 ms, EaseSmooth | instant swap |
| Due slot fill | 1 Hz value updates, no animation loop | same |

No `Forever` storyboards and no held `CompositionTarget.Rendering` (CPU gotcha).

### Accessibility

- Every stepper: `AutomationProperties.Name` = "Heart rate, beats per minute"; value announced; buttons
  named "Increase heart rate" etc.
- Strip: `AutomationProperties.Name` summary ("12 readings, last at 10:35: heart rate 92 ...") and
  the Table toggle as the accessible alternative.
- Status never by colour alone (glyph + text). Contrast 24/24 pairs ≥ 4.5:1 (DesignMd2Uno report).
- Android Skia TalkBack is WIP upstream: claim screen-reader support on desktop/WASM only after a pass.

### Runtime verification (uno-verify)

Per screen: launch Debug via App MCP, snapshot the visual tree against the page tree, drive the flow
by peer actions, capture Light + Dark, resize through the bands (twice per size), one detail navigation
+ Back, then bands again; drive Error with `VIGIL_FAULT` and invoke Retry.

---

## Spec Graph Brief

### Route tree

```text
Root Frame (InitializeNavigationAsync, initialRoute Board)
├─ Board        ViewMap<BoardPage, BoardModel>   IsDefault
│  ├─ LiveBand    ← FeedView(BoardModel.Live) {Value, Progress, None, Error}  → Record (CaseRef)
│  ├─ CaseList    ← FeedView(BoardModel.Today) {Value, Progress, None, Error} → Record (CaseRef)
│  ├─ NewCase !   flyout · NewCaseModel → -/… then Record (CaseRef)
│  └─ Settings    → Settings
├─ Record       DataViewMap<RecordPage, RecordModel, CaseRef>   back: - → Board
│  ├─ Tabs [Region.Attached, Navigator=Visibility, inline]: Preop | Drugs | Monitor | Recovery
│  ├─ Dose !      flyout · DataViewMap<DosePage, DoseModel, CaseRef>
│  └─ SignOff !   flyout · DataViewMap<SignOffPage, SignOffModel, CaseRef>
└─ Settings     ViewMap<SettingsPage, SettingsModel>   back: - → Board
```

### Page trees (key nodes)

```text
RecordPage @RecordPage.Resources
├─ NavigationBar (MainCommand ← back, Content ← Data.Patient band)  ⋯ Export (Signed only)
├─ FeedView(Case) {Value, Progress, None, Error}
│  └─ Grid
│     ├─ TabBar (Region.Name Preop/Drugs/Monitor/Recovery)
│     └─ Grid [Visibility region]
│        ├─ Monitor: Grid(cols *,*)
│        │  ├─ VitalsStrip (SKCanvasElement) ← Data.Readings, Data.Doses, Data.DueProgress   ≥40%
│        │  ├─ ReadingsTable (ListView, Table toggle) ← Data.Readings
│        │  └─ Pad ← Parent.Draft.* (TwoWay)  ⇒ IState Draft ; Record → RecordReading
│        ├─ Preop: Weight (Display) ← Data.Patient.WeightKg; Checklist ⇒ Preop; Induce
│        ├─ Drugs: Doses list ← Data.Doses; → !Dose
│        └─ Recovery: SinceExtubation (Display); ⇒ Recovery; → !SignOff
```

### Data-flow graph

```text
ICaseStore.Watch ─┬─► BoardModel.Today (IListFeed<CaseSummary>) ─► CaseList
                  ├─► BoardModel.Live = Combine(store active case, IClock.Ticks) ─► LiveBand
                  ├─► RecordModel.Case = Combine(store case(id), IClock.Ticks, Settings.Interval) ─► CaseView
                  └─► SignOffModel.Summary
RecordModel.Draft (IState) ⇐ Pad TwoWay (single writer: the pad; reset by RecordReading)
RecordReading ⇒ ICaseStore.Append(id, reading) ⇒ Watch ⇒ Case
DoseModel.Calc = Combine(Selected, MgPerKg, store weight) ; Give ⇒ ICaseStore.AddDose
SignOff.Sign ⇒ ICaseStore.Sign(id, vet) (store rejects later writes)
```

### Design graph

```text
App.xaml @App
├─ ColorPaletteOverride (generated)  ThemeResource roles
├─ VitalsPalette  ThemeDictionaries
│  ├─ VitalHrBrush    cue: ● dot          ← VitalsStrip snapshot, Pad HR label
│  ├─ VitalBpBrush    cue: ∨ ∧ chevrons   ← VitalsStrip, Pad SYS/DIA label
│  ├─ VitalSpO2Brush  cue: ○ ring
│  ├─ VitalEtCo2Brush cue: × cross
│  └─ VitalTempBrush  cue: ■ square
├─ Status.OutOfRange = ErrorBrush   cue: ▲/▼ glyph + "High"/"Low"
├─ Status.Due = PrimaryBrush        cue: "Due" text + filled slot
├─ Status.Overdue = ErrorBrush      cue: "Overdue m:ss" + icon
├─ Typography (generated) DisplayLarge ← Weight, Volume, Elapsed ; LabelLarge ← Pad labels
└─ MotionTokens  EaseSmooth/EaseOut, DurationFast/Normal/Slow ← entrance, confirm
```

### Spec gate

| Check | Result | Nodes | Resolution |
|---|---|---|---|
| Registration | PASS | all 6 pages mapped; 3 DataViewMaps name `CaseRef` | |
| Region host | PASS | root Frame; Record inline Visibility region names match TabBar items | |
| Shell safety | PASS | no Shell; no `Region.Attached` in splash | |
| Cross-page links | PASS | all links pass `CaseRef` | |
| Back stack | PASS | Record/Settings `-` → Board; NewCase → Record replaces the sheet | |
| Sources | PASS | every bound node traced in data-flow graph | |
| Feed vs state | PASS | | |
| Single writer | PASS | Draft: pad only, reset by command | |
| FeedView states | PASS | Dose/SignOff n/a rows justified | |
| Failure paths | PASS | store write failure → InfoBar error on the page with Retry | |
| Data shape | PASS | nullable vitals (not every monitor reads EtCO2/BP) | |
| Tokens | PASS | | |
| Theme-awareness | PASS | VitalsPalette in ThemeDictionaries; strip snapshot on theme change | |
| Scope | PASS | | |
| Color cue | PASS | every status role has a glyph/text cue | |
| Contrast | PASS | 24/24 roles; vitals series checked in build (risk R3) | |
| Encodings | PASS | strip y-scale: 0–220 fixed for HR/BP, 80–100 band for SpO2 in its own lane, clamp at edges with a ▲ marker | |
| Motion | PASS | | |
| Type assets | PENDING | fonts downloaded in build step 1; verify on desktop + Android | Risk R1 |
| UnoFeatures | PASS | + Skia | |
| Targets | PASS | | |
| Capabilities | PASS | 6 substituted/omitted rows repeated below | |

---

## Implementation Plan

Phase 2 runs in a session rooted in `C:\Users\Platform006\Vigil` (App MCP must load).

0. Confirm `uno_health` lists the App MCP tools. Start `docs/fleet/BUILD-LOG.md` (`uno-build-log`).
1. Foundation: wire generated Styles + fonts (download static TTFs, manifests), MotionTokens,
   VitalsPalette; Uno0001 as error; `UseStudio()` gate; Domain records + `JsonCaseStore` + formulary +
   clock + tests. Board page with Today/Live FeedViews and all states. Verify Board.
2. **Release path before screen 2:** private repo `mtmattei/Vigil`, CI (tests, desktop ×3, WASM,
   Android, iOS simulator with Xcode/workload pin), one Release publish per head locally where the host
   allows (desktop win-x64, WASM, Android APK). Log CI run ids.
3. NewCase sheet → Record page shell + tabs + Pre-op + Induce. Verify incl. bands after detail + Back.
4. Monitor: VitalsStrip (Skia), pad, RecordReading, due states, table toggle. Measure taps/seconds.
5. Drugs + Dose sheet (calc, out-of-range dialog).
6. Recovery + SignOff + read-only + Export.
7. Settings (theme, language, reduce motion, interval, sample day, clear all). EN/FR complete.
8. Persistence passes: desktop restart, WASM reload, Android relaunch.
9. Phase 3 gates in order (below).

## Quality Gates

Copied from *Build gates* in `~/.claude/rules/uno-scaffolding.md` (tiers for a new app).

| Phase | Gate (skill) | Done when |
|---|---|---|
| Every step | Build + tests; lint hook clean | 0 warnings on desktop; tests pass |
| Every UI step (1, 3–7) | `uno-verify` in the rooted session | Touched states exercised through the App MCP; bands checked after one detail navigation + Back |
| Steps 1, 3–6 (async surfaces) | `uno-component-states` | Loading/Empty/Error exist; Retry proven at runtime (`VIGIL_FAULT`) |
| Each feature step | spec section (this file); `uno-build-log` entry; `uno-completeness-audit` scoped to the feature | Feature graded by evidence; gaps listed |
| Step 2 (before screen 2) | Release path | CI green on every head; one Release publish per head |
| Feature-complete | `uno-break-it` → `gold-standard-pass` (every screen) → `uno-audit` | Findings fixed or listed in Unresolved Questions |
| Feature-complete | Atlas `atlas extract` route diff | Built route tree = spec route tree, or differences explained |
| Any done/shippable claim | Full `uno-completeness-audit` → `docs/COMPLETENESS.md`; `docs/DEPLOY.md` matrix | No such claim without the saved report |
| Session end | `handoff`; `uno-build-log` entry | `HANDOFF.md` current |

## Risks

- R1 Fonts: static TTF download (css2 Android UA), manifest weight selection on Skia + Android — spike 15 min in step 1.
- R2 `SKCanvasElement` on WASM and Android with `SkiaRenderer` — verify in the step 2 head smoke.
- R3 Vitals series contrast on both grounds (green/red/orange on `#F3F5F2` and `#161B1A`) — compute in step 4.
- R4 `FileSavePicker` on WASM/Android/iOS for Export — spike 15 min in step 6; fallback: copy summary to clipboard, recorded.
- R5 `LocalFolder` persistence on WASM across reload — verify in step 8.
- R6 Language hot swap — 15 min spike in step 7; fallback restart note.
- R7 iOS: no Mac on this host; CI simulator build is the only iOS evidence.

## Unresolved Questions

Decided by default for the sample; each is a product decision a real deployment would revisit.

- **Substituted:** vitals chart is a custom `SKCanvasElement` strip instead of LiveCharts2 (bespoke paper-record notation is the signature). Accept, or prototype LiveCharts2 custom geometries?
- **Omitted:** audible due reminder (visual only). Needs a media decision per head.
- **Substituted:** PMS handoff is CSV/text export only; no PMS integration (Covetrus/ezyVet/etc. APIs are partner-gated).
- **Omitted:** sync between tablets / central records server (single-device store).
- **Substituted:** sign-off is a typed name + confirmation on a shared device; no per-user authentication or cryptographic signature.
- **Omitted:** monitor integration (auto-capture of vitals from the multiparameter monitor).
- **Omitted:** patient photo.
- Formulary concentrations, mg/kg ranges and alarm thresholds are sample reference data, labelled in the app; a practice would supply its own.
- No real vet tech has timed the Monitor pad (VALIDATION.md next test).
- iOS is built in CI only; no device/simulator run from this host.
