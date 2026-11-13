# App MCP visual pass, 2026-10-29

Debug + devserver, `--clover-preview` (sample day, clock pinned at 12:42), desktop Skia, Windows 11, Uno.Sdk 6.7.30.
Driven through automation peers (no pointer or keyboard injection).

## Proven
- Today: avatar shows "AO" (first name Amara + Okafor).
- Tracked numbers match the prototype widths. Element captures, in px:

  | Element | Font | Expected (CSS) | Rendered |
  |---|---|---|---|
  | Hero "1:12" | 64 px, -0.05 em | 166.4 | 167 (`el-hero.png`) |
  | "7h 16m" | 28 px, -0.03 em | 112.6 | 113 |
  | Card timer | 20 px, -0.02 em | 54.4 | 55 (`el-timer.png`) |
- Patients: Today's patient cards. "Show details" grows its rows. A capture mid-animation shows the rows at about 56 of 132 px and the chevron at 90°; the end state shows the procedure, weight, pet parent and team.
- Settings, Agenda, New patient, Medication pane: v2 layout renders, in light and dark.
- New patient: Nova was created with OR 2, start 14h30, 45 min, fasting 20:00 and the parent notified. The record shows:
  - "Planned 14:30 · OR 2"
  - the status card's "starts 14:30" with a 1:46 countdown
  - fasting 16:44 (since 20:00 the night before)
  - a Patients count that went from 4 to 5, and Nova under "In preparation" on the Agenda
- Fluids, from the card footer row into the Medication pane:
  1. Start → "Fluids running · LRS 5 mL/kg/h", and the button reads "Change rate".
  2. Rate 8 → "LRS 8 mL/kg/h".
  3. Stop → "No fluids running".

## Found and fixed in this pass
- Status strips (Anesthesia, In recovery, Booked) and the record status card filled a square behind a rounded edge. A filled `ShadowContainer` with an inner shadow paints its fill square on Skia. They are now three rounded Borders: fill, a 4 px inset shade, and the edge (`strip-zoom*.png` before, `strips-after.png` and `status-after.png` after).
- Placeholder times ("14:00", "OR 1") read as filled-in values next to the floating labels. They were removed, and the hint moved to the help text.
- Species box was 40 px beside 56 px fields. `ComboBoxMinHeight` is now 56.
- "No doses yet…" did not wrap after the restyle.
- Empty error and language-notice lines added blank space. They now collapse while empty.
- The Agenda ruler's "Now" pill covered the 13:00 label. An hour label under the pill is now left out.
- On user request:
  - the card photo shows the whole picture (`Stretch="Uniform"`);
  - Bella was renamed Knuckles in the sample day.

## Not covered
- The 700 px Settings check.
- The Release head under the App MCP (timing only, see `../perf-2026-10-28`).
- Re-verifying the hidden "Now" hour label after the last threshold change (C# only; unit math in `CareRuler.cs`).
