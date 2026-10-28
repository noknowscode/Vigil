# SPEC v2: Clover redesign (Today dashboard, Patient care, shell)

Written 2026-10-28. Supersedes the Board and Record layouts in `SPEC.md`; everything else in `SPEC.md` (domain rules,
dosing, sign-off, storage) still holds. Exact visual values live in the value sheets, which are part of this spec:

- `docs/design/shell-and-tokens.md`: tokens, type ramp, shadows, buttons, sidebar, top bar, icons (Lucide paths)
- `docs/design/today-dashboard-values.md`: Today dashboard (prototype `Today · dashboard (C cards)`)
- `docs/design/patient-care-values.md`: Patient care (prototype `Patient care · Bella`)

Reference renders: `Downloads\Today · dashboard (C cards)-html\*@1x.png`, `Downloads\Patient care · Bella-html\*@1x.png`.
Goal: at 1440 px wide, light theme, sample day, each screen matches its render (parity ≥ 85 % by the uno-verify
compare, no unexplained deltas in type, spacing, radius or colour).

User decisions (2026-10-28):
1. Sidebar shows every item as designed; items without a screen are disabled (tooltip "Coming soon").
2. Data the design shows that Vigil lacked is added to the record model (OR room, fluids, dose schedule, tasks, labs,
   planned times, discharge call).
3. Waveforms are sparklines of recorded readings, not decorative ECG (Vigil is not a live monitor).
4. The new token set and fonts apply app-wide; dark theme is derived from the same roles.
5. Not a tablet app: no 56 px touch rule; size controls to the design.

## Architecture Brief

- **Targets**: unchanged (`net10.0-desktop` primary, android, ios, browserwasm). Uno.Sdk 6.7.30.
- **UnoFeatures**: unchanged (Material, Toolkit, Navigation, MVUX, Hosting, Localization, ThemeService, Skia).
- **Presentation**: MVUX stays. Decision: one `ShellModel` for sidebar state, page models unchanged in kind.
  Reason: project convention, all data is async feeds. Tradeoff: sidebar counts need their own feeds over `ICaseStore`.
- **Shell and regions**: new `ShellPage` (route `Main`, default) = sidebar column + content `Frame`
  (`uen:Region.Attached`). Sidebar items call `ShellModel.Go(route)` which navigates
  `./-/<route>` with `Qualifiers.Nested + ClearBackStack` (FrameNavigator revisit gotcha; no `Region.Name` on items).
  Detail pages (Record, Dose, SignOff, ParentPreview, NewCase `!`) navigate inside the content frame, so the sidebar
  stays. The agenda drawer and the board's AgendaDock region are removed.
- **Routes** (see Spec Graph): Main{Today*, Patients, Agenda, Anesthesia→Record of live case, Record, Dose, SignOff,
  ParentPreview, Settings, NewCase!}. Records, Messages, Reports, Help center: no route (disabled items).
- **Domain additions** (`Domain/Records.cs`, all optional with defaults so existing JSON loads):
  - `Case.OperatingRoom` (string, "" = none) and `Case.PlannedStart` (DateTimeOffset?), `Case.PlannedMinutes` (int?).
  - `Case.PrepStartedAt` (DateTimeOffset?): Booked → Prep. `Case.DischargedAt` (DateTimeOffset?): Home stage.
  - `Case.Fluids` (`FluidRun(string Name, decimal RateMlKgH, DateTimeOffset StartedAt, DateTimeOffset? StoppedAt)` list).
  - `Case.Labs` (`LabResult(string Panel, LabState State, DateTimeOffset At)` list; `LabState { Pending, Ready, Reviewed }`).
  - `RecoveryLog.PainScores` (list of `PainScore(DateTimeOffset At, int Score)`; the single `PainScore` stays for export).
  - `Case.DischargeCall` (`DischargeCall(DateTimeOffset DueAt, DateTimeOffset? DoneAt)`?).
  - `Case.FastingSince` and `Case.ParentNotifiedAt` (DateTimeOffset?) for the next-surgery hero state.
  - Formulary: `RedoseMinutes` per drug (cefazolin 90) → next dose = last dose + RedoseMinutes.
  - Practice preference `PlannedPrepMinutes` (default 25) for the care-time insight.
- **Derived logic (pure, unit-tested)**: `Stage.Of(case)` Booked/Prep/Surgery/Recovery/Home;
  `CarePhases.Of(case, now)` → spans {Waiting, Prep, Anesthesia, Recovery, Planned} for the gantt and the care-time
  totals; `DueTasks.Of(cases, now, prefs)` → rows {Record observations, Pain score (every 15 min in recovery),
  Redose, Bloodwork review (Ready), Discharge call}; `CareInsight.Of(...)` (prep average vs planned).
- **Errors**: unchanged; feeds fail into FeedView Error templates with Retry via `Store.Reload()`.
- **Testing**: unit tests for every derived rule above and JSON back-compat (old file loads); App MCP proof per screen
  at 1440 light, then dark, 1100, 760, 393, French.
- **Capability inventory**

| Capability (design) | Status | Note |
|---|---|---|
| Sidebar: Today, Patients, Agenda, Anesthesia, Settings | implemented | routes in the shell frame |
| Sidebar: Records, Messages, Reports, Help center | substituted | disabled items, "Coming soon" (user decision 1) |
| Clinic switcher, search ⌘K, notification bell | substituted | disabled controls (decision 1) |
| "Monitoring on · 1 min ago" | substituted | disabled styling; text "Monitoring off" would contradict design, see UQ |
| User card | implemented | Settings surname + role "Veterinarian" |
| Collapse sidebar | implemented | icon rail 72 px (also the < 1080 layout) |
| Greeting + recap | implemented | `BoardGreeting` (exists), copy aligned to design |
| Live case hero, timer, overdue chip, stats | implemented | existing feeds |
| Care gantt (waiting/prep/anesthesia/recovery/planned) | implemented | new `CarePhases` + new fields |
| Patient cards with stage stepper, show details | implemented | `Stage.Of`; details = existing summary fields |
| Due next table with actions | implemented | `DueTasks` + new fields; actions navigate to the matching screen |
| Where care time went + insight | implemented | `CarePhases` totals + `PlannedPrepMinutes` |
| ECG/pulse graphics | substituted | recorded-HR sparkline (decision 3); pulse dot kept only where a case is live |
| Patient hero nav (Preparation…Medications) | implemented | existing panes |
| Anesthesia record chart, circulation, respiration | implemented | restyled renderer + sparklines from readings |
| Latest medication, next dose, fluids running | implemented | dose log + `RedoseMinutes` + `Fluids` |
| OR room, procedure start | implemented | new field |

## Design Brief

- **Direction**: the prototypes, literally. Neutral warm-grey ground, one green primary, phase colours, Newsreader
  titles, Martian Mono for every number. Values: `docs/design/shell-and-tokens.md` §1-6.
- **Tokens** replace Clover's: `Styles/CloverPalette.xaml` becomes the Material role mapping
  (Primary #244936, Background/page #F3F3F2, Surface #FCFBF9, OnSurface #1B212F, OnSurfaceVariant #5C616C,
  Outline #E4E4E6, Error #B3261E) plus app tokens in `Themes/Tokens.xaml` (ThemeDictionaries Light/Dark):
  Ink, InkMuted, IconMuted, InkFaint, Page, Surface, Surface2, ChipNeutral, Track, Divider, BorderTop/Bottom,
  Phase.{Prep,Anesthesia,Recovery,Waiting,Error}.{Solid,Deep,Soft,SoftBorder,Step}, HeroGradient, hatch brushes.
  Every colour above is ThemeResource; dark values derived (Ink #E6E7EA, Page #121417, Surface #1A1D21, …; see UQ).
- **Type ramp** (§1 table) as `TextBlock` styles named by role (`Type.DisplayTitle`, `Type.MonoHero`, `Type.Body`…).
  Fonts in `Assets/Fonts`, static per weight, underscore names: `HankenGrotesk_Regular/Medium/SemiBold.ttf`,
  `MartianMono_Medium.ttf`, `Newsreader_Regular.ttf` (OFL). Material font keys point at Hanken Grotesk.
  `CharacterSpacing` is a no-op on Skia TextBlock (gotcha): negative tracking on the mono hero numbers is dropped and
  listed in UQ.
- **Radii/shadows/spacing**: §3-5 as resources; outer shadows via Toolkit `ShadowContainer` (solid backgrounds only),
  inset shadows `IsInner`.
- **Scopes**: tokens, type, buttons, chips in App.xaml; page-only templates (patient card, due row, gantt) in the
  page's Resources.
- **Layout**: frame 12 px margin, radius 22, padding 36/40/40/40, sidebar 256. Responsive (`ShellLayout`):
  ≥ 1080 full sidebar; 760-1080 icon rail 72; < 760 rail hidden, menu button opens the sidebar as a `!` drawer route.
  Today cards 3/2/1 columns at 1080/760; hero and bottom split stack below 1080.

## Interaction Brief

- **Flows**: Today → card / Record button / Due row → Record (data CaseRef) → `-` back to Today. Sidebar items clear
  the frame stack. Breadcrumb "Patients" → Patients. Anesthesia → Record of the live case (disabled when none).
- **FeedView states**: every async block (hero, gantt, cards, due, care time, record panes) has Progress (skeleton),
  None (empty copy), Error (message + Retry → `Store.Reload`).
- **State designs** (user reference image, 2026-10-28, chat only; values read from it):
  - *No surgery in progress*: the hero card becomes "Next surgery · Oscar" (calendar icon tile, "Mass removal, left
    flank · OR 1"), secondary button "Start prep" (sets `PrepStartedAt`), countdown "1:18 until 14:00" (mono hero),
    green chip "✓ Bloodwork ready" (from Labs) + "Fasting since 06:00", stats Prep starts 13:30 (in 48 min) · Team
    Dr. Okafor / S. Tremblay · Pet parent L. Moreau (notified 12:30). Needs new fields `FastingSince` and
    `ParentNotifiedAt`.
  - *Monitor signal lost* (patient card): status strip turns error-soft (#FBE6E2/#F1C9C2), "⚠ Signal lost at 12:39" in
    error ink, clock keeps running. Needs a monitor connection (see UQ).
  - *Loading*: skeleton with the collapsed card's footprint (photo block, two text bars, strip block, stepper bars,
    toggle bar) in #ECECEE-family greys.
  - *Nothing due*: the table is replaced by a check tile (green soft) "All caught up" + "Next: Oscar prep at 13:30";
    header copy "Nothing due before 14:00", the Open agenda link stays.
  - *Monitoring paused* (sidebar): error-soft pill, "⚠ Monitoring paused", "Last sync 12:39 · Retry" (link).
  - *No patients today*: section card "Today's patients" / "No one is booked in for today yet." with buttons
    "New patient +" and "Open agenda".
- **Motion** (§6): buttons press 0.96/150 ms; show-details expand 300/collapse 200 ms ease (0.2,0,0,1); chevron
  rotate; pulse ring 2 s; reduced motion (Preferences) disables all.
- **Accessibility**: composed AutomationProperties.Name on cards, rows and stats; disabled items announce
  "Coming soon"; status never colour-only (labels + icons as designed); focus outline 2 px ink.
- **Verification**: uno-verify per phase; screenshot compare against the PNGs at 1440x1380 / 1440x1084.

## Spec Graph Brief

```text
Main  ShellPage/ShellModel  [sidebar + Frame Region.Attached]  IsDefault
├─ Today        ViewMap<TodayPage, TodayModel>          IsDefault
│  ├─ Hero      ← Live, CarePhases(gantt)               {Value, Progress, None, Error}
│  ├─ Cards     ← Cards (IListFeed<PatientCard>), Filter (IState)   → Record(CaseRef)
│  ├─ DueNext   ← DueTasks                                → Record/Dose/SignOff(CaseRef)
│  └─ CareTime  ← CareTotals, Insight
├─ Patients     ViewMap<PatientsPage, PatientsModel>      → Record(CaseRef)
├─ Agenda       ViewMap<AgendaPage, AgendaModel>
├─ Record       DataViewMap<RecordPage, RecordModel, CaseRef>   breadcrumb → Patients, - back
│  ├─ ParentPreview  DataViewMap (existing)
│  ├─ Dose           DataViewMap (existing)
│  └─ SignOff        DataViewMap (existing)
├─ Settings     ViewMap<SettingsPage, SettingsModel>
└─ NewCase !    flyout (existing ResultData CaseRef)
ShellModel: Counts(IFeed: patients, live, messages=n/a), Clinician(IFeed), Go(route) command
```

Gate (initial): Registration PASS (all pages mapped); Region host PASS (single Frame); Single writer PASS
(Filter only by chips); FeedView states PENDING (built per phase); Tokens PENDING (Phase 1);
Color cue PASS (labels + icons); Type assets PENDING (download + verify); Capabilities PASS (inventory above,
substitutions in UQ).

## Implementation Plan

1. **Tokens and fonts, app-wide**: fonts in Assets/Fonts, Tokens.xaml (Light/Dark), type ramp, buttons/chips,
   palette roles; remove Clover tokens (keep CareMark logo, updated to #244936). Existing screens restyle through tokens.
   Remove AgendaDrawer and the AgendaDock. Build, test, lint.
2. **Domain**: new fields with defaults, `Stage`, `CarePhases`, `DueTasks`, `CareInsight`, formulary redose, sample day
   extended to match the prototype (Bella OR 1, fluids, Milo pain score, Oscar bloodwork Ready, Pepper discharge call,
   planned slots). Unit tests + JSON back-compat test.
3. **Shell**: ShellPage, sidebar (disabled items), top bar cluster, routes moved under Main, responsive rail.
4. **Today**: hero (live card + gantt), patient cards (status strip, stepper, details), due next, care time.
5. **Patient care**: header/breadcrumb, hero tray (nav, photo, weight, anesthesia card), alert, procedure header,
   anesthesia record chart, medication, circulation, respiration.
6. **Polish**: dark theme, responsive bands, French strings, lint, parity compare, handoff.

## Quality Gates

| Phase | Gate (skill) | Done when |
|---|---|---|
| every phase | build + tests + lint hook | 0 warnings desktop, tests pass, no new gating lint |
| 2 | unit tests for rules | Stage/CarePhases/DueTasks/CareInsight/back-compat tests fail when the rule breaks |
| 3-5 | uno-verify | each screen exercised via App MCP, screenshot vs PNG |
| 4-5 | uno-component-states | Progress/None/Error per async block, Retry proven once |
| 6 | uno-completeness-audit (scoped) | Today + Patient care graded; gaps listed |

## Risks

- Fonts: Google css2 static TTF download (Android 2.2 UA trick, verify `name` table, no `fvar`) — 10 min.
- Negative letter-spacing unavailable on Skia (CharacterSpacing no-op) — accepted; hero numbers render slightly wider.
- Tri-tone borders: XAML Border has one brush — use a vertical gradient border brush; verify on Skia — 10 min.
- Repeating hatch brushes (`SpreadMethod=Repeat`, Absolute) — known to work on Skia, edges not anti-aliased.
- Blurred photo backdrop (Bella panel, `blur(28px)`): Skia-native AcrylicBrush or a pre-blurred asset — 15 min.
- ShellPage + Frame nested routing with existing DataViewMap pages — 15 min spike in Phase 3.

## Unresolved Questions

- Disabled-but-visible Records, Messages, Reports, Help center, clinic switcher, search and bell show features the
  app does not have (user chose this; capability rule flags it). Revisit before any release.
- Monitoring (default applied 2026-10-28, user did not choose): no monitor connection is built. The "Monitoring on"
  pill renders disabled like the other screen-less items; the "Monitor signal lost" card state and the "Monitoring
  paused" pill are omitted. Revisit if a monitor integration (device/protocol) is wanted.
- Dark-theme values are derived, not designed.
- Negative tracking on mono numbers is dropped on Skia.
- Phone layout (< 760) is not in the prototypes; the drawer-sidebar behaviour is our design.
