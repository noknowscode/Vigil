# Clover — care, continued.

Clover is the visible product name. The project, assembly, application ID and existing local case storage retain their Vigil identity to preserve existing records and upgrades.

## Native direction

Warm cream (#F8F5ED), forest (#244936), soft sky and restrained pale lime. Public Sans provides spacious headings; monospaced clocks remain stable as time changes. Photography is expressive, clinical surfaces are quiet and opaque. The four-leaf mark is shared by the header, launcher and splash.

The patient board gives each photograph a large dedicated region. The record has one Patient care workspace that follows the saved care phase. Preparation, dose entry, observations, medication history and recovery open in a right-hand SplitView. All existing validation, record persistence, signatures and exports remain in place. Main clinical actions retain 56 px targets; viewport-sized drawers fit narrow devices. A signed record remains immutable.

The existing domain stores one patient weight and no appointment, vaccination or longitudinal history service. The dashboard labels these gaps explicitly. A single weight is drawn as one point, not a fabricated trend. The surgery strip displays the existing timestamped observations.

## Dose acknowledgement

DoseFeedback is updated only after a successful dose write. The PetBackdrop canvas blurs only decorative photography for ten seconds and returns to focus. Text, controls and alerts are separate native XAML layers. The feedback is patient-specific, never changes clinical state, and is disabled by the app/platform reduced-motion preference. Native bitmap/filter resources and animation timers are disposed or stopped on unload.

## Parent companion

ParentPreview is a responsive, read-only clinic preview with a 460 px content maximum. Its milestones come only from saved timestamps and the actual preparation checklist. A signed record never implies discharge readiness. There is no parent authentication, remote sharing, notification service or live monitor; the preview says so. No clinical score is invented.

## Photography

Bundled animal portraits are fictional AI-generated sample assets. PhotoAsset and HeroAsset are optional additions to Patient; old JSON remains readable. SampleDay assigns explicit asset IDs, including Bella the English bulldog. Existing records receive no image based on name or breed. Image IDs are restricted to bundled assets.

## Verification preview

Debug flag --clover-preview selects a separate clover-design-preview case folder before loading sample cases. It does not modify the normal case folder. --clover-size=393x852 supports native responsive inspection. Existing --vigil-seed hooks retain their legacy behavior and should not be used on a real store for visual QA.

CloverPalette.xaml and Themes/Clover.xaml are the authored design sources. Legacy generated design resources are retained for existing forms. French static labels are maintained with tools/localize.py; its allocator preserves existing resource IDs when new labels are added.

## Validation

Desktop build succeeded with zero errors. All 55 repository test sources passed against the built desktop assembly in a separate cached-package harness after the normal Uno headless build encountered environment task-host restrictions. Native UI verification covered dose save and decorative blur, observation save and reminder reset, panel dismissal, desktop views, and phone-sized parent navigation. Other target builds were not verified. Existing Pkcs/Xml dependency security warnings remain.

## Home shell, left agenda and patient identity

The home page retains a warm outer canvas and a rounded inner patient shell. Top tabs are Today’s patients and Patient care. A full-height 352 px agenda replaces the former left navigation rail on desktop. Below 1120 px it opens from the calendar button; Close or Escape restores the patient board. The patient shell and agenda scroll independently.

The agenda’s hourly ruler uses lavender anaesthesia and mint recovery blocks, quarter-hour ticks, concurrent lanes, actual recorded timestamps and a current-time marker. It updates each minute and after record changes. Short intervals retain a minimum 48 px target; printed times remain exact. Overnight care keeps the original start date while clipping the visible interval to today. Untimed preparation stays separate: gaps never imply appointment availability.

The earliest observation reminder remains pinned below the ruler. It now uses a real patient thumbnail (or the patient initial when no stored photo exists), a compact warning label, serif name, due-time detail and a directional action. Preparation entries share that compact portrait-led composition. Photographs are never inferred from patient names.

Large photo-led patient cards now show labelled species, breed, weight, age and pet parent, plus the saved ASA status, procedure, timing and care team. Weight-date context is available on the weight field. Georgia is used selectively for names and italic procedure lines on the Windows desktop; Public Sans remains the interface typeface and IBM Plex Mono handles small labels and clocks. Serif availability on other targets has not been verified.

CareGreeting is original native vector line art: an English bulldog high-five in forest, white and yellow on cream. It appears at a small size beside the home actions and in the empty state. It is decorative, excluded from interaction/accessibility navigation and never substitutes for an actual patient photo or clinical status. No continuous decorative animation was added.

## Unified patient care

Patient identity, weight/date, ASA and clinical work share one record page. The saved case status determines the primary content: preparation before induction, observations and anaesthesia chart during care, recovery milestones after anaesthesia, and a read-only summary once signed. The separate Patient/In surgery view switch and duplicated overview cards are removed. Preparation, medication history, lab context, visit history, dose entry and recovery remain accessible from native slide-out panels. Existing dose feedback remains patient-specific and decorative.

The home Patient care shortcut prioritizes the current anaesthetized patient, then a recovering patient, then the most recent visit. Its tooltip names the destination and it is disabled on an empty board. Selecting any pet card still opens that exact record. The active tab is restored correctly after navigation.

## Latest validation

The native desktop build passed. Thirteen targeted checks passed against the built desktop assembly in-process: ten agenda/time-ruler checks plus unified-phase behavior, read-only signed-state guards, patient shortcut selection and reminder photo identity. Static English/French resources are complete. Native inspection covered the 1600 × 1080 desktop and 393 × 852 compact window, agenda dismissal, tab selection and patient navigation. Earlier full-suite results above predate this refinement; the standard test host remains unavailable in this environment.
