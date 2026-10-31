# Completeness audit: Vigil v2 redesign (2026-10-28, Uno.Sdk 6.7.30, heads: desktop, android, ios, wasm)

Scope: app shell (sidebar, top bar, routes), Today (route Today, `BoardPage`), Patient care (route Record) and every
workflow through them. Sources: SPEC-v2.md (decisions, capability inventory, State designs, Quality Gates), HANDOFF.md,
docs/design/*.md, the two reference renders, docs/evidence/v2/*. Method: `uno-completeness-audit` with runtime proof
through the App MCP on `net10.0-desktop` (`--clover-preview --clover-size=1440x1084`, sample day reseeded per launch),
store files read from `%LOCALAPPDATA%\Clover\com.unoplatform.vigil\LocalState\clover-design-preview\cases` after each
write. Evidence grades: R runtime, C code, I inferred, X not checked.

**Verdict: Complete with gaps** (0 Blockers, 5 Majors, 9 Minors) at 2a10457.
Initial grade at 4881137 was **Incomplete** (2 Blockers, 7 Majors, 9 Minors); 2 Blockers and 3 Majors were fixed in
this pass (see Fixed).

Safe to show (desktop, R this session): Today hero, cards, Due next and every Due next action; open a record from
Today, sidebar Anesthesia and New patient; record observations; pre-op checklist + ASA + Induce; administer dose;
pain score; lab review; discharge call; export panel of a signed record.
Keep off screen: the 10-35 s blank load between pages (Debug), anything on android/wasm/ios, a new patient's OR room
and planned start (not captured), fluids (sample only).

## Inventory (source: SPEC-v2 Spec Graph + capability inventory)

Screens: 10 intended, 10 present, 0 unreachable (Shell, Today, Patients, Agenda, Record with 7 panes, Dose, SignOff,
ParentPreview, Settings, NewCase `!`, Menu `!` below 760). Record panes: Preop, Monitor, Dose, Drugs, Recovery,
History, Labs, all reachable (Recovery for a signed record only since this pass).
Routes: 11 registered under Main; 0 dead references.
Interactions: 88 XAML triggers (`Command=` / `Navigation.Request`). 0 dead by construction. Disabled by user decision:
Records, Messages, Reports, Help center, clinic switcher, search, bell, account menu, Monitoring pill; also disabled
"Coming soon" beyond that list: care-time report button and "See the prep checklist" (Minor below).
Critical workflows:
1. Open a record (Today card / hero Record / Due next row, Patients, sidebar Anesthesia, New patient) and Back
2. Record observations
3. Pre-op checklist + ASA + Induce
4. Administer dose (calculation, persistence, redose)
5. Recovery: pain score, extubation, sign-off, export, discharge call
6. Due next actions (Record, Score, Prepare, Review, Call) clear their task
7. Settings (surname, theme, language), parent preview, agenda, breadcrumb

## Completeness by area

| # | Area | Status | Evidence | Blockers | Majors | Minors | Note |
|---|---|---|---|---|---|---|---|
| 1 | Screens | Complete | R | 0 | 0 | 1 | 10/10; New case and pane bodies keep the old Clover styling |
| 2 | Navigation | Complete | R | 0 | 0 | 1 | Wrong-patient record fixed; sidebar highlight stays on Patients after Back to Today |
| 3 | Interactions | Complete | R | 0 | 0 | 1 | Review/Call/Score/export now reachable; two extra "Coming soon" buttons |
| 4 | Logic | Complete | R | 0 | 0 | 0 | 121 unit tests; pain, lab review, discharge call rules added with tests |
| 5 | Data | Partial | R | 0 | 2 | 0 | Writes persist (store files read back); v2 fields for new patients and fluids are sample-only |
| 6 | States | Complete | R | 0 | 0 | 0 | Loading/empty/error/retry captured 2026-10-28 (docs/evidence/v2 today-*, patient-*); not re-run here |
| 7 | Forms | Partial | R | 0 | 0 | 2 | New case validates and creates (Ziggy); dose TextBox header overlaps value; "0 years ·" residue |
| 8 | Authentication | N/A (decision) | C | 0 | 0 | 0 | Local single-device record, no accounts (SPEC.md) |
| 9 | Responsive/adaptive UI | Complete | R | 0 | 0 | 0 | 1440/1000/700 captured 2026-10-28 (shell-*, today-*, patient-*); not re-run here |
| 10 | Platform behavior | Partial | C | 0 | 1 | 0 | wasm and android build clean; no v2 runtime on any head but desktop |
| 11 | Failure handling | Complete | R | 0 | 0 | 0 | Write failures surface in the InfoBar; signed-record writes refused except the discharge call (test) |
| 12 | Accessibility | Partial | C | 0 | 0 | 1 | Named controls and composed names present; disabled controls keep the enabled look by design |
| 13 | Performance | Broken | R | 0 | 1 | 0 | 10-35 s blank between pages in Debug; Release not measured |
| 14 | Security/privacy | Complete | C | 0 | 0 | 0 | No network, no secrets; records unencrypted in app storage |
| 15 | Visual quality | Partial | R | 0 | 0 | 2 | Hot Design toolbar overlaps the logo (Debug); pane bodies off the v2 type ramp |
| 16 | Testing | Partial | R | 0 | 1 | 1 | 121 tests pass; no automated UI suite; App MCP proof is this session's log |

## Critical workflows by head

| Workflow | desktop | android | ios | wasm |
|---|---|---|---|---|
| 1. Open a record + Back | R (Oscar → Anesthesia → Bella stays Bella 20 s with pane open; Back → Today; New patient "Ziggy" → record, Patients 4 → 5) | X (build only) | X (no Mac) | X (build only) |
| 2. Record observations | R (overdue 3:11 → "next 5:00", chart and banner updated) | X | X | X |
| 3. Pre-op + Induce | R (Oscar 5/5 checks + ASA I in store, Induce → Status Anesthetized, InducedAt 12:45:38, Anesthesia count 1 → 2) | X | X | X |
| 4. Administer dose | R (Cefazolin 22 mg/kg × 6.8 kg = 1.50 mL IV in store; next dose 14:16 = +90 min) | X | X | X |
| 5. Recovery, sign-off, export, call | R (pain "2" → PainScores gains 12:44 entry; Pepper "Call made" → DoneAt in store, still Signed; Export/Copy visible); sign-off itself R 2026-10-08 audit, not re-run | X | X | X |
| 6. Due next actions clear | R (Review → Labs pane → "Mark reviewed": Reviewed + pre-op check, 5 → 4 tasks; Call → row gone; Score → due 12:59; Record → reading saved) | X | X | X |
| 7. Settings, parent preview, agenda, breadcrumb | R 2026-10-28 earlier session (shell-agenda, shell-patients, patient-back-from-sidebar captures); Settings surname/theme/language C this session | X | X | X |

## Fixed in this pass

- **Blocker, Navigation**: opening a record from the shell (sidebar Anesthesia, New patient) while a record was open
  showed the previous patient's data under the new name once a pane opened. The shell now clears to Today and opens
  the record from there. Fix cycle 1 (single `Today/Record` route) failed at runtime; cycle 2 held. - 0140e26 - R.
- **Blocker, Interactions**: a signed record's CTA opened History, so Export CSV / Copy summary were unreachable. It now
  opens Recovery & handover. - 2a10457 - R.
- **Major, Logic**: pain score wrote only the legacy field; the 15-minute task never cleared and the status card
  ("1 of 4") disagreed with the pane. Each tap of 0-4 now records a timed score (`RecoveryLog.WithPainScore`). - a400bc7 - R + 3 tests.
- **Major, Interactions**: Due next "Review" had no completion; the Lab results pane now lists panels with
  "Mark reviewed" (`Case.WithLabReviewed`). - 2a10457 - R + test.
- **Major, Interactions**: Due next "Call" opened Sign-off; it now opens Recovery & handover with "Call made"
  (`Case.WithDischargeCallDone`); the store accepts that one follow-up on a signed record (`Case.OnlyFollowUpDiffers`). - 2a10457 - R + 2 tests.
- Every Due next action opens the record on its pane (`CaseRefPane`). - 2a10457 - R + test.

## Findings

### Blockers
None.

### Majors
- [Data] New patient captures no OR room, planned start/minutes, fasting time or parent notification
  (`NewCasePage.xaml`): a new case shows "starts —", no OR, no planned gantt slot, no next-surgery stats. These v2 fields
  are written only by `SampleDay` - C/R ("Ziggy" record) - owner: mvux + uno-toolkit (form).
- [Data] Fluids ("Fluids running · LRS 5 mL/kg/h") have no entry or stop action anywhere; only the sample day sets
  `Case.Fluids` - C - owner: mvux. Needs a UI decision (see Unresolved Questions).
- [Performance] Each navigation renders a blank frame (header only, no Progress skeleton) for 10-35 s in Debug before the
  record or Today appears (measured by screenshot polling: Today revisit ~16 s, Record via Due next 20-35 s, New patient
  → record ~40 s). The first launch renders Today in under 5 s. Cause not traced (I: page construction on the UI thread,
  or record models that stay alive after navigation and keep their 1 Hz clock feeds). Release not measured - R (Debug) /
  X (Release) - owner: uno-audit / uno-hill-climb.
- [Platform] android and wasm: `dotnet build` 0 warnings, 0 errors this session, no runtime run of v2 - X.
  ios: not built (Windows host) - X.
- [Testing] No automated UI suite; workflow proof is this session's App MCP log and store reads - C.

### Minors
- [Navigation] After Back from a record to Today the sidebar keeps "Patients" highlighted - R.
- [Interactions] "See the prep checklist" and the care-time report button are "Coming soon" although a prep checklist
  exists (Record > Preparation) - C.
- [Forms] Dose pane: the "Dose (mg/kg)" header overlaps the typed value - R.
- [Forms] A new patient without age and owner reads "0 years ·" - R.
- [Screens/Visual] New case sheet and the record pane bodies (Preop, Monitor, Dose, Recovery, History) keep the v1
  Clover/Material styling, not the v2 type ramp - R - owner: gold-standard-pass.
- [Visual] Hot Design toolbar overlaps the logo in Debug - R.
- [Accessibility] Disabled controls keep the enabled look (`Themes/Tokens.xaml:378`), so a disabled button reads as
  active by sight; screen readers get IsEnabled - C.
- [Testing] Record and Today state captures (loading/empty/error/retry, dark, 1000/700) are from the earlier session
  today, not re-run after these fixes - R (prior).
- [Performance] Clock-driven feeds re-render the record every second while a pane is open (I).

## Debug checkpoint (closed)

Wrong-patient record: tried (1) navigating `Today/Record` with a cleared frame: the new page still showed Oscar after a
pane opened; (2) clearing to Today with `Go`, then navigating Record from there: held for 20 s with the pane open and
after Back. Evidence: `uno_app_get_element_datacontext` on the page returned Bella's model while the FeedView template
showed Oscar. Cause not traced (I: a stale FeedView subscription when the shell navigator opens Record directly). Not
reduced to a minimal repro; candidate runtime gotcha.

## Not checked (X)
- Release-build navigation timing - Debug only - time the same three navigations in a Release desktop run.
- android, wasm, ios runtime for every workflow - not run this session - `uno_app_start` per TFM, emulator/browser.
- Dark theme, French and 760/393 widths after this pass's XAML changes (Labs pane list, pain buttons, discharge block) - not re-captured.
- Sign-off itself and CSV export file write - covered by the 2026-10-08 audit, not re-run.

## Noted for other skills
- uno-audit: `RecordPage.xaml` is 830 lines with seven panes in one template; split panes into controls (load time).
- gold-standard-pass: restyle the pane bodies and New case to the v2 ramp; fix the dose TextBox header overlap.

## Unresolved Questions
- User decision 1 (kept): Records, Messages, Reports, Help center, clinic switcher, search, bell and account menu are
  visible but disabled with "Coming soon". They advertise features the app does not have; revisit before release.
- "Monitoring on" pill (kept, user decision): it reads as an active monitor link while Vigil has none; the "Monitor
  signal lost" and "Monitoring paused" states are omitted. Revisit if a monitor integration is wanted.
- Waveforms are recorded-HR sparklines by decision; no live monitor by decision; not a tablet app (no 56 px rule).
- Fluids: add a start/stop fluid action (where: Dose pane or a Fluids pane?) or label the card as recorded elsewhere?
- New patient: add OR room and planned start (time picker) to the New case form, or schedule them elsewhere (Agenda)?
- Due next actions now open the record on the task's pane (Monitor, Recovery, Labs); confirm this is the intended flow.
- A signed record accepts one change after sign-off: marking the discharge call made. Confirm that rule.
- Authentication N/A: local single-device record by design (SPEC.md); confirm.
- android, wasm, ios: no v2 runtime evidence; which heads must be proven before calling v2 done?
- Performance: is the 10-35 s Debug navigation acceptable for demos, or should Release be measured and the record page split?
