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

The App MCP visual pass ran on 2026-10-29 (docs/evidence/runtime-2026-10-29/RESULTS.md). It proved the new
New patient fields, fluids from start to stop, the AO avatar, the details animation, and that the tracked
numbers match the prototype to within 1 px. It also found and fixed square-filled status strips, placeholder
values that looked filled in, and the ruler label overlap. The sample patient Bella is now Knuckles (user request).

## Last verified state
- Build: desktop Debug and Release, 0 warnings. Tests 131/131.
- Runtime: timing runs only (Release and Debug, no devserver). Release page changes reach first frame in 0.1–2.0 s;
  startup takes 14.7 s to a settled Today. Both harness runs exited 0xE0434352 after `Application.Exit()`.
- Git: `archive/local-2026-10-28`, last commit d052fb9, pushed.
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
1. Investigate the 0xE0434352 exit after `Application.Exit()` in PageTimer, and check that a normal window
   close exits 0.
2. Startup (14.7 s Release) and the first Settings visit (2.0 s): profile with `dotnet-trace`, or split the timing
   into time to first frame.
3. Check Settings and the forms at 700 px, then confirm the Agenda hides the hour label under the Now pill.
4. Re-run `uno-completeness-audit` and update `docs/COMPLETENESS.md`.
5. Still open from the audit: Android/WASM runtime proof, a UI test suite, a drawer below 760 px, and press scale
   under reduced motion.

## Open questions
- The disabled Records, Reports, Help, search, bell and Monitoring pill are still visible (Messages is resolved: not
  in the MVP).
- `PlannedPrepMinutes` has no Settings field. It is only the default of 25 minutes.
- Native French review of the 49 new strings.
- The preview surname reads "Ehok" (typed into the running app during the pass, not by an automation step); it
  lives only in the preview data folder.

## Relaunch
cd C:\Users\Platform006\Vigil; dotnet build Vigil/Vigil.csproj -f net10.0-desktop
Timing run: $env:VIGIL_PERF="<path>.csv"; Vigil\bin\Release\net10.0-desktop\Vigil.exe (exits by itself).
App MCP: uno_app_start (Debug, devserver). Ask the user before any launch.
