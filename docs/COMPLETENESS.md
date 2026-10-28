# Completeness audit: Vigil v2 redesign (2026-10-28, Uno.Sdk 6.7.30, heads: desktop, android, ios, wasm)

Scope: app shell (sidebar, top bar, routes), Today (Board), Patient care (Record) and every workflow through them.
Initial grade at 4881137, before fixes.

Verdict: **Incomplete** (2 Blockers, 7 Majors, 9 Minors). Fix pass follows; the final grade replaces this file.

## Blockers
- [Navigation] Sidebar Anesthesia from an open record shows the previous patient's record under the new name
  (Oscar's data, "Bella" breadcrumb; Record reading disabled; checkboxes bound to the other model) - R.
- [Interactions] A signed record's "Review record" opens History: Export CSV and Copy summary are unreachable - C/R.

## Majors
- [Logic] Pain score never joins `RecoveryLog.PainScores`: the Due next "Pain score" row never clears - R.
- [Interactions] Due next "Review" (bloodwork) has no way to mark a panel reviewed - R.
- [Interactions] Due next "Call" opens Sign-off, a dead end for a signed patient - R.
- [Data] New patient captures none of the v2 fields (OR, planned start, fasting, parent notified); they come only from the sample day - C.
- [Data] Fluids card has no way to start or stop a fluid run (sample-fed) - C.
- [Performance] Record and Today render blank (no skeleton) for 10-35 s per navigation in Debug - R.
- [Platform] android, wasm and ios have no runtime evidence for v2 - X.
