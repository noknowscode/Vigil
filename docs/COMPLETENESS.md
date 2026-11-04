# Completeness audit: Vigil (2026-11-04, Uno.Sdk 7.0.0-dev.115, heads: desktop, android, ios, wasm, windows)

Audited at f376607 (`feat/production-readiness`). Report mode, no code changed. Sources: SPEC.md, SPEC-v2.md,
HANDOFF.md, docs/DECISIONS.md, docs/design/*, the code-review notes of 2026-11-01, and the previous audit (2026-10-28,
Uno 6.7.30), whose findings were re-checked here rather than carried over. Runtime: Debug `net10.0-desktop` on Linux
under Xvfb (`VIGIL_X11_RENDERER=software`), driven with xdotool and the Debug hooks (`--vigil-seed`, `--vigil-now`,
`--vigil-theme`, `--vigil-route`, `VIGIL_FAULT`). Every write was checked in the case JSON files and the preferences
file, and after a relaunch. Evidence grades: R runtime, C code, I inferred, X not checked.

Fix pass 2026-11-04 (7741ede, 69d1a6f): both Blockers fixed and proven at runtime; see Findings > Fixed. The rest of
this report is the audit at f376607, with the lines the fixes changed updated.

Verdict: **Complete with gaps** (0 Blockers, 2 functional Majors, 4 head Majors, 17 Minors)

Safe to show (desktop, R): Today, Patients, Agenda, Settings; open a record from the hero, a card, Due next and the
Agenda; New patient with its schedule fields; pre-op checks, ASA and Induce; record observations; administer a dose,
including the out-of-range confirmation; end anesthesia, recovery, sign-off and export of a case created in the app
(fix pass); theme, names and interval surviving a relaunch; error and Retry; the empty day.
Keep off screen: CSV export on a Linux box without a desktop portal; every head except desktop.

## Inventory

Screens: 11 intended, 11 present, 0 unreachable as pages, 0 missing (source: SPEC-v2 Spec Graph). Shell, Today, Patients,
Agenda, Record (panes Preop, Monitor, Dose, Drugs, Recovery, History, Labs), ParentPreview, Dose, SignOff, Settings,
NewCase `!`, Menu `!` (below 760). The Recovery pane opens from End anesthesia in the procedure header while a case is
under anesthesia, and from the stage card's button in recovery and once signed (fix pass).
Records, Messages, Reports and Help center have no screen by decision (SPEC-v2 decision 1, D6).
Routes: 11 registered (Main + 10 nested), every one with a trigger; 0 dead references.
Interactions: 98 XAML lines with `Command=` / `Navigation.Request`; 0 dead by construction; 11 disabled "Coming soon"
controls (D6); 0 that show a state the app does not have (the pill and the bell's dot were fixed in the fix pass).
Critical workflows (derived from SPEC.md flows 1-6 and SPEC-v2):
1. Open a record (hero, card, Due next, Agenda, Patients, sidebar Anesthesia) and Back
2. New patient to record
3. Pre-op checks, ASA and Induce
4. Record observations
5. Administer a dose (calculation, out-of-range confirmation)
6. End anesthesia, recovery, sign off, export
7. Settings, then relaunch with everything kept

## Completeness by area

| # | Area | Status | Evidence | Blockers | Majors | Minors | Note |
|---|---|---|---|---|---|---|---|
| 1 | Screens | Complete | R | 0 | 0 | 0 | 11/11 present; Recovery pane reachable in every stage (fix pass) |
| 2 | Navigation | Partial | R | 0 | 0 | 2 | End anesthesia reachable (fix pass); sidebar highlights two items after Anesthesia |
| 3 | Interactions | Partial | R | 0 | 1 | 0 | 11 "Coming soon" controls (D6); the pill and the bell no longer claim a monitor or alerts (fix pass) |
| 4 | Logic | Complete | R | 0 | 0 | 1 | Dose math, range check, validation proven; chart caption ignores the interval |
| 5 | Data | Partial | R | 0 | 1 | 1 | Writes persist across a relaunch; no case can be edited after creation |
| 6 | States | Complete | R | 0 | 0 | 0 | Error + Retry and empty day proven; loading skeleton not captured |
| 7 | Forms | Partial | R | 0 | 0 | 3 | Validation works; errors sit in a footer; an empty reading closes its pane; "0 years ·" |
| 8 | Authentication | N/A (decision) | C | 0 | 0 | 0 | Shared clinic device, typed-name sign-off (SPEC.md) |
| 9 | Responsive/adaptive UI | Partial | R | 0 | 0 | 2 | 390 px: record hero clips, menu drawer opens 107 px down |
| 10 | Platform behavior | Partial | R | 0 | 4 | 1 | Export picker fails on Linux and reads "Export cancelled."; 4 heads never run |
| 11 | Failure handling | Partial | R | 0 | 0 | 2 | Store faults recover through Retry; no crash handler; preference saves unguarded |
| 12 | Accessibility | Partial | C | 0 | 0 | 1 | Names on controls; disabled items keep the enabled look |
| 13 | Performance | Partial | I | 0 | 0 | 1 | 14.7 s Release startup (2026-10-28 run), not re-measured |
| 14 | Security/privacy | Complete | C | 0 | 0 | 0 | No network, no secrets; fault, seed and route hooks are `#if DEBUG` |
| 15 | Visual quality | Partial | R | 0 | 0 | 2 | Hard-coded clinic; mixed "Anaesthesia"/"Anesthesia" |
| 16 | Testing | Partial | R | 0 | 0 | 1 | 156/156 pass; no UI suite, so the unreachable End anesthesia passed |

## Critical workflows by head

| Workflow | desktop | android | ios | wasm | windows |
|---|---|---|---|---|---|
| 1. Open a record + Back | R (hero Record, Due next Record → Knuckles on the Monitor pane, Agenda → Ziggy, sidebar Anesthesia, Back → Today) | X (CI build only) | X (CI build only) | X (not run) | X (CI build only) |
| 2. New patient to record | R (empty Create: 5 errors, no file; Ziggy saved with OR 2 and 14:30, record opened, Patients 4 → 5) | X | X | X | X |
| 3. Pre-op + Induce | R (5 checks + ASA I in the store; Induce → Status Anesthetized 12:48:26, Anesthesia 1 → 2) | X | X | X | X |
| 4. Record observations | R (HR stepped to 98, readings 13 → 14, overdue banner cleared, next 4:56) | X | X | X | X |
| 5. Administer a dose | R (Meloxicam 0.2 mg/kg × 18.4 kg ÷ 5 = 0.74 mL SC stored; 2 mg/kg raised the confirmation; Change dose wrote nothing) | X | X | X | X |
| 6. End anesthesia, recovery, sign off, export | R, fix pass (Ziggy created in the app, induced, 2 readings; header End anesthesia → Recovery pane → End anesthesia: Status Recovery, EndedAt 03:41:03; extubated, sternal, pain 1, 37.8 °C, notes; Sign record → Signed by Dr. Okafor; relaunch, Review record → Export CSV wrote the CSV through a portal stand-in) | X | X | X | X |
| 7. Settings + relaunch | R (first name, surname, 3 min, Dark in preferences.json; relaunch shows Dark, "Dr. Okafor", AO, 3-minute strip, Ziggy and the signed Milo) | X | X | X | X |

## Findings

### Fixed (fix pass, 2026-11-04)
- [Navigation] No control ended anesthesia (was Blocker 1). Fix 7741ede: the procedure header shows End anesthesia
  (secondary-l, the design's button) ahead of Pre-op and Administer dose while the case is under anesthesia; it opens the
  Recovery pane, where the existing button ends anesthesia under its "vaporizer off" note (`RecordPage.xaml:219-239`).
  Below 600 px it sits above the pair. Chosen over the two audit candidates because both break the measured hero tray at
  1440: a sixth RecordNav item needs 42 px the fixed 434 px identity column does not have, and a second stage-card
  button leaves the status card about 3 px short of its content while a reading is overdue (measured on the 1440 render). The same commit keeps the
  stage card's button enabled on a signed record: it read "Review record" but was bound to `CanChange`, so once the pane
  was closed or the app relaunched, Export CSV was out of reach. No unit test: the bug was a missing control in XAML
  (the model's `OpenPane`/`EndAnesthesia` already worked), so the proof is runtime - R (Debug desktop, Xvfb, xdotool,
  1440x1000, empty store: New patient Ziggy 12.6 kg → file Scheduled; 5 checks + ASA I → `Preop` all true, Asa 1;
  Induce → Anesthetized, InducedAt 03:38:21; HR 92 / RR 12 / SpO₂ 98 / 110/62 recorded → 1 reading, MAP 78 calculated
  (a second reading later); header End anesthesia → Recovery & handover pane; End anesthesia → Recovery, EndedAt
  03:41:03; Extubated now, Sternal now, pain 1, 37.8 °C, notes → `Recovery` in the file; Sign off, confirm, Sign record →
  Signed by Dr. Okafor, SignedAt 03:42:18; relaunch: Today "Every record from today is signed", Ziggy's gantt row, Signed
  out 1; Record → Review record → Export CSV → "Saved Ziggy-2026-11-04-anesthesia." and a 262-byte CSV with both
  readings, written by the app through a minimal FileChooser portal stand-in on a private session bus. Header layout seen
  at 1440, 700 (one row, dark) and 390 (End anesthesia above the pair); a booked or signed record keeps Pre-op and
  Administer dose where they were).
- [Interactions] "Monitoring on" and the bell's unread dot stated things the app does not do (was Blocker 2). Fix
  69d1a6f, within D6 (both stay disabled "Coming soon" items): the pill reads "No monitor connected" (French "Aucun
  moniteur connecté") in muted ink with a hollow grey ring, which is also all the icon rail shows, and its automation
  name says the same; the bell has no unread dot, since nothing can be unread without an alert feed. XAML only, so no
  unit test - R (light and dark at 1440 with the empty store and with `VIGIL_FAULT=store-read-x6`, dark with the sample
  day at 900 px (rail): the pill shows the grey ring and the muted text, never green or "on"; no dot on the bell).
- Seen during the fix pass, not graded: the record nav still has no Recovery item, so with the Recovery pane open it
  keeps the previous item highlighted (the toolkit logs "failed to transition to the 'Recovery' state"; listed below as a
  Minor); the export's suggested name carries no `.csv`, so a portal that keeps the name saves the file without the
  extension; once, the first press of the header End anesthesia right after recording a reading did not open the pane
  (the second did; not reproduced in three further tries).

#### As found at f376607
- [Navigation] No control ends anesthesia. "End anesthesia" lives in the Recovery pane (`RecordPage.xaml:711`), and nothing
  opens that pane while a case is under anesthesia: RecordNav has five items and no Recovery (`Controls/RecordNav.xaml:55-108`),
  the stage button opens Monitor (`RecordViews.cs:231`), and Due next opens Recovery only for pain-score and discharge-call
  tasks (`TodayViews.cs:439`). A case started in the app can never reach recovery, sign-off or export; only a case seeded
  in recovery can. The v2 rebuild (4a7b33a) removed the old "Recovery & handover" card. The model test calls
  `EndAnesthesia` directly, so the suite passes - R (Ziggy induced: Vitals, Preparation, Medications and the stage button
  checked at 1440x1380, none offers it; the Debug hook `--vigil-route=Record:Recovery` shows the button, and pressing it
  set Status Recovery, EndedAt 12:53:27) - owner: uno-navigation, mvux.
- [Interactions] The sidebar says "Monitoring on" next to a green dot (`Controls/Sidebar.xaml:199-213`), and the bell
  carries a red unread dot (`Controls/TopBarCluster.xaml:31-35`). Vigil has no monitor link (SPEC.md: monitor integration
  omitted) and no alert feed. Both stayed on with an empty store and while every store read failed. In an anesthesia
  record, "Monitoring on" is a safety claim. D6 keeps the pill as a disabled item, but it is a status rather than a control,
  and it renders in full ink - R (captures with the sample day, the empty day and `VIGIL_FAULT=store-read-x6`) - owner:
  gold-standard-pass for wording; decision for the owner (see Unresolved Questions).

### Blockers
None open.

### Majors (functional)
- [Data] A case cannot be edited once created. No pane edits the patient, weight, procedure, team or schedule. Delete is
  offered only while the case is booked (`RecordPage.xaml:572-575`). A weight mistyped on a case that is already under
  anesthesia drives every dose and cannot be corrected - R (no edit control on any pane of Knuckles or Ziggy) / C - owner:
  mvux, uno-toolkit.
- [Interactions] 11 controls advertise features the app lacks: the clinic switcher, Records, Messages, Reports, Help center,
  the account menu, the Monitoring pill, search, the bell, the care-time report button, and "See the prep checklist". The
  last one points at a checklist that exists (Record > Preparation) (`BoardPage.xaml:697`). This is the recorded exception
  D6, so it is graded as one Major, not eleven - C/R (rendered and inert at 1440 and 390) - owner: decision for the owner.

### Majors (head)
- [Platform] android: no runtime run of any workflow (CI builds only) - X.
- [Platform] ios: no runtime run (CI simulator build only) - X.
- [Platform] wasm: no runtime run this audit (CI publishes to GitHub Pages) - X.
- [Platform] windows (WinAppSDK): no runtime run (CI build check only) - X.

### Minors
- [Platform] The CSV save picker fails on Linux desktop (Uno `LinuxFileSaverExtension`: "Can not determine DBus session
  bus address"), and the app reports "Export cancelled." (`RecordExporter.cs:31-34`). A failed picker cannot be told from
  a cancel - R.
- [Forms] Recording an empty reading shows "Not saved: Enter at least one vital sign" and closes the Record observations
  pane, so the user has to reopen it - R.
- [Forms] New patient lists its errors in a footer above Create, not at the fields - R.
- [Forms] A patient without an age or owner reads "0 years · " (`CaseView.cs:135`) - C.
- [Logic] The record chart caption says "5-minute strip" (`RecordPage.xaml:250`) with the interval set to 3 minutes. The
  banner on the same screen says "every 3 minutes", and Today's hero says "3-minute strip" - R.
- [Data] Copy summary shows "Summary copied"; the clipboard contents were not read (no reader in the container) - R/X.
- [Navigation] While the Recovery pane is open, RecordNav highlights Preparation - R.
- [Navigation] After sidebar Anesthesia, both Patients and Anesthesia stay highlighted - R.
- [Responsive] At 390 px the record's hero tray is cut off at its right edge (the photo and the nav highlight end at
  x=348 with no border) - R.
- [Responsive] At 390 px the menu drawer opens 107 px below the top, with no scrim, and shows the desktop
  collapse-sidebar button - R.
- [Failure handling] No `UnhandledException` / `UnobservedTaskException` handler anywhere; `async void OnLaunched` is
  unguarded (`App.xaml.cs:21`) - C.
- [Failure handling] A preference is set in memory before an unguarded file write; callers never catch it
  (`Preferences.cs:94-104`) - C.
- [Accessibility] Disabled controls keep the enabled look (`Themes/Tokens.xaml:432`): Records, Messages and Reports read as
  active in both themes - R.
- [Performance] Startup to a settled Today took 14.7 s in the 2026-10-28 Release timing run (HANDOFF), against a 2 s
  target. Not re-measured here: Debug navigations settled within the 6-8 s capture waits - I.
- [Visual] The clinic identity "Clinique Plateau · 2 suites · Montréal" is hard-coded in the sidebar
  (`Sidebar.xaml:47-48`) - R.
- [Visual] "Anaesthesia" (Agenda, gantt: `CareDay.cs:9`, `AgendaPage.xaml:42`) and "Anesthesia" are mixed - R.
- [Testing] No automated UI suite. Workflow proof is this audit's log; 156/156 unit tests pass (`dotnet test`, 2 s) - R.

### Re-checked from the 2026-10-28 audit
- Fixed (R): New patient now captures the OR room and planned start (Ziggy: OR 2, 14:30 stored). The dose header no
  longer overlaps its value. Debug navigations settled within the 6-8 s capture waits (was 10-35 s), not timed finer.
- Fixed (C): Fluids start, change rate and stop on the Medications pane (seen on Knuckles, not driven).
- Still open: "0 years ·" residue; disabled controls look enabled; no UI suite; heads other than desktop not run.

## Not checked (X)
- CSV file write: no DBus portal in the container. Check: run on Windows or a Linux desktop session, export Milo, open the file.
- Loading skeletons (`VIGIL_SLOW_MS`, `record-slow`): not captured. Check: launch with `VIGIL_FAULT=record-slow`.
- "Next surgery" hero, Start prep and Due next Prepare/Review/Call: not driven. Check: `--vigil-seed=next`.
- Pet-parent view, Labs "Mark reviewed", fluids start/stop, Delete case confirm: present, not driven.
- android, ios, wasm, windows: every workflow. Check: emulator, simulator, browser (Playwright on the Pages build), Windows machine.
- French strings and reduce motion: not run.

## Noted for other skills
- uno-audit: `RecordPage.xaml` holds seven panes in one template, and `RecordModel` carries every pane's commands;
  splitting them per pane would have made the missing Recovery entry visible.
- gold-standard-pass: pill wording, the mixed spelling, the 390 px record hero, and the drawer placement.

## Unresolved Questions
- Resolved in the fix pass: "Monitoring on" and the bell's dot were reworded and dropped within D6 (the pill reads "No
  monitor connected", disabled; no dot). SPEC-v2's capability table still notes that "Monitoring off" would contradict
  the design. Original question: hide them, or reword (for example "No monitor connected") and drop the dot? D6
  covers disabled entry points. These two state facts the app does not have, and that is what made them Blockers.
- D6 "Coming soon" controls (11): kept by the owner and graded as one Major. Revisit before any release.
- Resolved in the fix pass: End anesthesia sits in the procedure header while the case is under anesthesia (see Fixed).
  Original question: where should it live in v2? Candidates: a sixth RecordNav item "Recovery", or a second button in
  the stage card while the case is under anesthesia.
- Case correction: should weight and patient details be editable until sign-off, with the edit logged?
- Authentication N/A: shared clinic device, typed-name sign-off (SPEC.md). Confirm.
- Derived inventory: workflows 6 and 7 are assembled from SPEC.md flows 5-6. Confirm that ending anesthesia belongs in
  the critical set.
- android, ios, wasm and windows have no runtime evidence. Which heads must be proven before Vigil is called done?
- Formulary and alarm thresholds are sample reference data, labelled "not for clinical use", and a practice cannot load
  its own (SPEC.md). Confirm that this stays out of scope.
