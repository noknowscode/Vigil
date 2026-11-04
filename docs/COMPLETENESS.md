# Completeness audit: Vigil (2026-11-04, Uno.Sdk 7.0.0-dev.115, heads: desktop, android, ios, wasm, windows)

Audited at f376607 (`feat/production-readiness`). Report mode, no code changed. Sources: SPEC.md, SPEC-v2.md,
HANDOFF.md, docs/DECISIONS.md, docs/design/*, the code-review notes of 2026-11-01, and the previous audit (2026-10-28,
Uno 6.7.30), whose findings were re-checked here rather than carried over. Runtime: Debug `net10.0-desktop` on Linux
under Xvfb (`VIGIL_X11_RENDERER=software`), driven with xdotool and the Debug hooks (`--vigil-seed`, `--vigil-now`,
`--vigil-theme`, `--vigil-route`, `VIGIL_FAULT`). Every write was checked in the case JSON files and the preferences
file, and after a relaunch. Evidence grades: R runtime, C code, I inferred, X not checked.

Fix pass 2026-11-04 (7741ede, 69d1a6f): both Blockers fixed and proven at runtime; see Findings > Fixed. The rest of
this report is the audit at f376607, with the lines the fixes changed updated.

A pass 2026-11-04 (f50c14b to the pass's last commit): both functional Majors closed (case details are correctable,
D9; the "Coming soon" controls are cut, D7), the record's panes became drawers (D8), eight Minors fixed, and the
WebAssembly head run in headless Chromium. See Findings > Fixed (A pass). Counts below are after the A pass.

Verdict: **Complete with gaps** (0 Blockers, 0 functional Majors, 2 head Majors, 10 Minors)

Safe to show (desktop, R): Today, Patients, Agenda, Settings; open a record from the hero, a card, Due next and the
Agenda; New patient with its schedule fields; pre-op checks, ASA and Induce; record observations; administer a dose,
including the out-of-range confirmation; end anesthesia, recovery, sign-off and export of a case created in the app
(fix pass); theme, names and interval surviving a relaunch; error and Retry; the empty day.
Since the A pass (R): correct a case's weight and details and see the dose follow; every record pane as a drawer
(open, hand over to another pane or sheet, Escape, focus back); the prep checklist link; the same open-record and
record-a-reading workflow in Chromium.
Keep off screen: CSV export on a Linux box without a desktop portal; the iOS and WinAppSDK heads (CI builds only).

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
| 3 | Interactions | Complete | R | 0 | 0 | 0 | A pass: the 11 "Coming soon" controls are cut (D7), "See the prep checklist" wired; "No monitor connected" is a status |
| 4 | Logic | Complete | R | 0 | 0 | 0 | Dose math, range check, validation proven; the dose follows a corrected weight (A pass); chart caption fixed |
| 5 | Data | Partial | R | 0 | 0 | 1 | Writes persist across a relaunch; case details correctable until sign-off, each correction logged (A pass, D9) |
| 6 | States | Complete | R | 0 | 0 | 0 | Error + Retry and empty day proven; loading skeleton not captured |
| 7 | Forms | Partial | R | 0 | 0 | 1 | Validation works; errors sit in a footer (an empty reading and "0 years ·" fixed in the A pass) |
| 8 | Authentication | N/A (decision) | C | 0 | 0 | 0 | Shared clinic device, typed-name sign-off (SPEC.md) |
| 9 | Responsive/adaptive UI | Partial | R | 0 | 0 | 2 | 390 px: record hero clips, menu drawer opens 107 px down |
| 10 | Platform behavior | Partial | R | 0 | 2 | 1 | Export picker fails on Linux; WebAssembly run in Chromium and Android in the CI emulator (A pass); iOS and WinAppSDK CI builds only |
| 11 | Failure handling | Complete | R | 0 | 0 | 0 | Store faults recover through Retry, also in a drawer; crash handler and guarded preference saves (uno-audit) |
| 12 | Accessibility | Partial | R | 0 | 0 | 1 | Names on controls (25.7 per 1,000 XAML lines); disabled buttons fade (A pass); keyboard walk with the focus log; Today's Tab order |
| 13 | Performance | Partial | I | 0 | 0 | 1 | 14.7 s Release startup (2026-10-28 run), not re-measured |
| 14 | Security/privacy | Complete | C | 0 | 0 | 0 | No network, no secrets; fault, seed and route hooks are `#if DEBUG` |
| 15 | Visual quality | Partial | R | 0 | 0 | 1 | Mixed "Anaesthesia"/"Anesthesia" (the hard-coded clinic went with the switcher) |
| 16 | Testing | Partial | R | 0 | 0 | 1 | 223/223 pass, every page and drawer model tested; no UI suite |

## Critical workflows by head

| Workflow | desktop | android | ios | wasm | windows |
|---|---|---|---|---|---|
| 1. Open a record + Back | R (hero Record, Due next Record → Knuckles on the Monitor pane, Agenda → Ziggy, sidebar Anesthesia, Back → Today) | R, launch only (CI emulator: first screen) | X (CI build only) | R (A pass, Chromium: hero Record → Knuckles) | X (CI build only) |
| 2. New patient to record | R (empty Create: 5 errors, no file; Ziggy saved with OR 2 and 14:30, record opened, Patients 4 → 5) | X | X | X | X |
| 3. Pre-op + Induce | R (5 checks + ASA I in the store; Induce → Status Anesthetized 12:48:26, Anesthesia 1 → 2) | X | X | X | X |
| 4. Record observations | R (HR stepped to 98, readings 13 → 14, overdue banner cleared, next 4:56; A pass: in the Vitals drawer, the same) | X | X | R (A pass, Chromium: Vitals drawer, HR 98, next 4:47, banner cleared) | X |
| 5. Administer a dose | R (Meloxicam 0.2 mg/kg × 18.4 kg ÷ 5 = 0.74 mL SC stored; 2 mg/kg raised the confirmation; Change dose wrote nothing) | X | X | X | X |
| 6. End anesthesia, recovery, sign off, export | R, fix pass (Ziggy created in the app, induced, 2 readings; header End anesthesia → Recovery pane → End anesthesia: Status Recovery, EndedAt 03:41:03; extubated, sternal, pain 1, 37.8 °C, notes; Sign record → Signed by Dr. Okafor; relaunch, Review record → Export CSV wrote the CSV through a portal stand-in) | X | X | X | X |
| 7. Settings + relaunch | R (first name, surname, 3 min, Dark in preferences.json; relaunch shows Dark, "Dr. Okafor", AO, 3-minute strip, Ziggy and the signed Milo) | X | X | X | X |

## Findings

### Fixed (A pass, 2026-11-04)
- [Data] A case could not be edited once created (was a functional Major). Built (15533c0, D9 proposed): Edit case details
  in the Preparation drawer, until sign-off, opens the case form filled from the case (patient, weight, procedure, team,
  room, schedule). Each changed detail is logged (time, detail, before, after), listed first in Visit history and written
  to the CSV and the summary; readings and doses given stay as recorded. R (desktop: Knuckles 18.4 → 14.8 kg; the file
  holds the correction; Visit history lists it; Cefazolin 22 mg/kg then draws up 3.26 mL instead of 4.05) / C (10 tests).
- [Interactions] 11 "Coming soon" controls advertised unbuilt features (was a functional Major, D6). Cut (9376e13, D7
  proposed, supersedes D6): the clinic switcher, Records, Messages, Reports, Help center, the account menu, search, the
  bell (the whole top bar cluster) and the care-time report button. "See the prep checklist" is wired: it opens the next
  booked case on its Preparation drawer (R: Oscar). The "No monitor connected" pill stays as a status.
- [Forms] An empty reading closed the Record observations pane: the drawer now stays open with its own Not saved bar - C
  (VitalsModelTests) / R (drawer error bar seen with a read fault).
- [Forms] "0 years · " for a patient without an age or owner (2998f6a) - C.
- [Logic] The chart caption ignored the interval (2998f6a) - C/R ("Observed values · 5-minute strip" from Settings).
- [Navigation] With the Recovery pane open the record nav highlighted Preparation: a drawer without a nav item highlights
  none (f50c14b) - C.
- [Accessibility] Disabled controls kept the enabled look (4e37b9a) - R (Anesthesia item faded after End anesthesia).
- [Visual] The hard-coded clinic identity went with the clinic switcher (9376e13) - R.
- [Failure handling] No crash handler, and unguarded preference saves: fixed by the uno-audit (1b44591, f69a057) - C.
- [Platform] wasm: run in headless Chromium (Release publish): open a record and record a reading - R
  (docs/evidence/a-pass-2026-11-04).
- Seen and fixed during the A pass: the Dose drawer and the Redose page showed type names in their fields (ca7c793);
  focus stayed on the page after a drawer closed (7731f4b); 80 px of empty-row spacing under the vitals pad.

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
None open (A pass: case correction built, the "Coming soon" controls cut).

### Majors (head)
- [Platform] ios: no runtime run (CI simulator build only) - X.
- [Platform] windows (WinAppSDK): no runtime run (CI build check only) - X.
- android: the CI emulator smoke job installs and launches the Release APK and finds the first screen (see HANDOFF for
  the run); no workflow beyond the launch is driven there.

### Minors
- [Platform] The CSV save picker fails on Linux desktop (Uno `LinuxFileSaverExtension`: "Can not determine DBus session
  bus address"), and the app reports "Export cancelled." (`RecordExporter.cs:31-34`). A failed picker cannot be told from
  a cancel - R.
- [Forms] New patient lists its errors in a footer above Create, not at the fields - R.
- [Data] Copy summary shows "Summary copied"; the clipboard contents were not read (no reader in the container) - R/X.
- [Navigation] After sidebar Anesthesia, both Patients and Anesthesia stay highlighted - R.
- [Responsive] At 390 px the record's hero tray is cut off at its right edge (the photo and the nav highlight end at
  x=348 with no border) - R.
- [Responsive] At 390 px the menu drawer opens 107 px below the top, with no scrim, and shows the desktop
  collapse-sidebar button - R.
- [Performance] Startup to a settled Today took 14.7 s in the 2026-10-28 Release timing run (HANDOFF), against a 2 s
  target. Not re-measured here: Debug navigations settled within the 6-8 s capture waits - I.
- [Visual] "Anaesthesia" (Agenda, gantt: `CareDay.cs:9`, `AgendaPage.xaml:42`) and "Anesthesia" are mixed - R.
- [Testing] No automated UI suite. Workflow proof is the audits' logs; 223/223 unit tests pass (`dotnet test`) - R.
- [Accessibility] On Today, Tab alternates between the Due next rows and the care-time card's "See the prep checklist"
  (the two columns sit side by side) - R (focus log, A pass).

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
- D6 "Coming soon" controls (11): cut in the A pass under the owner's "build small, cut large" rule (D7, proposed,
  supersedes D6). Confirm D7.
- Resolved in the fix pass: End anesthesia sits in the procedure header while the case is under anesthesia (see Fixed).
  Original question: where should it live in v2? Candidates: a sixth RecordNav item "Recovery", or a second button in
  the stage card while the case is under anesthesia.
- Case correction: built in the A pass as "editable until sign-off, each change logged" (D9, proposed). Confirm, or ask
  for a reason or a second signature per correction.
- Authentication N/A: shared clinic device, typed-name sign-off (SPEC.md). Confirm.
- Derived inventory: workflows 6 and 7 are assembled from SPEC.md flows 5-6. Confirm that ending anesthesia belongs in
  the critical set.
- ios and windows have no runtime evidence (wasm ran in Chromium and android in the CI emulator in the A pass). Which
  heads must be proven before Vigil is called done?
- Formulary and alarm thresholds are sample reference data, labelled "not for clinical use", and a practice cannot load
  its own (SPEC.md). Confirm that this stays out of scope.
