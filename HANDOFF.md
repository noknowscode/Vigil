# HANDOFF — Vigil: A pass (record drawers, case correction, cuts, Android emulator CI)
Updated: 2026-11-04

## Where we are
The A pass of the production-readiness round (uno-lab docs/production-readiness/A-GRADE-PLAN.md) is on
`feat/production-readiness` (PR to main, CI on every push):
- **Record split (D8).** Each of the record's seven panes is a `!` route to a Flyout view in the Toolkit
  `DrawerFlyoutPresenter` (`Presentation/Record/Drawers/`), with its own model over a shared `CaseDrawer`. RecordModel
  keeps the page and carries out what a drawer hands back (`PaneChoice`: next pane, Sign off, Edit case details, Delete
  case); a Due next task lands on its drawer. `RecordPage.xaml` 903 → 439 lines, `RecordModel` 306 → 163, no
  code-behind. Focus returns to the opener (`Controls/FlyoutFocus.cs`). The Dose drawer shares DoseModel with the Due
  next page, whose route is now "Redose".
- **Case correction (D9, proposed).** Edit case details (Preparation drawer, until sign-off) reopens the case form
  (`Presentation/CaseForm/CaseFormPage`, shared with New patient). Each change is logged in the case (`Corrections`),
  shown first in Visit history and exported; the dose calculator follows the corrected weight.
- **Cut (D7, proposed, supersedes D6).** The 11 "Coming soon" controls and their copy are gone (the top bar cluster with
  them); "See the prep checklist" opens the next booked case's Preparation drawer; "No monitor connected" stays.
- **Android readiness.** 144 `FallbackValue=Collapsed`; localized stepper names; French "Clair" (`tools/fr.json` gained
  `@keys` for per-key French); disabled buttons fade.
- **Tests.** 223 (was 159): every page and drawer model, through a recording navigator (`Vigil.Tests/TestNavigation.cs`).
- **CI.** Android emulator smoke job (shared recipe; marker "Nothing in surgery", settle 45 s); workflow concurrency
  (a newer push cancels the run it supersedes, never on main where the Pages deploy runs).

## Last verified state
- Build: desktop Debug and Release 0 warnings; WebAssembly Release publish (trim-analysis IL2026/IL2104 only, kept as
  warnings by design). Tests 223/223 (stable over 6 runs). Lint gate: CARD, HEX, TOKENTHEME, BACKBAR, CODEBEHIND,
  OVERLAY, RESPONSIVE, BUILTIN, ICON all 0. Automation names 25.9 per 1,000 XAML lines (130 / 5,019).
- Runtime: desktop (Xvfb, software renderer) and WebAssembly (headless Chromium), docs/evidence/a-pass-2026-11-04.
  Keyboard walk with `--vigil-focuslog` (new Debug hook).
- CI: run 41870428201 at 6937dba: unit tests, desktop linux-x64 and win-x64, WinAppSDK, WebAssembly, Android and the
  Android emulator smoke test green (marker "Nothing in surgery" after 22 s, process alive at 45 s, frame not blank:
  luma spread 30.1, 175 colours; no crash or ANR); the macOS jobs (iOS, osx-arm64) were still queued behind older runs.
  Earlier: 41848463515 green at 7a928e7; 41839566033 failed only the blank-frame check (a black capture right after the
  marker), hence the 45 s settle; 41855113111, 41863962685 and 41869324137 failed the Android Release publish on two
  doc comments the other heads do not check (CS1570, CS1574; fixed in 2e0fcdc and 6937dba).
- Docs: docs/COMPLETENESS.md (A pass: 0 Blockers, 0 functional Majors, 2 head Majors, 10 Minors), docs/UNO-AUDIT.md
  (follow-up section), docs/DECISIONS.md (D7, D8, D9).

## Open (owner decisions)
- Live pulse on Today costs about 20 % of a core while a case is under anesthesia (Forever storyboard): keep the
  design's pulse, or stop it after a few cycles. Left as is.
- D7 (the cut) and D9 (correction until sign-off, logged, no reason or second signature) are proposed: confirm.
- A rethrown launch failure leaves a windowless process on desktop (exit after reporting?).
- Test packages behind; FluentAssertions 8 changes licence.

## Next actions
1. Confirm the CI run at the head is green on every job, including the emulator smoke.
2. Minors left in docs/COMPLETENESS.md: Today's Tab order alternates between Due next and the care-time card; at 390 px
   the record hero clips and the menu drawer opens 107 px down; New patient errors sit in a footer; mixed
   "Anaesthesia"/"Anesthesia"; the Linux CSV picker failure reads "Export cancelled."; startup time.
3. At 390 px a drawer leaves a 42 px strip of scrim (DrawerLength 640 against the window); decide if phones should get
   the full width.
4. iOS and WinAppSDK heads are CI builds only.

## Relaunch (this Linux box)
Build: `dotnet build Vigil/Vigil.csproj -f net10.0-desktop -p:VigilTargetFrameworks=net10.0-desktop`
Run: `APP_NO_HOTDESIGN=1 VIGIL_X11_RENDERER=software DISPLAY=:<n> dotnet Vigil/bin/Debug/net10.0-desktop/Vigil.dll
--vigil-seed=sample --vigil-now=12:42:26 --clover-size=1280x800 --vigil-route=Record:Monitor [--vigil-focuslog]`.
Send keys without `xdotool windowfocus` while a sheet is open (window activation light-dismisses it).
WebAssembly: `dotnet publish Vigil/Vigil.csproj -f net10.0-browserwasm -c Release -p:VigilTargetFrameworks=net10.0-browserwasm -o <dir>`,
serve `<dir>/wwwroot`, drive with Playwright; the first screen takes 40-60 s in headless Chromium.
