# Runtime pass, Clover fixes (2026-10-28)

Uno App MCP, `net10.0-desktop`, Debug, `--clover-preview --clover-size=WxH`. Hot Reload was unavailable (Uno Platform account signed out), so the one fix was rebuilt and relaunched.

| Check | Width / theme | Result | Evidence |
|---|---|---|---|
| Agenda docked >=1120 | 1600 | Pass, docked left | board-1600-light.jpg |
| Agenda undocks <1120, agenda button shows | 1100 | Pass | board-1100-dark.jpg |
| Tabs move to second row <760 | 700 | Pass | board-700-dark.jpg |
| Live band stacks <660 | 640 | Pass | board-640-dark.jpg |
| Frame margin narrows <600 | 393 | Pass (8 px vs 20 px) | board-393-dark-fr.jpg |
| Dark: live band gradient | 1600 | Pass | board-1600-dark.jpg |
| Dark: reminder card readable | 1600 | Pass | board-1600-dark.jpg |
| Dark: no-photo placeholder and initials | 1100 | Pass | nophoto-dark.jpg |
| Dark: agenda ruler block names | 1600 | **Fail, fixed in 62be117** (names drew Light ink on dark blocks) | agenda-1100-dark.jpg (after fix) |
| Ruler blocks <56 px show name only | 1100 | Pass, 60 px block shows name and time, no phase | agenda-1100-dark.jpg |
| Record side buttons 48 px | 1600 | Pass, all five 48 px tall (68-127 wide) | visual tree bounds |
| Record side buttons below 600 | 393 | Collapsed by design (`Narrowest=Collapsed, Normal=Visible`) | record-393-dark-fr.jpg |
| Record hero grows, nothing clipped | 1600, 393 | Pass | record-1600-light.jpg, record-393-dark-fr.jpg |
| French: board, record panes, parent preview, accessible names | 393 | Pass; seed data (breeds, procedures) stays English | board/record/parent-393-dark-fr.jpg |
| Composed accessible names | all | Pass, e.g. "Bella, Under anesthesia. Ovariohysterect..." | visual tree |

## Other observations
- Parent preview back button sits under the debug Hot Design toolbar (16,10, 50x48). Debug-only overlay, not an app defect.
- Switching the language back to English saved correctly (verified on relaunch) but showed no "Language saved" notice. Switching to French did show it.
- Record hero keeps its fixed light inks in dark theme while its stats card goes dark (existing open question).
