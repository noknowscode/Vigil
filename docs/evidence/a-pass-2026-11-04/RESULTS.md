# A pass runtime proof (2026-11-04)

Desktop: Debug `net10.0-desktop` on Linux under Xvfb, `APP_NO_HOTDESIGN=1`, `VIGIL_X11_RENDERER=software`, driven with
xdotool (press 150 ms, release) and the Debug hooks (`--vigil-seed=sample`, `--vigil-now=12:42:26`, `--vigil-theme`,
`--vigil-route`, `--clover-size`, `VIGIL_FAULT`, `--vigil-focuslog`). Every write was read back from the case file under
`LocalState/cases`. WebAssembly: Release `net10.0-browserwasm` publish served over HTTP and driven in headless Chromium
(Playwright, 1280 x 800).

## WebAssembly (Chromium)

Workflow: open a record and record a reading.

| Step | Capture | Result |
|---|---|---|
| Fresh profile, Today | `wasm-1-today.png` | Empty store first ("Nothing in surgery"); Settings > Load sample day > Replace records loaded the day |
| Hero Record | `wasm-2-record.png` | Knuckles' record, under anesthesia, reading overdue 6:32 |
| Record nav Vitals | `wasm-3-vitals-drawer.png` | The Record observations drawer over the record, pad pre-filled from the 16:12 reading |
| HR + twice, Record reading | `wasm-4-reading-recorded.png` | HR 98, the button reads "next 4:47" |
| Escape | `wasm-5-record-after.png` | Drawer closed; last reading 16:24, next due 16:29, the overdue banner gone, Circulation 98 |

One console line during the run: `Error synchronizing filesystem from IndexDB: [object Object] (errno: 44)`; the
reading was saved and shown. The Release publish reports only trim-analysis warnings from generated code (IL2026 x 529,
IL2104 x 3), which the build gate keeps as warnings by design.

## Desktop

| Capture | What it shows |
|---|---|
| `desktop-01-record-1280.png` | The record after the split: no top bar cluster, the sidebar without the cut items, the "No monitor connected" status |
| `desktop-02-vitals-drawer.png` | Vitals drawer, 640 px from the right over a scrim; no empty-row gap under the pad |
| `desktop-03-reading-recorded.png` | HR stepped 94 to 98 and recorded: the case file had 14 readings, the last HR 98 at 12:43:07; "next 4:56" |
| `desktop-04-preop-drawer.png` | Preparation drawer with Edit case details |
| `desktop-05-case-details.png` | The case form filled from Knuckles, with the dose note (she has doses) |
| `desktop-06-weight-corrected.png` | Weight 18.4 corrected to 14.8: the file holds `WeightKg 14.8` and the correction `Weight 18.4 -> 14.8` |
| `desktop-07-history-corrections.png` | Visit history lists the correction above the readings |
| `desktop-08-dose-after-correction.png` | Cefazolin 22 mg/kg now draws up 3.26 mL (14.8 kg x 22 / 100) |
| `desktop-09-dose-given.png` | Dose drawer Give: 4.05 mL saved (before the correction), drawer closed, focus on Administer dose, acknowledgement chip |
| `desktop-10-anesthesia-ended.png` | Recovery drawer: End anesthesia; Status Recovery, the Anesthesia item disabled and faded |
| `desktop-11-signoff-sheet.png` | Sign off handed back from the Recovery drawer opens the sign-off sheet |
| `desktop-12-drawer-error-dark.png` | Dark, `VIGIL_FAULT=record-read-x8`, landing on Visit history: the drawer's error state with Retry |
| `desktop-13-drawer-retry-dark.png` | Retry recovers the drawer and the page; Medical history highlighted while its drawer is open |
| `desktop-14-vitals-390.png` | 390 px: the drawer takes the width but a 42 px strip of scrim (a dismiss target) |
| `desktop-15-preop-390-dark.png` | 390 px dark, Preparation drawer |
| `desktop-16-today-care-time.png` | Today: no report button; the insight's "See the prep checklist" |
| `desktop-17-prep-checklist.png` | The link opened Oscar's record on its Preparation drawer |

Hand-backs proven: Labs > View preparation opened the Preparation drawer; Preparation > Edit case details opened the
case form; Recovery > Sign off opened the sign-off sheet; Medications > Give a dose (unit tests); a Due next landing
(`--vigil-route=Record:History`) opened its drawer.

Keyboard (`--vigil-focuslog`): Tab from the window reaches Collapse sidebar, New patient, Today, Patients, Agenda,
Anesthesia, Settings, then on the record Back, Patients list and each record nav item; Enter on Medical history opened
its drawer and focus moved into it; Escape closed it and focus returned to Medical history; Enter opened it again. On
Today, Tab reaches the hero's Record, the filters, the cards, Show details, the Due next rows and the prep checklist
link. A drawer that hands over to a sheet leaves focus in the sheet (Preparation > Edit case details: focus Preparation,
then the sheet, then its fields).

Harness notes: a `--clover-size` resize races the first layout now and then (a 640 x 640 frame in a larger window);
those runs were discarded and relaunched. Forcing window focus with `xdotool windowfocus` while a sheet is open closes
it (the flyout's light dismiss on window activation), so keys are sent without it.
