# HANDOFF — Vigil: v2 redesign (Today dashboard, Patient care, shell)
Updated: 2026-10-28 (America/Montreal)

## Where we are
Building the v2 redesign from three references: `Downloads\Today · dashboard (C cards)-html`,
`Downloads\Patient care · Bella-html`, and a states image (recorded in SPEC-v2 Interaction Brief). The spec is
`SPEC-v2.md` (read it first) with exact values in `docs/design/{shell-and-tokens,today-dashboard-values,patient-care-values}.md`.
Phase 1a done (85448bb): v2 palette, phase colours and font wiring app-wide. Branch `archive/local-2026-10-28`,
pushed up to 592803b; 2e4c73b and 85448bb are local.

## Last verified state
- Build: desktop 0 warnings. Tests 72/72. Runtime: v2 tokens not yet run.
- Fonts: manifests point at `Assets/Fonts/HankenGrotesk_{Regular,Medium,SemiBold}.ttf`, `MartianMono_Medium.ttf`,
  `Newsreader_Regular.ttf` plus base copies `HankenGrotesk.ttf`, `MartianMono.ttf`, `Newsreader.ttf` (copy of the
  400/500 file, like PublicSans.ttf). **The TTFs are missing**: the session could not download them. Run the
  PowerShell one-liner in the chat (Google css2 with the Android 2.2 UA), check each starts with 00 01 00 00 and has
  no `fvar`, add the three base copies, then delete PublicSans*, IBMPlexMono*, ArchivoNarrow* (no other references).
- Lint: 113 gating before v2 work.

## Next actions (in order) — SPEC-v2 Implementation Plan
1. Fonts in place (above), run the app once, check type renders.
2. Phase 2 domain: new Case fields with defaults (OperatingRoom, PlannedStart/Minutes, PrepStartedAt, DischargedAt,
   Fluids, Labs, PainScores, DischargeCall, FastingSince, ParentNotifiedAt), formulary RedoseMinutes, preference
   PlannedPrepMinutes; pure `Stage`, `CarePhases`, `DueTasks`, `CareInsight` with unit tests and a JSON back-compat
   test; sample day matching the prototype.
3. Phase 3 shell (ShellPage, sidebar with disabled items, top bar), routes under Main; remove the board AgendaDock.
4. Phase 4 Today, Phase 5 Patient care, Phase 6 polish (dark, responsive, French, lint, parity), per SPEC-v2.

## Open questions
- Monitoring: the states image shows "Monitor signal lost" and "Monitoring paused · Last sync · Retry". Vigil has no
  monitor link. Build a connection service (which device/protocol) or keep the pill disabled and drop those states?
- See SPEC-v2 Unresolved Questions (disabled-but-visible features, derived dark values, Skia letter-spacing).

## Relaunch
```
cd C:\Users\Platform006\Vigil ; git checkout archive/local-2026-10-28
dotnet build Vigil/Vigil.csproj -p:VigilTargetFrameworks=net10.0-desktop ; dotnet test Vigil.Tests/Vigil.Tests.csproj
uno_app_start  projectPath=C:\Users\Platform006\Vigil\Vigil\Vigil.csproj  tfm=net10.0-desktop  args=--clover-preview --clover-size=1440x1000
Before a rebuild: uno_app_close, then Get-Process Vigil | Stop-Process -Force (exe lock).
Strings: python tools/localize.py --write   (fails on missing French; add French to tools/fr.json in insertion order)
```
