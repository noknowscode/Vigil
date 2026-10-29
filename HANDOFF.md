# HANDOFF — Vigil: v2 follow-through (data entry, perf, screen restyle, polish)
Updated: 2026-10-28 (America/Montreal)

## Where we are
The v2 redesign (SPEC-v2.md) is built; `docs/COMPLETENESS.md` graded it Complete with gaps (f09f8a3). This session
closed most of the remaining Majors in code:
- New patient now captures room, start, length, fasting and parent notified.
- Fluids start and stop from the record's Medication pane.
- The avatar shows two initials from a new first-name setting.
- Settings, New patient, Sign off, Dose, Agenda, Parent preview, Patients and the record panes all use v2 styling.
- The details expansion animates its height.
- Tracked font copies replace the letter-spacing Skia ignores.
- Clover.xaml, CaseCards, CareGreeting and LiveBandView were deleted.
- A Release timing run measured page changes (docs/evidence/perf-2026-10-28/RESULTS.md).

**None of the restyled screens has been seen at runtime yet.** The user approved only the two timing runs, not an
App MCP pass.

## Last verified state
- Build: desktop Debug and Release, 0 warnings. Tests 131/131.
- Runtime: timing runs only (Release and Debug, no devserver). Release page changes reach first frame in 0.1–2.0 s;
  startup takes 14.7 s to a settled Today. Both harness runs exited 0xE0434352 after `Application.Exit()`.
- Git: `archive/local-2026-10-28`, last commit 5d330d8 (plus this handoff).
- Lint: CARD 0 · HEX 0 · BACKBAR 0 · CODEBEHIND 2 (RecordPage pane SizeChanged/Escape) · ICON 2 (CareMark logo).

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| uno-material lightweight keys (OutlinedTextBox*, ComboBox*) | Vigil/Themes/Controls.xaml:11 |
| Card surfaces via CardContentControl (PanelCardStyle) | Vigil/Themes/Tokens.xaml (PanelCardStyle), Settings/NewCase/SignOff/Dose/Agenda/ParentPreview pages |
| NavigationBar for back (lint BACKBAR) | Vigil/Presentation/Parent/ParentPreviewPage.xaml |
| MVUX command parameters from states (StartFluids) | Vigil/Presentation/Record/RecordModel.cs (StartFluids) |
| User: Messages out of MVP, stays disabled | SPEC-v2.md capability table |
Read, not applied: none.

## Next actions (in order)
1. With the user's OK, run an App MCP visual pass covering:
   - The restyled screens, in light and dark.
   - The New patient Schedule card: create a case with a start time and confirm it appears in Today's gantt.
   - Fluids: start, change the rate, then stop.
   - The AO avatar.
   - The details height animation.
   - The tracked numbers against the prototype widths.
   - Settings at 1600 and 700 px.
2. Investigate the 0xE0434352 exit after `Application.Exit()` in PageTimer, and check that a normal window close
   exits 0.
3. Startup (14.7 s Release) and the first Settings visit (2.0 s): profile with `dotnet-trace`, or split the timing
   into time to first frame.
4. Re-run `uno-completeness-audit` and update `docs/COMPLETENESS.md`.
5. Still open from the audit: Android/WASM runtime proof, a UI test suite, a drawer below 760 px, and press scale
   under reduced motion.

## Open questions
- The disabled Records, Reports, Help, search, bell and Monitoring pill are still visible (Messages is resolved: not
  in the MVP).
- `PlannedPrepMinutes` has no Settings field. It is only the default of 25 minutes.
- Native French review of the 48 new strings.

## Relaunch
cd C:\Users\Platform006\Vigil; dotnet build Vigil/Vigil.csproj -f net10.0-desktop
Timing run: $env:VIGIL_PERF="<path>.csv"; Vigil\bin\Release\net10.0-desktop\Vigil.exe (exits by itself).
App MCP: uno_app_start (Debug, devserver). Ask the user before any launch.
