# Production readiness: Vigil v2 (Clover)

Branch `feat/production-readiness`. Phases 1-2 (Uno.Sdk 7.0.0-dev.115, five heads, SkiaSharp 4) are in 0d1ab35 and
9cea5fe. This file covers Phases 3-4: UI hardening (tokens, responsive layouts, themes, states, accessibility) and
the evidence for it.

## Baseline (2026-10-30, at 9cea5fe)

Method: code read of every page and control, then a Debug desktop run under Xvfb through the new verification hooks
(`--vigil-theme`, `--vigil-route`), live-resized through 360, 599, 600, 800, 1080 and 1920 px at 640 px high, light
theme. Captures: `vigil-v2-evidence/baseline/` in the session scratchpad (not in the repo).

**Pages**: shell (sidebar, drawer below 760), Today, Patients, Agenda, Record (+7 side panes), Dose, Parent preview,
Settings; sheets New patient, Sign off, Menu.

**Responsive** (four categories at 360 / 600 / 800 / 1080 / 1920):
- Two breakpoint sets were mixed: a custom `ShellLayout` (400 / 760 / 1080 / 2400, so "Widest" never happened) for the
  shell, Today and Record, the Toolkit defaults (300 / 600 / 800 / 1080) elsewhere. 87 markups used `ShellLayout`.
- Patients, Agenda, Settings, Dose and Parent preview gave phones the 40 px desktop inset (`Narrow=` where `Normal=`
  was meant): their phone layout started at 300 px, not 600.
- 360: Today's hero card clipped (fixed 448 px column), the top bar's 340 px search field clipped on every page,
  Settings squeezed both name fields into one row ("First na…"), Agenda's card header overprinted its time range,
  Dose stacked the drug list into a `*` row that a 640 px window left 0 px tall (no drug could be picked).
- 1080: Today's hero gantt overprinted its hour labels (≈190 px for 12 labels), Due next had a ≈70 px task column.
- 1920: no maximum content width; cards and forms stretched to 1650 px.
- 640 px high: the sidebar had no scrolling, so Monitoring, the user row and (on the rail) Help were cut off.
- 0 `utu:ResponsiveView`; navigation changed by Visibility only.

**Rendering**: under Xvfb the Uno 7 X11 host's OpenGL backend (Mesa llvmpipe) left parts of the window unpainted
(black) after some resizes, and Parent preview black at every width. With the software backend (DEBUG switch
`VIGIL_X11_RENDERER=software`) every capture settles, so this is the GL path on this machine, not the pages; a real-GPU
desktop was not available to confirm. `PetBackdrop` (an `SKCanvasElement`) also read DependencyProperties and
resources from the render thread and built blur filters per frame (skiasharp-uno gotchas); its hero and pulse modes
were unused, as were `VitalsStrip` and `StripRenderer`.

**Themes**: Light and Dark ThemeDictionaries complete in `Themes/Tokens.xaml` and `Styles/CloverPalette.xaml`;
System/Light/Dark selector in Settings through `IThemeService` (persisted by the theme service, restored at launch);
Skia/code controls re-read colours on `ActualThemeChanged`. Gaps: hex literals in code (`CareRuler` fallbacks,
`PetBackdrop` veil colour) and in `RecordPage` (acrylic tint and fallback).

**Tokens**: radii, colours, type and buttons were tokens; spacing was not. ~1,000 literal layout values in pages and
controls (Spacing 188, Padding 79, Margin 115, CornerRadius 63, Rectangle radii 88, plus widths); 22 inline shadow
collections repeating 9 design shadows.

**States**: FeedView Progress / None / Error with Retry on Today, Patients, Agenda (intervals), Record, Parent preview.
Gaps: Agenda "In preparation" and "Next observation" errors had no Retry; the Dose formulary list had no states.

**Accessibility**: icon buttons, nav items, cards and rows carry `AutomationProperties.Name`; disabled "Coming soon"
items announce it; status uses labels and icons, not colour alone; focus visuals 2 px ink. Touch targets follow the
design (40 px icon buttons and nav items): SPEC-v2 user decision 5 retired SPEC.md's 56 px gloved-hand rule ("Not a
tablet app"), so it is not re-imposed here.

**Other**: a stray PowerShell "`n" between two elements of BoardPage.xaml; sheets and the Record side pane close when
the window is resized (light dismiss), which is WinUI behaviour, not a defect.

## Changes (Phases 3-4)

### Tokens (`Styles/Layout.xaml`, `Themes/Tokens.xaml`)
- New hand-authored `Styles/Layout.xaml`, merged last in `App.xaml`: the design's spacing scale (`Space2` … `Space48`,
  shell-and-tokens.md §5, named by value like the radii), its Thickness forms (`Inset*`, `InsetX*`, `InsetY*`,
  `GapTop*` …), page insets per responsive category (`PageInsetNarrow/Normal`, header, body, sheet, pane), content
  widths (`ContentMaxWidth` 1280, form, settings, reading, sheet, phone preview), component insets and sizes.
- `Themes/Tokens.xaml`: the nine design shadows (§4) as shared `ShadowCollection`s (`ShadowFrame`, `ShadowXs`,
  `ShadowNavOn`, …) replacing 22 inline collections; the missing radii (11, 19, 20, tray header, toggle foot); acrylic
  tint and fallback colours per theme.
- Codemod (session tool `tokenize_v2.py`) moved every Spacing, Padding, Margin, CornerRadius and Rectangle radius on
  the scale to tokens, without snapping (the v2 design is matched to a prototype). What stays literal is structural or
  optical: negative bleeds, baseline nudges, chart label positions, element sizes (dots, skeleton bars, icon boxes),
  1 px hairlines, Grid star sizes.
- No hex colours in pages, controls or code-behind (`CareRuler`'s hex fallbacks and `RecordPage`'s acrylic hexes went
  to theme resources).

### Responsive (four categories, Toolkit default breakpoints 600 / 800 / 1080)
- `ShellLayout` (760 / 1080 / 2400) is gone; every page and the shell use the defaults. The only custom layout is
  `SplitLayout` (Widest at 1280) for Today's two content splits, justified in `Layout.xaml`: from 1080 the full sidebar
  leaves the page ~730 px, too narrow for a 448 px card beside a gantt.
- Shell: `utu:ResponsiveView` builds the sidebar only from Normal: Narrow (phones) gets the frame header and the
  `!Menu` drawer, Normal and Wide the 72 px icon rail, Widest the full 256 px sidebar (still collapsible). The sidebar
  scrolls when the window is short.
- Page content stops at `ContentMaxWidth` and centres at Widest; full-width headers and bodies share the column.
- Per page, see "Layouts" below. Phone fixes: stretchable top-bar search (340 px from Normal), stacked name fields
  (Settings), stacked New patient field pairs, Agenda card header, Today hero (full-width card, caption and action
  under the timer and title, 16 px stat numbers), gantt hour labels that skip instead of overprinting (`SpanPanel.
  HideOverlaps`), a two-row gantt legend, Due next's status column only from Wide, a scrolling Dose body with the
  action bar pinned, the card sparkline dropped on phones.

### Components
- `PetBackdrop` (an `SKCanvasElement` that read DependencyProperties and resources on the render thread) is replaced by
  `ImageBrush` shapes on Agenda and Parent preview; its unused hero and pulse modes, `VitalsStrip` and
  `StripRenderer` are deleted.
- The record's two warning glyphs use the Lucide triangle like every other alert (they were Segoe `FontIcon`s).

### Fixes found on the way
- Sign off measured a live case from induction to its creation time ("00:-30"); it shows a dash until anesthesia ends.
- WinAppSDK (CI): the stray "`n" in BoardPage (WMC0020) is gone and the record's acrylic brush keeps the Uno-only
  `UseCompositionEffectBrush` behind `not_win:` conditional XAML (WMC0010).
- Record reading's due clock was body ink on the primary green (~1.3:1); it now uses the button's ink.

### Layouts per page (Narrow < 600 · Normal 600-800 · Wide 800-1080 · Widest ≥ 1080)
| Page | Narrow (phone) | Normal (rail) | Wide (rail) | Widest (full sidebar) |
|---|---|---|---|---|
| Shell | frame header + menu → `!Menu` drawer | 72 px icon rail | icon rail | 256 px sidebar (collapsible), page column ≤ 1280, centred |
| Today | stacked header, full-width live card (action under title, caption under timer), stacked gantt, 1 card column, Due next without status column | 2 card columns where they fit | Due next status column, filter on the title row | header beside the top bar; hero and bottom splits side by side from 1280 |
| Patients | 1 card column, stretch search | cards by width (320 min) | 2 columns | header beside the top bar, 2-3 columns |
| Agenda | range under the card title, ruler scrolls sideways when lanes need it | 40 px insets | same | intervals and next observation side by side |
| Record | one column, no top-bar cluster | one column | identity + photo, status below, cards 2 × 2 | hero 236 \| photo \| 300, cards in one row, top-bar cluster |
| Record pane | full-width pane, CTA clock under the label, steppers in one column | 640 px pane, steppers 2-up | same | same |
| Dose | stacked drug list and calculation, body scrolls, action pinned | side by side | same | same, sheet ≤ 1040 |
| Settings | stacked name fields, vertical buttons, theme radios in one column | names side by side | same | ≤ 720 column |
| Parent preview | phone column (≤ 460) | same, centred | same | same |
| New patient / Sign off (sheets) | stacked field pairs | pairs side by side, ≤ 640 | same | same |

### Themes
Light and Dark come from the ThemeDictionaries in `Themes/Tokens.xaml` and `Styles/CloverPalette.xaml`; no page or
control sets a theme-dependent value with StaticResource or a literal colour. The app follows the system theme until
Settings → Appearance picks Light or Dark (`IThemeService`, persisted and restored at launch). `CareRuler` redraws on
`ActualThemeChanged`. Evidence: the matrix runs every page and sheet in both themes.

### States and accessibility
Every data-driven view has Progress (skeleton), None and Error templates; Error offers Retry wherever a store read can
fail (the embedded formulary explains instead). Icon buttons and rows keep their automation names; new controls are
named and localized (EN/FR, `tools/localize.py`, no duplicate `x:Uid`). Status keeps labels and icons next to colour.
Touch targets stay at the design's 40 px (SPEC-v2 decision 5); the record pane keeps its 48 px steppers and 64 px
Record reading button.

## Evidence
Captures: `vigil-v2-evidence/final/` in the session scratchpad (`<page>-<theme>-<width>.png`, contact sheets
`sheet-<page>-<theme>-a|b|c.png` for 360/599/600, 800/1080 and 1920). The theme is switched at runtime through the
window's `IThemeService`, the path the Settings selector uses. Harness (session scratchpad, not in the repo): `tools/vigil-v2-matrix.sh` drives `tools/matrix-settle.sh` over every
page at 360, 599, 600, 800, 1080 and 1920 × 640, Light and Dark, with the software X11 renderer; pages are live-resized
in one launch, sheets and the record pane get one launch per width (they close on resize). A capture is kept once two
frames 1 s apart match and nothing is left unpainted. Not covered: heights other than 640 (Xvfb has no window
manager), real GPU rendering, Android/iOS/WASM/WinAppSDK runtime, text scaling, screen readers.

## Not done
- WinAppSDK and iOS heads compile only on CI. `{Binding}` from bool to `Visibility` is used throughout v2; Uno converts
  it implicitly, WinUI does not, so the Windows head needs a runtime check.
- Touch targets were not raised to SPEC.md's 56 px gloved-hand size (superseded by SPEC-v2 decision 5).
- Disabled "Coming soon" items (search, bell, Records, Messages, Reports, Help, clinic switcher, Monitoring) stay as the
  user decided in SPEC-v2.
