# Decisions

## D1. Concept: Vigil, a veterinary anesthesia record (2026-10-06)

Decision: build Vigil, the tablet anesthesia record for surgical vet techs in a small-animal hospital.
Reason: `/product-thinking` Quick mode on two candidates (docs/VALIDATION.md). Vigil earned
**Test first**; event rental turnaround earned **Reframe/Park** because its job lives inside the
rental suite that owns availability. Vigil's job recurs every 5 minutes during every procedure,
stands alone at the point of care, and its riskiest assumption (fast, gloved, interruptible entry) is
exactly what a built app tests.
Tradeoff: a crowded real market (Vet Radar, VetMate, SurgiVitals, Vetnapp, VetMo). That is acceptable
for a reference app and gives a quality bar to compare against.
Caveat: the validation answers are Claude's, graded E0/E1. Nothing here is customer evidence.

No overlap with Patina (public art), Cargo (port ops), FieldCheck (inspections), Lapse (app history),
Loadpath (truss engineering), MorningCard or UnoBusiness.

## D2. New case opens as a result flyout, then the Board navigates to Record (2026-10-07)

Decision: Board's `NewCase` command awaits `!NewCase` for a `CaseRef` result, then navigates to `Record`.
Reason: the sheet stays a pure form (save + `NavigateBackWithResultAsync`), and Back from Record returns to the
Board because the Board did the forward navigation. Navigating to Record from inside the flyout would have
needed a back-then-forward workaround (FieldCheck gotcha).
Tradeoff: the Board model owns one extra command; the sheet cannot be reused to open a record elsewhere.

## D3. Feeds reload from a store `Signal`, not a watch stream (2026-10-07)

Decision: list and record feeds are `Feed.Async(load, store.Changed)`; Retry is a model command that raises the
same signal. The 1 Hz clock is a separate `Feed.AsyncEnumerable` combined in.
Reason: `FeedView.Refresh` over `Feed.AsyncEnumerable` → `Combine` → `AsListFeed` greyed out and never recovered
from an injected read fault (measured). The signal path recovered both Board surfaces at once.
Tradeoff: every store write reloads the whole (cached) list; fine for a day's cases, not for a hospital archive.

## D4. Out-of-range dose confirmation is inline in the dose sheet, not a ContentDialog (2026-10-07)

Decision: Give on an out-of-range dose shows an inline confirmation card in the sheet ("Change dose" filled and
default, "Give anyway" outlined). SPEC.md's Interaction Brief asked for a ContentDialog via
`ShowMessageDialogAsync`.
Reason: measured on desktop: the message dialog opened from the `!Dose` flyout dismissed the flyout itself, so
"Change dose" returned the user to the record with the entry lost. The dialog also rendered Fluent chrome with
"Give anyway" as the default (accent) button.
Tradeoff: no modal focus trap; the confirmation is announced through a LiveSetting region instead.

## D5. No address-bar routing on WebAssembly (2026-10-07)

Decision: `NavigationConfiguration.AddressBarUpdateEnabled = false`.
Reason: measured on WASM, navigation wrote the message dialog's route and full text into the URL
(`/Settings/xxxxmessagedialogxxxx?…title=Load sample day…`), the URL drifted from the visible page (`/Board`
while Settings showed), and Create from the `!NewCase` sheet saved the case but left the app on the Board with
the URL stuck at `/Board/NewCase`. For an anesthesia record, browser history must not hold dialog text or
record routes either.
Tradeoff: no deep links or browser Back on the web head; the in-app Back (NavigationBar) is the only back path.

## D6. The "Coming soon" controls stay: an exception to "every control functional" (2026-11-01)

Decision: the controls without a screen stay visible and disabled, with a "Coming soon" tooltip and automation
name: in the sidebar the clinic switcher, Records, Messages, Reports, Help center and the account menu, plus the
Monitoring pill; in the top bar search and the notification bell; on Today the care-time report button and "See the
prep checklist". They are the one recorded exception to the production-readiness rule that every visible control
works.
Reason: SPEC-v2 user decision 1 (2026-10-28): the sidebar shows every item as designed and items without a screen
are disabled. The code review of 2026-11-01 asked to hide them, build them or record an exception; the owner kept
the design.
Tradeoff: the app advertises features it does not have (docs/COMPLETENESS.md). Building or hiding any of them
retires its part of this exception.

## D8. The record's panes are drawers with their own models (2026-11-04)

Decision: the seven panes of the record (Preparation, Administer dose, Medication record, Record observations, Recovery
and handover, Visit history, Lab results) are `!` routes ("!Preop", "!Dose", "!Drugs", "!Monitor", "!Recovery",
"!History", "!Labs"), each a Flyout view in the Toolkit's DrawerFlyoutPresenter from the right (640 px, the whole window
when narrower) with its own model. A drawer that closes on a step for the record (open another pane, Sign off, the case
details, Delete case) hands it back to RecordModel as a PaneChoice. The Dose drawer shares DoseModel with the Due next
redose page, whose route is now "Redose". Escape, Close and a tap on the scrim close a drawer; the flyout returns focus.
Reason: the approved refactor in docs/UNO-AUDIT.md: RecordModel drove seven panes and RecordPage.xaml was 903 lines,
with the pane width clamp and focus handling in code-behind. Measured: the Toolkit 11 presenter renders on desktop
(it did not with Toolkit 9.1.3, MenuFlyout.xaml), and a pixel DrawerLength is min(length, window), the old clamp.
Tradeoff: a drawer covers the sidebar with a scrim, where the SplitView pane sat beside it; a save failure shows in the
drawer's own Not saved bar.

## D9. Case details stay correctable until sign-off, and every correction is logged (2026-11-04, proposed)

Decision: Edit case details (Preparation drawer, until the record is signed) reopens the case form filled from the
case: patient (name, species, breed, weight, age, owner), procedure, team and schedule. Each changed detail is kept as a
correction (time, detail, before, after) in the case, listed in Visit history and exported in the CSV and the summary.
What was recorded never changes: readings, doses with the volume drawn up at the time, stage, recovery. Everything
calculated from the case follows the correction: the dose calculator and its reference range, the vitals alarms for
the species, Today's timeline.
Reason: a weight mistyped at booking drives every dose and could not be corrected once the case was under anesthesia
(docs/COMPLETENESS.md, Data Major). "Build small, cut large": the form already existed.
Tradeoff: no per-field reason or second signature for a correction; the signed record stays read-only.
