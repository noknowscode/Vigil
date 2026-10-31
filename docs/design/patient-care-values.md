# Patient care · Bella: value sheet

Source: `Downloads\Patient care · Bella-html\App-Bella.dc.html`, reference render `Patient care · Bella@1x.png`.
Shell, sidebar, search/bell, shared tokens, buttons, icons and motion: see `shell-and-tokens.md`. This sheet covers the Bella patient page.
Artboard: **1440 x 1084**. Main frame 1172 x 1060 at x=256, y=12. Content box 1090 wide at x=297, y=49. Sidebar active item: **Patients**.
The page is static (no component state; `renderVals` returns `{}`).

Data-driven values are marked `{data}`.

---

## 1. Tokens used on this page

Shared neutrals, borders, primary, anesthesia/recovery/error status colors, plus:

| Value | Role here |
|---|---|
| `#3F5249` | Muted text on the green anesthesia card ("since 11:30", "Last reading", "Next due") |
| `#B8DDC8` | Green card border and its internal divider |
| `#F6D2CC` | Alert icon tile |
| `#8C1D18` | Alert title and icon stroke |
| `#7A3B35` | Alert secondary text |
| `#A3A6AE` | Breadcrumb "/" |
| `#EBEBED` | Chart gridlines, card dividers |

Type roles: display-title-l (Bella 56/60), mono-hero-s (56/60), mono-vital (48/52), mono-stat-l (30/36), mono-stat-s (24/30), mono-value-s (16/22), mono-label, mono-meta, mono-svg-axis, h2-section, h3-card, title-m, title-s, body-l, body, body-s, caption.
Shadows: frame, xs, nav-on, float-btn, inset-photo, inset-strip.

---

## 2. Vertical rhythm (computed, matches PNG)

| Block | y (artboard) | Height | Spacing above |
|---|---|---|---|
| Header (breadcrumb + search) | 49–89 | 40 | — |
| Hero tray | 109–561 | 452 (fixed) | 20 |
| Overdue alert | 577–641 | 64 (min) | 16 |
| Procedure header | 673–721 | 48 (text 28+2+20 = 50, est. from PNG 48–50) | 32 |
| Metric card row | 737–1032 | ~292 (tallest card) | 16 |
| Bottom padding | — | 40 + 1 border + 12 margin | — |

---

## 3. Layout skeleton

### 3.1 Header

```
Header  flex space-between align center gap 24
├ Breadcrumb nav  flex align center gap 12  14/20
│   ├ Back button (.btn .bs)  40x40 radius 12, arrow-left 18 stroke ink 1.75  (aria "Back to patients")
│   ├ "Patients" link  color #5C616C, underline transparent (never shown)
│   ├ "/"  color #A3A6AE
│   └ "Bella" {data}  600 ink (aria-current page)
└ Right cluster: search 340 + bell 40 (shared sheet section 9)
```

### 3.2 Hero tray (identity · photo · live status)

```
Section  margin-top 20  w=1090 h=452  pad 8  radius 26  bg #F3F3F2  tri-tone border  shadow xs
  grid columns 236 | 1fr (=520) | 300  gap 8   (inner 1072 x 434)
├ Identity column  w=236  pad T12 R8 B4 L12  column  (inner w=216)
│   ├ Live chip  h=28 pad 0 10 radius 8 gap 8  bg #D5ECE0 border 1 #B8DDC8  dot 6 #0F8A57  "Under anesthesia" 13/18 600 #005527  (align-self start)
│   ├ h1 "Bella" {data}  margin-top 14  Newsreader 56/60 400 -0.02em ink
│   ├ "English bulldog" {data}  margin-top 4  16/24 500 ink
│   ├ "2 years · M. Lavoie" {data}  14/20 #5C616C
│   ├ Record nav  margin-top 20  margin-left -4  column gap 2  (items identical to sidebar nav item: h40 pad 0 12 radius 12 gap 12 14/20 500, icon 18)
│   │   Preparation (clipboard-check) · Vitals (activity, ACTIVE: bg #FCFBF9, tri-tone, shadow nav-on, icon #244936) · Medical history (history) · Lab results (flask-conical) · Medications (pill)
│   └ Team chip  margin-top auto (bottom)  h=32 pad 0 10 radius 10 gap 8  bg #FCFBF9 tri-tone border  13/18 ink
│       users icon 15 stroke #6B707B 1.75 + "Dr. Okafor · S. Tremblay" {data}
├ Photo panel  w=520 h=434  radius 16  overflow hidden  bg #E9E9EB  shadow inset-photo  relative
│   ├ Backdrop image: same asset, absolute inset -40 (w+80, h+80), object-fit fill, filter blur(28px) saturate(.9), opacity .9
│   ├ Foreground image: w 100% h 100%, object-fit contain  (1026x650 -> 520x329.5, letterboxed ~52 px top and bottom showing the blurred fill)
│   ├ Overlay span: inset 0 radius 16 shadow inset 0 1px 3px rgba(0,0,0,.2)
│   └ "Pet-parent view" button  absolute right 12 bottom 12  secondary-m (h36 pad 0 14 r10 13/20 600) + arrow-up-right 16 ink, shadow float-btn 0 2px 8px rgba(27,33,47,.18)
└ Status column  w=300  column gap 8
    ├ Weight card  pad 14 16  radius 14  bg #FCFBF9  tri-tone   (h=112 computed)
    │   ├ Row space-between gap 8: "Weight at this visit" 13/18 500 #5C616C | ASA badge "ASA II" (24 px outline)
    │   ├ Value row  margin-top 4  baseline gap 6: "18.4" {data} Martian Mono 30/36 500 -0.03em | "kg" 14/20 #5C616C
    │   └ "Recorded 28 Oct, 11:05" {data}  13/18 #5C616C
    ├ Anesthesia card  flex 1 (h≈258)  pad 12 14  radius 14  bg #D5ECE0  border 1 #B8DDC8  shadow inset-strip  column gap 2
    │   ├ Row space-between gap 8
    │   │   ├ pulse dot 8 #0F8A57 (animated) + gap 8 + "UNDER ANESTHESIA" Martian Mono 12/16 500 +0.02em uppercase #005527
    │   │   └ "since 11:30" {data}  Martian Mono 12/16 500 #3F5249
    │   ├ "1:12" {data}  Martian Mono 56/60 500 -0.05em ink
    │   ├ ECG  h=32 relative overflow hidden; SVG viewBox 0 0 300 40, preserveAspectRatio none, width 100% (=270) h 32 (see 3.5); .sweep overlay
    │   ├ Divider grid  margin-top auto  pad-top 10  border-top 1 #B8DDC8  2 x 1fr gap 12
    │   │   ├ "Last reading" 12/16 #3F5249 / "12:36" Martian Mono 16/22 500
    │   │   └ "Next due" 12/16 #3F5249 / "12:41" Martian Mono 16/22 500
    │   └ Overdue line  margin-top 10  gap 6  13/18 600 #B3261E  triangle-alert 14 stroke 2  "Reading overdue 1:26" {data}
    └ CTA "Record observations" + plus  primary, h=48, pad 0 18, radius 14, 15/20 600, width 100%
```

### 3.3 Overdue alert (role=alert)

```
Banner  margin-top 16  min-h 64  pad T10 R12 B10 L14  radius 16  bg #FBE6E2  border 1 #F1C9C2  flex align center gap 14
├ Icon tile 36x36 radius 10 bg #F6D2CC, triangle-alert 18 stroke #8C1D18 2
├ Column flex 1
│   ├ "Reading overdue by 1:26" {data}  15/20 600 #8C1D18
│   └ "Last recorded 28 Oct, 12:36 · readings due every 5 minutes" {data}  13/18 #7A3B35
└ Button secondary-l "Record observations"  h40 pad 0 18 radius 12 14/20 600
```

### 3.4 Procedure header and metric cards

```
Header row  margin-top 32  flex space-between align center gap 16
├ h2 "Ovariohysterectomy (spay)" {data} 20/28 600 + p margin-top 2 "Started 11:30 · OR 1 · Dr. Okafor, S. Tremblay" {data} 14/20 #5C616C
└ Buttons gap 8
    ├ "Pre-op" secondary-l (h40 pad 0 18 r12 14/20 600)
    └ "Administer dose" primary-l + syringe 16 stroke #FFFFFF 2
Card grid  margin-top 16  columns 2.1fr | 1.2fr | 1fr | 1fr  gap 16  align stretch
   widths: 412.9 | 235.9 | 196.6 | 196.6
Card (all four): pad 18 20  radius 18  bg #FCFBF9  tri-tone border  shadow xs  column
```

**Card A: Anesthesia record** (w 412.9, inner 370.9)

```
├ h3 "Anesthesia record" 16/24 600
├ "Observed values · 5-minute strip" 13/18 #5C616C
├ Legend  margin-top 10  wrap gap 6 14  12/16 #5C616C, items gap 6
│   ├ swatch 12x2 radius 1 #0F8A57  "HR bpm"
│   ├ swatch 4x12 radius 2 #5F78E2  "BP mmHg"
│   └ swatch 8x12 radius 3 bg #FBE6E2 border 1 dashed #B3261E  "Due 12:41" {data}
└ Chart SVG  margin-top 12  viewBox 0 0 400 188  width 100% (rendered ≈370.9 x 174.4, uniform scale .927)
```

Chart geometry (viewBox units; scale uniformly):

| Element | Value |
|---|---|
| Plot x range | 34 (11:30) to 378 (12:45): 344 units / 75 min = 4.5867 units/min. x = 34 + minutes_since_11:30 x 4.5867 |
| X labels | y=180, text-anchor middle, 10 Martian Mono #5C616C: 11:30 @34.0, 11:45 @102.8, 12:00 @171.6, 12:15 @240.4, 12:30 @309.2, 12:45 @378.0 |
| HR band (top) | gridlines y=12 (110), 38 (95), 64 (80); y = 64 - (bpm - 80) x 1.7333 |
| BP band (bottom) | gridlines y=96 (130), 128 (85), 160 (40); y = 160 - (mmHg - 40) x 0.7111 |
| Gridlines | x1 34 to x2 378, stroke #EBEBED width 1 |
| Y labels | x=26 text-anchor end, baseline y = line + 3.5, 10 Martian Mono #5C616C |
| Due band | rect x 351.7 y 8 w 16 h 152 rx 4 fill #FBE6E2; centre line x 359.7 y 8–160 stroke #B3261E 1.5 dasharray "3 3" (12:41) |
| HR line | polyline stroke #0F8A57 width 2, round join/cap, no fill |
| HR points | circle r 2.5 fill #0F8A57 stroke #FCFBF9 2; last point r 4 |
| BP bars | rect width 4 rx 2 fill #5F78E2, x = point x - 2, top = systolic y, bottom = diastolic y |

Draw order: due band, due line, HR gridlines + labels, HR line, HR points, BP gridlines + labels, BP bars, x labels.

Data (`{data}`, 13 readings every 5 min; points tooltips "11:36 · HR 96 bpm", "11:36 · BP 112/64 mmHg"):

| Time | x | HR | HR y | BP | bar y / h |
|---|---|---|---|---|---|
| 11:36 | 61.5 | 96 | 36.3 | 112/64 | 108.8 / 34.1 |
| 11:41 | 84.5 | 94 | 39.7 | 108/62 | 111.6 / 32.7 |
| 11:46 | 107.4 | 92 | 43.2 | 104/60 | 114.5 / 31.3 |
| 11:51 | 130.3 | 90 | 46.7 | 102/58 | 115.9 / 31.3 |
| 11:56 | 153.3 | 91 | 44.9 | 104/59 | 114.5 / 32.0 |
| 12:01 | 176.2 | 93 | 41.5 | 106/60 | 113.1 / 32.7 |
| 12:06 | 199.1 | 95 | 38.0 | 108/62 | 111.6 / 32.7 |
| 12:11 | 222.1 | 94 | 39.7 | 110/63 | 110.2 / 33.4 |
| 12:16 | 245.0 | 92 | 43.2 | 107/61 | 112.4 / 32.7 |
| 12:21 | 267.9 | 93 | 41.5 | 106/60 | 113.1 / 32.7 |
| 12:26 | 290.9 | 95 | 38.0 | 108/62 | 111.6 / 32.7 |
| 12:31 | 313.8 | 96 | 36.3 | 110/64 | 110.2 / 32.7 |
| 12:36 | 336.7 | 94 | 39.7 | 109/63 | 110.9 / 32.7 |

(Markup values match the formula to one decimal; bar height = (systolic - diastolic) x 0.7111.)
Accessible label: "Anesthesia record, 11:36 to 12:36. Heart rate 90 to 96 bpm. Blood pressure 102/58 to 112/64 mmHg. Next reading was due at 12:41."

**Card B: Latest medication** (w 235.9)

```
├ Label row gap 6: syringe 15 stroke #6B707B 1.75 + "Latest medication" 13/18 500 #5C616C
├ "Cefazolin" {data}  margin-top 8  18/24 600
├ "4.05 mL · IV · 11:40" {data}  Martian Mono 12/18 500 #5C616C
├ Next dose block  margin-top 16 pad-top 14 border-top 1 #EBEBED column
│   ├ "Next dose" 13/18 #5C616C
│   ├ "13:10" {data}  margin-top 2  Martian Mono 24/30 500 -0.03em
│   └ "Cefazolin redose · in 28 min" {data} 13/18 #5C616C
└ Footer  margin-top auto pad-top 14  gap 8  13/18 ink: dot 6 #0F8A57 + "Fluids running · LRS 5 mL/kg/h" {data}
```

**Card C: Circulation** (w 196.6, inner 154.6)

```
├ Title gap 6: heart-pulse 16 stroke ink 1.75 + "Circulation" 16/24 600
├ "Heart rate · bpm" margin-top 10  13/18 #5C616C
├ "94" {data}  Martian Mono 48/52 500 -0.05em
├ Sparkline  margin-top 12  SVG viewBox 0 0 160 32, width 100% h 32, preserveAspectRatio none
│   polyline stroke #0F8A57 2 round, non-scaling stroke; end dot r 4 fill #0F8A57 stroke #FCFBF9 2 (the circle is scaled with the viewBox, so it renders as an ellipse ~3.9 x 4 px; draw a true 8 px circle in XAML)
│   points: 4.0,8.8 16.7,13.6 29.3,18.4 42.0,23.2 54.7,20.8 67.3,16.0 80.0,11.2 92.7,13.6 105.3,18.4 118.0,16.0 130.7,11.2 143.3,8.8 156.0,13.6
│   mapping: x = 4 + i x 12.667 (13 points); y = 8.8 + (96 - HR) x 2.4  (same HR series as Card A)
└ "BP 109/63 · MAP 78" {data}  margin-top auto pad-top 12  13/18 #5C616C
```

**Card D: Respiration** (w 196.6)

```
├ Title gap 6: wind 16 stroke ink 1.75 + "Respiration" 16/24 600
├ "Oxygen saturation" margin-top 10 13/18 #5C616C
├ Value row baseline gap 4: "97" {data} Martian Mono 48/52 500 -0.05em + "%" 16/24 #5C616C
├ Sparkline (same geometry) stroke #5C616C; end dot fill #5C616C stroke #FCFBF9
│   points: 4.0,12.0 16.7,12.0 29.3,16.0 42.0,16.0 54.7,12.0 67.3,16.0 80.0,20.0 92.7,16.0 105.3,16.0 118.0,12.0 130.7,16.0 143.3,16.0 156.0,16.0
│   mapping (inferred): y = 16 - (SpO2 - 97) x 4  -> series 98,98,97,97,98,97,96,97,97,98,97,97,97
└ "EtCO₂ 38 mmHg · RR 12" {data}  margin-top auto pad-top 12  13/18 #5C616C
```

### 3.5 ECG strip (green card)

Same path as the Today card strip (one 75-unit beat repeated 4x, baseline y 24):
`M0 24 h16 q4 -6 8 0 h6 l3 3 l4 -21 l4 25 l3 -7 h9 q7 -8 14 0 h8` x4.
Here the viewBox is `0 0 300 40` so all 4 beats show, stretched non-uniformly to ≈270 x 32 (x .9, y .8). Stroke #0F8A57 1.6 non-scaling, round join. `.sweep` eraser animates across (2.6s linear infinite, color #D5ECE0 matches the card).

---

## 4. Interaction and motion

- Pulse on the "UNDER ANESTHESIA" dot and the sidebar monitoring dot (clv-pulse 2s).
- ECG sweep (clv-sweep 2.6s).
- Buttons: shared `.btn` press scale .96, hovers, focus ring.
- Record nav and sidebar nav: hover rgba(27,33,47,.05).
- Chart points/bars expose `<title>` tooltips per reading.
- Reduced motion: no pulse, no sweep, no transitions.
- Two "Record observations" actions exist (CTA in the hero and the alert button); they should invoke the same command.

---

## 5. Copy inventory

Fixed: "Patients", "/", "Search patients, owners, records", "⌘K", "Under anesthesia" (state label), "Preparation", "Vitals", "Medical history", "Lab results", "Medications", "Pet-parent view", "Weight at this visit", "kg", "UNDER ANESTHESIA", "since", "Last reading", "Next due", "Record observations", "Pre-op", "Administer dose", "Anesthesia record", "Observed values · 5-minute strip", "HR bpm", "BP mmHg", "Due", "Latest medication", "Next dose", "Circulation", "Heart rate · bpm", "Respiration", "Oxygen saturation", "%".

Data: "Bella", "English bulldog", "2 years · M. Lavoie", "Dr. Okafor · S. Tremblay", "ASA II", "18.4", "Recorded 28 Oct, 11:05", "since 11:30", "1:12", "12:36", "12:41", "Reading overdue 1:26", "Reading overdue by 1:26", "Last recorded 28 Oct, 12:36 · readings due every 5 minutes", "Ovariohysterectomy (spay)", "Started 11:30 · OR 1 · Dr. Okafor, S. Tremblay", "Due 12:41", chart series and axis times, "Cefazolin", "4.05 mL · IV · 11:40", "13:10", "Cefazolin redose · in 28 min", "Fluids running · LRS 5 mL/kg/h", "94", "BP 109/63 · MAP 78", "97", "EtCO₂ 38 mmHg · RR 12".

---

## 6. Assets

| File | Size | Content | Displayed |
|---|---|---|---|
| `assets/3383aea8ac2a78cbc35d0db76679e012.jpg` | 1026x650 | Bella, English bulldog sitting in a field (landscape) | Photo panel 520x434, radius 16. Foreground `contain` -> 520x329.5 centered (≈52 px bands top and bottom). Backdrop: same image stretched to 600x514 at -40,-40, blur 28 px, saturate .9, opacity .9, clipped by the panel. Inset shadow overlay. |

XAML: two `Image`s in a clipped `Grid` (CornerRadius 16); the backdrop needs a blur effect (Composition `GaussianBlurEffect` / Skia image filter; on targets without it, pre-blur the bitmap at build time). Alt text: "Bella, an English bulldog, sitting in a field".

---

## 7. Ambiguities

- Procedure header height: text block 28 + 2 + 20 = 50 vs 40 px buttons; row height is 50 (PNG reads ~48).
- Anesthesia card height (≈258) and team-chip position are flex results, not explicit values.
- SpO2 values are inferred from y-coordinates; no SpO2 readings appear in the markup.
- Breadcrumb "Patients" link never shows its underline (inline override); decide whether hover should underline.
- "⌘K" keycap: likely "Ctrl K" on Windows.
