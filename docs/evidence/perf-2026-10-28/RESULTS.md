# Page-change timing, 2026-10-28

Harness: `Vigil/Services/PageTimer.cs`, run with `VIGIL_PERF=<csv>` (own data folder `vigil-perf`, sample day,
pinned to no clock). It drives the shell's own commands (no input injection): Today → Patients → Settings → Agenda →
Today → Record (Anesthesia) → Today, twice. Desktop Skia, Windows 11, Uno.Sdk 6.7.30, launched without the devserver.
Columns are ms from the request: frame navigated, page Loaded, first rendered frame after Loaded, settled (element
count unchanged for 1.5 s; the time of the last change, so late photo loads and clock-driven changes count).

"First frame" is the number to read for "how long is the page blank".

| Step | Release first frame (run 1 / run 2) | Debug first frame (run 1 / run 2) |
|---|---|---|
| Startup → Today settled | 14.7 s (23.6 s on the very first launch, which also seeds) | 53.2 s |
| Patients | 1.0 / 0.1 s | 0.6 / 0.3 s |
| Settings | 2.0 / 0.2 s | 1.8 / 0.5 s |
| Agenda | 0.3 / 0.3 s | 0.5 / 0.3 s |
| Today | 0.1 / 0.4 s | 1.0 / 0.6 s |
| Record | 0.4 / 0.9 s | 7.1 / 2.8 s |
| Today (back from Record) | 0.1 / 0.4 s | 1.5 / 0.9 s |

Findings:
- Release page changes show their first frame within 0.1–2.0 s; the 10–35 s blank pages seen earlier were a Debug
  build under the devserver and Hot Design (App MCP launch). Debug alone peaks at 7.1 s (Record, first visit).
- Slowest Release items: startup (14.7 s to a settled Today), Settings first visit (2.0 s: first use of the
  Material RadioButtons/ToggleSwitch templates), Record (0.4–0.9 s to first frame, 1.2–2.8 s settled).
- Today settles late (up to 6.0 s) after its first frame: photos decode after the cards render.
- Both processes exited with 0xE0434352 (unhandled .NET exception) after `Application.Exit()` from the harness; the
  first Release run exited 0. CSVs were complete each time. Not yet investigated.

Raw: `perf-release.csv`, `perf-debug.csv`.
