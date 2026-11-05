# Clover shell and tokens (shared by Today and Patient care)

Source prototypes (design-component exports, values copied literally from inline `style` and the `<helmet><style>` block):

- `Downloads\Today · dashboard (C cards)-html\App-Today.dc.html` (artboard 1440 x 1380)
- `Downloads\Patient care · Bella-html\App-Bella.dc.html` (artboard 1440 x 1084)

Both files share an identical `<style>` block, sidebar and top-bar search/bell markup. Only the active nav item and the header's left side differ.

Screen sheets: `today-dashboard-values.md`, `patient-care-values.md`.

Conventions in these sheets:

- All px values are CSS px = XAML effective pixels at 100% scale.
- CSS `padding: a b c d` = top right bottom left. XAML `Thickness` is left,top,right,bottom; convert carefully.
- "tri-tone border" = `border: 1px solid #E4E4E6; border-top-color: #ECECEE; border-bottom-color: #D9D9DC` (see Borders). XAML `Border` has one `BorderBrush`; reproduce with a vertical `LinearGradientBrush` border (top #ECECEE, sides #E4E4E6, bottom #D9D9DC) or accept the #E4E4E6 average.
- `(est. from PNG)` marks a value measured from the 1x reference render, used only where the markup has no explicit value.
- Box sizes include border (`box-sizing: border-box`) unless stated.

---

## 1. Fonts

Loaded from Google Fonts:
`Hanken+Grotesk:wght@400;500;600` · `Martian+Mono:wght@500` · `Newsreader:opsz,wght@6..72,400`

| Family | Weights used | Role | Fallback in CSS |
|---|---|---|---|
| Hanken Grotesk | 400, 500, 600 | All UI text (root font) | system-ui, sans-serif |
| Martian Mono | 500 only | Times, durations, numeric readouts, status-strip labels, axis labels, keycap | ui-monospace, monospace |
| Newsreader | 400 (optical size axis 6..72, auto) | Page titles only (h1) | Georgia, serif |

Root: `font-family: 'Hanken Grotesk'; color: #1B212F; -webkit-font-smoothing: antialiased`.
`font-variant-numeric: tabular-nums` is set on counts, weights, percentages and Martian Mono values (Martian Mono is already monospaced; for Hanken Grotesk use `Typography.NumeralAlignment="Tabular"` or the `tnum` feature).

### Type ramp (shared names; size/line-height px, weight, tracking)

| Role name | Family | Size / LH | Weight | Letter-spacing | Color | Where |
|---|---|---|---|---|---|---|
| display-title-l | Newsreader | 56 / 60 | 400 | -0.02em (-1.12px) | ink | Patient name h1 (Bella) |
| display-title | Newsreader | 40 / 46 | 400 | -0.015em (-0.6px) | ink | Greeting h1 (Today), `text-wrap: balance` |
| mono-hero | Martian Mono | 64 / 68 | 500 | -0.05em (-3.2px) | ink | Today hero timer "1:12" |
| mono-hero-s | Martian Mono | 56 / 60 | 500 | -0.05em (-2.8px) | ink | Bella anesthesia timer "1:12" |
| mono-vital | Martian Mono | 48 / 52 | 500 | -0.05em (-2.4px) | ink | "94", "97" |
| mono-stat-l | Martian Mono | 30 / 36 | 500 | -0.03em | ink | Weight "18.4" |
| mono-stat | Martian Mono | 28 / 32 | 500 | -0.03em | ink | "7h 16m" |
| mono-stat-s | Martian Mono | 24 / 30 | 500 | -0.03em | ink | Next dose "13:10" |
| mono-timer-card | Martian Mono | 20 / 24 | 500 | -0.02em, tabular | ink | Card status strip timer |
| mono-value | Martian Mono | 18 / 26 | 500 | tabular | ink | Hero stats "11:30" |
| mono-value-s | Martian Mono | 16 / 22 | 500 | 0 | ink | Bella "12:36" |
| mono-cell | Martian Mono | 14 / 18 | 500 | tabular | ink | Due-chip time |
| mono-num | Martian Mono | 13 / 18 | 500 | tabular | ink | Care-time "4h 13m" |
| mono-label | Martian Mono | 12 / 16 | 500 | +0.02em, UPPERCASE | status color | Status-strip label "ANESTHESIA" |
| mono-meta | Martian Mono | 12 / 18 (or 16) | 500 | 0 | muted | "4.05 mL · IV · 11:40" (18), "since 11:30" (16) |
| mono-axis | Martian Mono | 11 / 16 | 500 | 0 | muted | Gantt axis, "Now 12:42", keycap "⌘K" |
| mono-svg-axis | Martian Mono | 10 (SVG font-size) | 500 | 0 | muted | Anesthesia record axis text |
| brand | Hanken Grotesk | 24 / 28 | 500 | -0.02em | primary #244936 | "clover" wordmark |
| h2-section | Hanken Grotesk | 20 / 28 | 600 | 0 | ink | Section titles |
| h3-card-name | Hanken Grotesk | 20 / 24 | 600 | 0 | ink | Patient card name |
| title-m | Hanken Grotesk | 18 / 24 | 600 | 0 | ink | "Cefazolin" |
| h3-card | Hanken Grotesk | 16 / 24 | 600 | 0 | ink | Card titles (Anesthesia record, Circulation, Respiration) |
| body-l | Hanken Grotesk | 16 / 24 | 400 (500 for breed) | 0 | muted / ink | Greeting subtitle; "English bulldog" (500) |
| title-s | Hanken Grotesk | 15 / 20 | 600 | 0 | ink | Row task title, hero card title, alert title |
| body | Hanken Grotesk | 14 / 20 | 400 / 500 / 600 | 0 | ink / muted | Nav, buttons (L), body text |
| body-s | Hanken Grotesk | 13 / 18 | 400 / 500 / 600 | 0 | muted | Secondary lines, chips, legends of status |
| button-s | Hanken Grotesk | 13 / 20 | 600 | 0 | — | 32/36 px buttons |
| caption | Hanken Grotesk | 12 / 16 | 400 / 500 / 600 | 0 | muted | Sublabels, badges, counts, legends, table header |
| micro | Hanken Grotesk | 11 / 14 | 500 / 600 | 0 | muted / error | "Due" / "Late" label in time chip |

Letter-spacing conversion to XAML `CharacterSpacing` (1/1000 em): -0.05em = -50, -0.03em = -30, -0.02em = -20, -0.015em = -15, +0.02em = 20.

---

## 2. Colors

### Neutrals

| Token | Value | Role |
|---|---|---|
| ink | `#1B212F` | Primary text, ink icons, focus outline, "Now" pill bg, now line |
| ink-muted | `#5C616C` | Secondary text, placeholders, axis labels, counts |
| icon-muted | `#6B707B` | Default stroke for 16/18 px line icons |
| ink-faint | `#A3A6AE` | Breadcrumb slash, dashed "ASA pending" border |
| page | `#F3F3F2` | App background behind the frame (sidebar sits on it); also inset/tray header fill, segmented-control track, `.bs:hover`, `.tg:hover` |
| surface | `#FCFBF9` | Main frame, cards, secondary buttons, active nav item, chips |
| surface-2 | `#F8F8F7` | Search field fill, patient-card outer shell, table row hover |
| chip-neutral | `#E9EAEF` | Clinic monogram tile, weight / pet-parent value chips |
| media-placeholder | `#E9E9EB` | Photo frame background |
| avatar-green | `#DCE7E0` | User avatar circle (text #244936) |
| track | `#EDEDEF` | Empty progress tracks, upcoming stepper segments, knob gradient bottom |
| pressed | `#ECECEE` | `.tg:active` fill (also border-top tone) |
| on-primary | `#FFFFFF` | Text/icons on primary button, Now pill text, ring around gantt dots |
| ink-a05 | `rgba(27,33,47,.05)` | Nav hover, segment hover |
| ink-a08 | `rgba(27,33,47,.08)` | Gantt hour gridlines |
| ink-a30 | `rgba(27,33,47,.3)` | Link underline (rest) |

### Borders

| Token | Value | Role |
|---|---|---|
| border | `#E4E4E6` | Default 1 px border (sides) |
| border-top | `#ECECEE` | Top edge of tri-tone border (lighter) |
| border-bottom | `#D9D9DC` | Bottom edge of tri-tone border (darker); also `.bs` bottom |
| divider | `#EBEBED` | Internal 1 px dividers (rows, stat columns, toggle top, card sections, chart gridlines) |
| tray-divider | `#E6E6E8` | Bottom border of patient-card header tray |
| segment-border | `#E8E8EA` | Segmented-control track border |
| knob-border | `#D4D4D8` | Stepper knob border |
| hover-border | `#CFCFD4` / top `#DCDCE0` / bottom `#C4C4CA` | Patient-card hover border (see section 6 note) |

### Brand / primary

| Token | Value | Role |
|---|---|---|
| primary | `#244936` | Primary button fill, logo, brand text, active nav icon stroke |
| primary-strong | `#1C3A2B` | Primary button border and hover fill |

### Status (phase) colors

| Phase | Solid (bars, dots, lines) | Deep text | Soft fill | Soft border | Stepper segment |
|---|---|---|---|---|---|
| Anesthesia / live / success | `#0F8A57` | `#005527` | `#D5ECE0` | `#B8DDC8` | `#BFE3CF` |
| Prep | `#C97A2E` | `#7A4A1C` (icon on soft) | `#FCE3C6` | — | `#FCE3C6` |
| Recovery | `#5F78E2` | ink | `#E5E8F9` | `#CDD3F3` | `#D3D9F7` |
| Waiting | `#7E828B` | ink-muted | — | — | — |
| Planned / booked | hatch (see 2.1) | ink-muted | hatch | `#E0E0E3` | hatch |
| Error / overdue | `#B3261E` | `#8C1D18` (strong) | `#FBE6E2` | `#F1C9C2` | — |

Extra status values:

- `#3F5249` muted text on the green soft surface (Bella: "since 11:30", "Last reading", "Next due").
- `#F6D2CC` error icon tile (Bella alert).
- `#7A3B35` secondary text on the error soft surface (Bella alert).
- Pulse ring: `rgba(15,138,87,.18)` (3 px) to `rgba(15,138,87,.08)` (5 px).
- "Last reading" dot in Today hero uses ink-muted `#5C616C`; "Pepper" row dot in Due next uses `#5C616C` (signed out).

Chart colors (Bella anesthesia record and sparklines): HR line `#0F8A57`, BP bars `#5F78E2`, SpO2 sparkline `#5C616C`, due band `#FBE6E2` with dashed line `#B3261E`, gridlines `#EBEBED`, axis text `#5C616C`, point halo stroke `#FCFBF9`.

### 2.1 Gradients and patterns

| Name | CSS (exact) | Used on |
|---|---|---|
| hero-gradient | `linear-gradient(110deg, #E5E8F9 0%, #DCEAF0 52%, #D5ECE0 100%)` | Today "Right now" section bg (border `#DDE3EE`) |
| planned-hatch | `repeating-linear-gradient(135deg, rgba(255,255,255,.75) 0 3px, rgba(27,33,47,.22) 3px 4px)` | Gantt planned bars, legend swatch |
| booked-hatch | `repeating-linear-gradient(135deg, #F4F4F3 0 5px, #E8E8EB 5px 6px)` | "Booked" status strip on a patient card |
| step-done-hatch | `repeating-linear-gradient(135deg, #DCDCE0 0 1px, #F0F0F1 1px 4px)` | Stepper "Booked" segment |
| knob-gradient | `linear-gradient(#FFFFFF, #EDEDEF)` (top to bottom) | Stepper knob |
| sweep | `linear-gradient(90deg, rgba(213,236,224,0), #D5ECE0 45%)` | ECG scanning eraser (20 px wide) |

XAML note: a 135deg repeating stripe = 45-degree diagonal stripes running from top-left to bottom-right direction of the gradient axis. Period: planned 4 px (3 px light + 1 px dark), booked 6 px (5 + 1), step-done 4 px (1 px dark + 3 px light). Use a tiled `ImageBrush`, a Skia/Composition surface brush, or a `LinearGradientBrush` with `SpreadMethod="Repeat"` and `MappingMode="Absolute"` (StartPoint 0,0 EndPoint p/√2, p/√2 where p is the period) if the target supports Repeat.

---

## 3. Radii

Observed radius values (px): 3, 4, 6, 8, 9, 10, 11, 12, 14, 16, 17, 18, 22, 24, 26, 999 (pill/circle).

| Radius | Use |
|---|---|
| 999 | Dots, avatar, pulse dot, gantt "now" dots, stepper knob |
| 26 | Bella hero tray |
| 24 | Today "Right now" hero section |
| 22 | Main frame; Today hero card outer glass; patient card outer; stretch-link focus ring |
| 18 | Patient card inner tray; header tray bottom corners (0 0 18 18); Bella metric cards |
| 17 | Toggle focus outline bottom corners (0 0 17 17) |
| 16 | Today hero inner card; Bella photo panel; Bella alert banner |
| 14 | Clinic switcher; Bella weight card, anesthesia card, big CTA; insight card |
| 12 | Nav items; search field; 40 px buttons; monitoring pill; photo thumbnail; status strip |
| 11 | Segmented-control track |
| 10 | 32/36 px buttons; icon tiles 32-36 px; due-time chip; team chip (Bella) |
| 9 | Hero card icon tile (30 px) |
| 8 | Badges/chips 24-28 px; segmented buttons |
| 6 | Keycap; Now pill; gantt track (6 = half of 12) |
| 4 | Gantt segments; care-time bars; chart BP bars (rx 2); due band rx 4 |
| 3 | Stepper segments; planned legend swatch |

---

## 4. Shadows (exact CSS)

| Name | CSS | Use |
|---|---|---|
| frame | `0 1px 2px rgba(27,33,47,.05), 0 12px 32px -24px rgba(27,33,47,.25)` | Main content frame |
| xs | `0 1px 2px rgba(27,33,47,.05)` | Clinic switcher, New patient, card tray, header tray, Bella hero tray, Bella metric cards |
| nav-on | `0 1px 2px rgba(27,33,47,.06)` | Active nav item |
| seg-on | `0 1px 2px rgba(27,33,47,.12)` | Selected segment |
| card | `0 2px 3px -1px rgba(27,33,47,.07)` | Patient card outer |
| glass | `0 10px 28px -16px rgba(27,33,47,.28)` | Today hero card glass shell |
| float-btn | `0 2px 8px rgba(27,33,47,.18)` | "Pet-parent view" button over photo |
| knob | `0 1px 3px rgba(27,33,47,.2)` | Stepper knob |
| gantt-dot | `0 1px 3px rgba(27,33,47,.25)` | Gantt now dots |
| inset-photo | `inset 0 1px 3px rgba(0,0,0,.2)` | Photo frames (overlay span) |
| inset-strip | `inset 0 1px 3px rgba(0,0,0,.12)` | Status strip (live/recovery), Bella anesthesia card |
| inset-strip-soft | `inset 0 1px 3px rgba(0,0,0,.1)` | Booked status strip |
| ring-bell | `0 0 0 2px #FCFBF9` | Ring around red bell dot |
| pulse | `0 0 0 3px rgba(15,138,87,.18)` (animated, see 6) | Live dots |

XAML: outer shadows map to `ThemeShadow` poorly; use Uno Toolkit `ShadowContainer` with these exact offset/blur/spread/color values. Inset shadows: Toolkit `ShadowContainer` supports `IsInner="True"`.

---

## 5. Spacing scale observed

Gaps/paddings that recur (px): 2, 3, 4, 6, 8, 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 40, 48.
Dominant rhythm: 4-based with 2/6/10/14/18 half-steps. Section spacing: 16 (header to grid), 24 (card grid gap), 28 / 32 / 40 / 48 (between major sections).

---

## 6. Interaction and motion (shared `<style>` block, verbatim intent)

Easing token: `cubic-bezier(.2,0,0,1)` (call it `ease-standard`; XAML `KeySpline="0.2,0 0,1"`). `ease-out` = CSS ease-out (KeySpline 0,0 0.58,1).

| Selector | Behavior |
|---|---|
| `.btn` | transition bg 150ms ease-out, border-color 150ms ease-out, transform 150ms ease-standard. `:active` scale(.96). Cursor pointer. |
| `.bp` (primary) | bg `#244936`, text `#FFFFFF`, border 1px `#1C3A2B`. Hover bg `#1C3A2B`. |
| `.bs` (secondary) | bg `#FCFBF9`, text ink, border 1px `#E4E4E6`, bottom `#D9D9DC`. Hover bg `#F3F3F2`. |
| focus-visible (`.btn`, `.nav`, `.lnk`, `.fld:focus-within`) | outline 2px solid `#1B212F`, offset 2px (outside). |
| `.nav` | no underline, ink text, bg transition 150ms ease-out. Hover bg `rgba(27,33,47,.05)`. `.nav.on` and `.nav.on:hover` bg `#FCFBF9`. |
| `.lnk` | underline, decoration color `rgba(27,33,47,.3)`, underline offset 4px; hover decoration `#1B212F` (150ms ease-out). |
| `.seg` | bg transition 150ms; hover `rgba(27,33,47,.05)`. |
| `.row` | bg transition 150ms; hover `#F8F8F7`. |
| `.fld input` | no border/outline, transparent, inherits font; placeholder `#5C616C`. Whole field gets the focus outline via `:focus-within`. |
| `.sh` (patient card) | cursor pointer; border-color transition 160ms ease-out on card and `.tray`; hover border `#CFCFD4`, top `#DCDCE0`, bottom `#C4C4CA`. |
| `.stretch` | card-name link; `::after` covers the whole card (inset -1px, radius 22) so the full card is clickable. Focus-visible: 2px ink outline offset 3px around the card. |
| `.tg` (Show details toggle) | bg 150ms ease-out; hover `#F3F3F2`; active `#ECECEE`; focus-visible outline 2px ink, offset -3px (inset), radius 0 0 17 17. |
| `.chev` | rotate transition 200ms ease-standard; open: rotate(180deg) over 300ms. |
| `.cx` (collapsible) | CSS grid rows 0fr to 1fr. Collapse 200ms ease-standard; expand 300ms ease-standard. Child: opacity 0 + translateY(-8px), hide 150ms ease-out; show opacity 1 + translate 0 over 240ms ease-standard with 60ms delay. `.cx.late` child delay 160ms instead of 60ms. |
| `clv-pulse` | `.pulse` 2s ease-in-out infinite: box-shadow ring 0 0 0 3px rgba(15,138,87,.18) at 0%/100%, 0 0 0 5px rgba(15,138,87,.08) at 50%. (XAML: animate a ring ellipse behind the 8 px dot from 14 px to 18 px diameter and opacity .18 to .08.) |
| `clv-sweep` | `.sweep` absolute, top 0 bottom 0, width 20px, gradient 90deg transparent to `#D5ECE0` at 45%; animates `left` from -20px to 100% over 2.6s linear infinite. It erases the ECG line as it passes (looks like a monitor refresh). |

Reduced motion (`prefers-reduced-motion: reduce`): all transitions above set to none, `.sweep` display none, `.pulse` animation none, `.btn:active` transform none.

Prototype quirks (inline style beats the class rule, so these hovers never render in the HTML; the intended values are above):

- Ghost icon buttons (collapse sidebar, account menu) set inline `background: transparent; border-color: transparent`, so `.bs:hover` does nothing on them. Only the press scale shows.
- Segmented buttons set inline `background`, so `.seg:hover` does nothing.
- The `.tg` toggle sets inline `background: transparent`, so its hover/active fills do not show.
- Patient cards set inline border colors, so `.sh:hover` border change does not show.
- Bella breadcrumb "Patients" link sets inline `text-decoration-color: transparent`, so it never shows an underline.
Decide per control whether to implement the intended hover (recommended) or match the literal render.

---

## 7. Page frame (both screens)

```
Root  w=1440  h=1380 (Today) | 1084 (Bella)  bg=#F3F3F2  grid columns: 256 | 1fr (=1184)
├ Sidebar <aside>  w=256  pad=24 16 16 16 (T R B L)  flex column gap=12  (content width 224)
└ Main frame <main>  margin=12 12 12 0  -> w=1172, h=root-24 (1356 | 1060)
    pad=36 40 40 40  bg=#FCFBF9  border=1 tri-tone  radius=22  shadow=frame  overflow-y=auto
    content width = 1172 - 2 - 80 = 1090
```

---

## 8. Sidebar (identical on both screens except active item)

```
Sidebar  w=256  pad T24 R16 B16 L16  column gap=12  bg=transparent (page #F3F3F2 shows)
├ Brand row  h=40  pad L8 R4  justify=space-between  align=center
│   ├ Logo + wordmark  gap=10
│   │   ├ Clover logo  26x26, filled #244936 (4 rounded leaves, see Icons)
│   │   └ "clover"  Hanken 24/28 500, -0.02em, #244936
│   └ Collapse button (ghost)  32x32 radius=10  bg/border transparent  icon panel-left 18 stroke #6B707B 1.75
├ Clinic switcher (.btn .bs)  margin-top=8 (+12 gap)  h=56  pad L10 R12  radius=14  gap=12  shadow=xs  text-align left
│   ├ Monogram tile  34x34 radius=10 bg=#E9EAEF  "CP" Hanken 12/16 600 ink, centered
│   ├ Text column (flex 1)
│   │   ├ "Clinique Plateau"  14/20 600 ink
│   │   └ "2 suites · Montréal"  12/16 400 #5C616C
│   └ chevrons-up-down icon 16 stroke #6B707B 1.75
├ New patient (.btn .bs)  h=40 (full width 224)  radius=12  gap=8  center  14/20 500  shadow=xs
│   └ plus icon 16 stroke #1B212F 2, then "New patient"
├ Main nav  margin-top=12  column gap=2
│   └ Nav item (x7)  h=40  pad 0 12  radius=12  gap=12  14/20 500 ink  border 1 transparent
│       ├ icon 18, stroke 1.75, #6B707B (inactive) / #244936 (active)
│       ├ label
│       └ count (optional) margin-left auto, 12/16 600 #5C616C tabular
│     Active (.nav.on): bg #FCFBF9, tri-tone border, shadow nav-on.
│     Items: Today (grid) · Patients (paw) count 4 · Agenda (calendar) · Anesthesia (activity) count 1 · Records (file-text) · Messages (message-square) count 2 · Reports (bar-chart)
│     Today screen: "Today" active. Bella screen: "Patients" active.
├ Footer nav  margin-top=auto  column gap=2
│   ├ Settings (settings-2 / sliders icon)
│   └ Help center (circle-help)
├ Monitoring pill  pad 10 12  radius=12  bg #FCFBF9  tri-tone border  gap=10  (h = 18+20+2 = 40)
│   ├ Pulse dot 8x8 #0F8A57, ring 3px rgba(15,138,87,.18), animated (clv-pulse)
│   ├ "Monitoring on"  13/18 600 ink, nowrap
│   └ "1 min ago"  margin-left auto, 12/16 #5C616C
└ User row  pad T6 R4 B0 L8  gap=10  align center
    ├ Avatar 34x34 circle bg #DCE7E0  "AO" 13/16 600 #244936
    ├ Column: "Dr. Okafor" 14/20 600 · "Veterinarian" 12/16 #5C616C
    └ Account menu (ghost) 32x32 radius=10, ellipsis-vertical 18 #6B707B 1.75
```

Vertical positions (est. from PNG, Today): brand row y 24-64, switcher 84-140, New patient 152-192, nav items start y 216, each 40 + 2 gap. Footer block is bottom-aligned: user row bottom at root height - 16.

---

## 9. Top bar right cluster (both screens)

```
Right cluster  flex gap=10  flex-shrink 0
├ Search field (.fld, a <label>)  w=340 h=40  pad L12 R8  radius=12  bg #F8F8F7  tri-tone border  gap=10  14/20
│   ├ search icon 18 stroke #6B707B 1.75
│   ├ input (flex) placeholder "Search patients, owners, records" color #5C616C, text ink
│   └ Keycap "⌘K"  Martian Mono 11/16 (500)  pad 2 6  radius=6  bg #FCFBF9  border 1 #E4E4E6  text #5C616C  (h = 16+4+2 = 22)
└ Bell button (.btn .bs)  40x40 radius=12  aria-label "Alerts, 1 new"
    ├ bell icon 18 stroke #1B212F 1.75
    └ Unread dot  8x8 #B3261E circle, absolute top=8 right=9, ring 0 0 0 2px #FCFBF9
```

Focus: the whole search field gets the 2px ink outline (offset 2) when the input is focused.
Header row: Today uses `align-items: flex-start` (title block left, cluster top-aligned); Bella uses `align-items: center` (breadcrumb left). Both `justify-content: space-between; gap: 24px`.

---

## 10. Shared components

### Buttons

| Variant | Height | Padding (L/R) | Radius | Text | Fill / border | Icon |
|---|---|---|---|---|---|---|
| primary-s | 32 | 14 | 10 | 13/20 600 white | `.bp` | trailing 16, stroke #FFFFFF 2, gap 6 |
| primary-m | 36 | 14 | 10 | 13/20 600 | `.bp` | none |
| primary-l | 40 | 18 | 12 | 14/20 600 | `.bp` | trailing 16 white, gap 6 |
| primary-xl | 48 | 18 | 14 | 15/20 600 | `.bp`, full width | trailing plus 16 white |
| secondary-m | 36 | 14 | 10 | 13/20 600 ink | `.bs` | optional trailing 16 ink stroke 2 |
| secondary-l | 40 | 18 | 12 | 14/20 600 ink | `.bs` | — |
| icon-secondary | 40x40 / 32x32 | — | 12 / 10 | — | `.bs` | 18 (40) or 16 (32) |
| icon-ghost | 32x32 | — | 10 | — | transparent bg + border | 18 stroke #6B707B 1.75 |

All buttons: `display:inline-flex; align-items:center; justify-content:center; gap:6px; white-space:nowrap`, 1 px border included in height. States: hover (bp `#1C3A2B`, bs `#F3F3F2`), pressed scale .96 over 150ms ease-standard, focus-visible 2px ink outline offset 2.

### Badge / chip (24 px)

`h=24 pad 0 9 radius=8 12/16 600`. Variants:
- outline: bg `#FCFBF9`, border 1 `#E4E4E6`, text ink ("ASA II", "ASA I").
- dashed-pending: no fill, border 1 dashed `#A3A6AE`, text `#5C616C` ("ASA pending").
- filled-neutral: bg `#E9EAEF`, no border, tabular ("18.4 kg", "M. Lavoie"; ellipsis on overflow).

### Status chip (soft)

- Error chip (Today hero): h=26 pad 0 8 radius=8 gap=6, bg `#FBE6E2`, border 1 `#F1C9C2`, text `#B3261E` 13/18 600, triangle-alert 14 stroke #B3261E 2.
- Live chip (Bella): h=28 pad 0 10 radius=8 gap=8, bg `#D5ECE0`, border 1 `#B8DDC8`, text `#005527` 13/18 600, leading dot 6x6 `#0F8A57`.

### Link (`.lnk`)

`inline-flex gap=6 14/20 600 ink`, underline color rgba(27,33,47,.3), offset 4, trailing arrow-right 16 stroke ink 2.

### Dots

6x6 (legend-in-text, row meta), 8x8 (legend, live pulse). Hollow variant: 8x8 border 1.5 px status color, no fill (recovery / booked).

---

## 11. Icons

All line icons are Lucide (viewBox 24, `fill="none"`, round caps and joins). The exact SVG path data is in the HTML and can be pasted into a XAML `PathIcon`/`Path` (`Data` mini-language supports M L H V Q C A Z; convert `<circle>`/`<rect rx>` to `EllipseGeometry`/`RectangleGeometry` in a `GeometryGroup`). Fluent mapping is the nearest Segoe Fluent / Fluent UI System icon (verify glyph before use).

| Use | Lucide name | Glyph | Size / stroke / color | Nearest Fluent |
|---|---|---|---|---|
| Logo | custom (filled) | 4 rounded leaves (each a quarter-circle-rounded square pointing to center), fill #244936 | 26 | none, ship as Path |
| Collapse sidebar | panel-left | rounded square with left vertical divider | 18 / 1.75 / #6B707B | PanelLeft |
| Clinic switcher | chevrons-up-down | up chevron over down chevron | 16 / 1.75 / #6B707B | ChevronUpDown |
| New patient, Record, Record observations | plus | + | 16 / 2 / ink or white | Add |
| Today | layout-grid | 2x2 rounded squares | 18 / 1.75 | Grid |
| Patients | paw-print | 3 toe circles + pad | 18 / 1.75 | AnimalDog / paw (no exact Segoe glyph) |
| Agenda | calendar | calendar with 2 rings + header line | 18 / 1.75 | Calendar |
| Anesthesia, Vitals, hero icon | activity | ECG/pulse line | 18 / 1.75 (16 / 2 #005527 in hero tile) | Pulse |
| Records | file-text | page with folded corner + 3 lines | 18 / 1.75 | DocumentText |
| Messages | message-square | square speech bubble | 18 / 1.75 | Chat |
| Reports | bar-chart-3 (chart-column) | L axis + 3 bars | 18 / 1.75 | DataBarVertical |
| Settings | settings-2 | two sliders with knobs | 18 / 1.75 | Options |
| Help center | circle-help | ? in circle | 18 / 1.75 | QuestionCircle |
| Account menu | ellipsis-vertical | 3 vertical dots | 18 / 1.75 | MoreVertical |
| Search | search | magnifier | 18 / 1.75 / #6B707B | Search |
| Alerts | bell | bell with clapper | 18 / 1.75 / ink | Alert |
| Links | arrow-right | → | 16 / 2 / ink | ArrowRight |
| Report / Pet-parent view | arrow-up-right | ↗ | 16 / 2 / ink | ArrowUpRight |
| Overdue | triangle-alert | warning triangle | 14 or 16 / 2 / #B3261E; 18 / 2 / #8C1D18 (alert) | Warning |
| Results ready | check | ✓ | 16 / 2 / #005527 | Checkmark |
| Show details | chevron-down | ˅ | 16 / 2 / #6B707B | ChevronDown |
| Insight | lightbulb | bulb | 16 / 2 / #7A4A1C | Lightbulb |
| Procedure row | stethoscope | stethoscope | 16 / 1.75 / #6B707B | Stethoscope |
| Weight row | weight | kettlebell-like weight | 16 / 1.75 / #6B707B | Scales (approx) |
| Pet parent row | user-round | head + shoulders | 16 / 1.75 / #6B707B | Person |
| Back | arrow-left | ← | 18 / 1.75 / ink | ArrowLeft |
| Preparation | clipboard-check | clipboard with check | 18 / 1.75 | ClipboardTask |
| Medical history | history | counter-clockwise clock arrow | 18 / 1.75 | History |
| Lab results | flask-conical | conical flask | 18 / 1.75 | Beaker |
| Medications | pill | capsule | 18 / 1.75 | Pill |
| Team | users | two people | 15 / 1.75 / #6B707B | People |
| Administer dose, Latest medication | syringe | syringe | 16 / 2 / white; 15 / 1.75 / #6B707B | (none in Segoe; ship as Path) |
| Circulation | heart-pulse | heart with pulse line | 16 / 1.75 / ink | HeartPulse |
| Respiration | wind | 3 curling wind lines | 16 / 1.75 / ink | WeatherDuststorm / Wind |

Recommendation: use the Lucide paths verbatim (Lucide is ISC licensed) so stroke weight matches; Fluent glyphs have different stroke metrics.
