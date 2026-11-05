# Today dashboard (C cards): value sheet

Source: `Downloads\Today · dashboard (C cards)-html\App-Today.dc.html`, reference render `Today · dashboard (C cards)@1x.png`.
Shell, sidebar, search/bell, shared tokens, buttons and icons: see `shell-and-tokens.md`. This sheet covers what is specific to the Today page.
Artboard: **1440 x 1380**. Main frame 1172 x 1356 at x=256, y=12. Content box 1090 wide, starting x=297 (256 + 40 pad + 1 border), y=49.

Data-driven values are marked `{data}`; everything else is a fixed label.

---

## 1. Tokens used on this page

Colors: all neutrals, borders, primary, status set (anesthesia, prep, recovery, waiting, error) from the shared sheet, plus:

| Value | Role here |
|---|---|
| `#E5E8F9` / `#DCEAF0` / `#D5ECE0` | Hero gradient stops (110deg, 0% / 52% / 100%) |
| `#DDE3EE` | Hero section border |
| `rgba(255,255,255,.5)` | Hero glass shell fill |
| `rgba(255,255,255,.8)` | Hero glass shell border |
| `rgba(255,255,255,.45)` | Gantt row track |
| `#7A4A1C` | Lightbulb stroke on prep tile `#FCE3C6` |

Type roles used: display-title, body-l, mono-hero, mono-stat, mono-value, mono-timer-card, mono-label, mono-cell, mono-num, mono-axis, h2-section, h3-card-name, title-s, body, body-s, button-s, caption, micro.
Shadows: frame, xs, card, glass, seg-on, knob, gantt-dot, inset-photo, inset-strip, inset-strip-soft.

---

## 2. Layout skeleton

```
Main content (w=1090)
├ Header  flex space-between, align flex-start, gap 24
│   ├ Title block
│   │   ├ h1 "Good afternoon, Dr. Okafor"  Newsreader 40/46 400 -0.015em ink
│   │   └ p margin-top 8  16/24 #5C616C:
│   │       [span ink 600 "1 patient in surgery."] + " Bella's reading is overdue · 1 recovering · 1 booked · 1 signed out today."
│   └ Right cluster (search 340 + bell 40, gap 10)  -> shared sheet section 9
├ Hero "Right now" section  margin-top 28  (y≈155-506 est. from PNG; computed h=351)
├ "Today's patients" section  margin-top 40
├ Bottom split (Due next | Where care time went)  margin-top 48
```

### 2.1 Hero section ("Right now")

```
Section  w=1090 pad=24 radius=24 border 1 #DDE3EE bg=hero-gradient
  grid columns 448 | 1fr (=552)  gap 40  align-items center   (inner width 1040)
├ Glass shell  w=448  pad 6  radius 22  bg rgba(255,255,255,.5)  border 1 rgba(255,255,255,.8)  shadow glass   (h=301 computed)
│ └ Live card  pad T18 R20 B18 L20  radius 16  bg #FCFBF9  tri-tone border   (inner w=394)
│   ├ Row 1  flex space-between align center gap 12
│   │   ├ Icon tile 30x30 radius 9 bg #D5ECE0 border 1 #B8DDC8; activity icon 16 stroke #005527 width 2
│   │   ├ (gap 10) Column: "In surgery · Bella" 15/20 600 ink · "Ovariohysterectomy (spay) · OR 1" 13/18 #5C616C
│   │   └ Button primary-s "Record" + plus  (h32 pad 0 14 r10 13/20 600)
│   ├ Timer row  margin-top 14  flex baseline gap 12
│   │   ├ "1:12" {data}  Martian Mono 64/68 500 -0.05em ink
│   │   └ "under anesthesia"  14/20 #5C616C
│   ├ Chip row  margin-top 8  flex center gap 10
│   │   ├ Error chip h26 "Reading overdue 1:26" {data time}  (shared: Status chip)
│   │   └ "5-minute strip"  13/18 #5C616C
│   └ Stats  margin-top 16, pad-top 14, border-top 1 #EBEBED; grid 3 x 1fr gap 16
│       Each stat: column gap 2
│         ├ label row: dot 6x6 + gap 6 + label 13/18 #5C616C
│         ├ value Martian Mono 18/26 500 tabular ink
│         └ sub 12/16 #5C616C nowrap
│       Stat 2 and 3: border-left 1 #EBEBED, pad-left 16
│       1: dot #0F8A57 "Induced"  "11:30"  "1h 12m ago"
│       2: dot #5C616C "Last reading"  "12:36"  "HR 94 · SpO₂ 97%"
│       3: dot #B3261E "Next due"  "12:41"  "1:26 late"
└ Care timeline column  pad T4 R4 B0 L0  (inner w=548)
    ├ Summary block
    │   ├ "Under care today"  14/20 500 #5C616C
    │   ├ "7h 16m" {data}  margin-top 2  Martian Mono 28/32 500 -0.03em
    │   └ "4 patients · 3 procedures · Wed 28 Oct" {data}  margin-top 4  13/18 #5C616C
    └ Gantt (role=img)  margin-top 14  -> section 2.2
```

Computed live-card height: 18 + 38 + 14 + 68 + 8 + 26 + 16 + 1 + 14 + 64 + 18 + 2(border) = 287; shell 287 + 12 + 2 = 301.

### 2.2 Gantt timeline ("Care through the day")

Time axis 08:00 to 15:00 = 420 min = 100%. x% = (t - 08:00) / 420 x 100. One hour = 14.286%.
Plot track width = 548 - 56 = **492 px** (left label column 56).

```
Gantt  column
├ Now label row  margin-left 56  h=22  relative
│   └ Pill "Now 12:42" {data}  left 67.143% translateX(-50%)  top 0  h=20 pad 0 6 radius 6 bg #1B212F
│        text Martian Mono 11/16 500 #FFFFFF nowrap
├ Rows block (relative)
│   ├ Grid overlay (absolute, top 0 bottom 0, left 56, right 0)
│   │   ├ 8 hour gridlines at 0, 14.286, 28.571, 42.857, 57.143, 71.429, 85.714, 100%  w=1 rgba(27,33,47,.08), full height
│   │   └ Now line at 67.143%  w=1 #1B212F  top -2 to bottom 0
│   └ Row (x4)  grid 56 | 1fr  h=30  align center
│       ├ Name 13/16 500 ink
│       └ Track  h=12 radius 6 bg rgba(255,255,255,.45)  relative
│           └ Segment  absolute top 0 h=12 radius 4  left=start%  width=dur% (minus 2px when followed by another segment)
├ Axis labels  margin-left 56  h=16  margin-top 6
│   └ "08:00" … "15:00" each absolute at hour %, translateX(-50%)  Martian Mono 11/16 500 #5C616C
└ Legend  margin T14 L56  wrap  gap 6 (row) 16 (col)  12/16 #5C616C
    items: dot 8x8 + gap 6 + label: Prep #C97A2E · Anesthesia #0F8A57 · Recovery #5F78E2 · Waiting #7E828B · Planned (swatch 14x8 radius 3 planned-hatch)
```

Segments (all `{data}`; left / width as in markup):

| Row | Phase | Times | left | width | Color |
|---|---|---|---|---|---|
| Pepper | Prep | 08:00–08:32 | 0% | 7.619% - 2px | #C97A2E |
| Pepper | Anesthesia | 08:32–09:27 | 7.619% | 13.095% - 2px | #0F8A57 |
| Pepper | Recovery | 09:27–10:07 | 20.714% | 9.524% | #5F78E2 |
| Milo | Prep | 09:30–10:02 | 21.429% | 7.619% - 2px | #C97A2E |
| Milo | Anesthesia | 10:02–12:08 | 29.048% | 30.000% - 2px | #0F8A57 |
| Milo | Recovery | 12:08–now | 59.048% | 8.095% | #5F78E2 |
| Bella | Prep | 11:00–11:30 | 42.857% | 7.143% - 2px | #C97A2E |
| Bella | Anesthesia | 11:30–now | 50.000% | 17.143% | #0F8A57 |
| Oscar | Waiting | 12:27–now | 63.571% | 3.572% | #7E828B |
| Oscar | Planned prep | 13:30–14:00 | 78.571% | 7.143% - 2px | planned-hatch |
| Oscar | Planned surgery | 14:00–15:00 | 85.714% | 14.286% | planned-hatch |

Now dots (only on rows whose current phase is active now: Milo, Bella): 16x16 circle, fill = phase color (Milo #5F78E2, Bella #0F8A57), border 3 #FFFFFF, shadow gantt-dot, top -2, left calc(67.143% - 8px).
Segment tooltips (`title`): "{Name} · {Phase} {start}–{end}" e.g. "Pepper · Prep 08:00–08:32", "Milo · Recovery 12:08–now", "Oscar · Planned surgery 14:00–15:00".
Accessible label: "Care through the day, 08:00 to 15:00. Pepper: prep, anesthesia 08:32 to 09:27, recovery to 10:07. Milo: anesthesia 10:02 to 12:08, in recovery now. Bella: under anesthesia since 11:30. Oscar: waiting since 12:27, surgery planned 14:00."

Gantt total height: 22 + 120 + 6 + 16 + 14 + 16 = 194 (+ summary 76 + 14 = 284), vertically centered against the 301 px live card.

### 2.3 Today's patients

```
Section  margin-top 40
├ Header row  flex space-between align center gap 16
│   ├ h2 "Today's patients"  20/28 600
│   └ Right  flex gap 16
│       ├ Segmented control (role=group "Filter patients")  inline-flex gap 2 pad 3 radius 11 bg #F3F3F2 border 1 #E8E8EA
│       │   └ Segment  h=30 pad 0 12 radius 8 gap 6 13/18; count span #5C616C tabular
│       │       Selected: bg #FCFBF9, border 1 #E4E4E6, shadow seg-on, weight 600
│       │       Unselected: bg transparent, border 1 transparent, weight 500
│       │       "In clinic" 3 (selected) · "Signed out" 1 · "All" 4   (counts {data})
│       │       Track height = 30 + 6 + 2 = 38
│       └ Link "All records" + arrow-right
└ Card grid  margin-top 16  3 columns 1fr gap 24  align start   -> card w = (1090-48)/3 = 347.33
```

### 2.4 Patient card (3 instances)

```
Article (.sh)  w=347.33  pad 3  radius 22  bg #F8F8F7  tri-tone border  shadow card  column  (whole card clickable via .stretch)
└ Tray  radius 18  overflow hidden  bg #FCFBF9  tri-tone border  shadow xs  column
    ├ Header tray  bg #F3F3F2  border-bottom 1 #E6E6E8  radius 0 0 18 18  shadow xs  pad 6
    │   └ Row  flex align center gap 12  pad-right 8
    │       ├ Photo  88x44 radius 12 overflow hidden bg #E9E9EB; img object-fit contain; overlay inset-photo radius 12
    │       └ Column (flex 1)
    │           ├ Row space-between gap 12: name h3 20/24 600 ink (link .stretch) + ASA badge (24 px, shared)
    │           └ Breed · age  14/20 #5C616C
    ├ [Collapsible .cx "details"]  (hidden by default)
    │   ├ Procedure row  min-h 44 pad 10 14  border-bottom 1 #EBEBED  space-between gap 12
    │   │   ├ stethoscope 16 #6B707B + gap 8 + "Procedure" 14/20 #5C616C
    │   │   └ value 14/20 500 ink right-aligned, text-wrap balance
    │   ├ Weight row  h 44 pad 0 14  border-bottom 1 #EBEBED: weight icon + "Weight" | filled-neutral chip (tabular)
    │   └ Pet parent row  h 44 pad 0 14: user-round icon + "Pet parent" | filled-neutral chip (ellipsis)
    ├ Status strip  margin 6 6 0  h=44  pad 0 12  radius 12  border 1  shadow inset  space-between gap 12
    │   ├ Left gap 8: indicator dot + label (Martian Mono 12/16 500 +0.02em UPPERCASE nowrap)
    │   └ Right gap 12: [ECG sparkline 72x20] + value
    ├ Stage stepper (role=img)  pad 12 14  column gap 6
    │   ├ Track  h=20 relative
    │   │   ├ Segments  absolute top 7 left 0 right 0; grid 5 x 1fr gap 3; each h=6 radius 3
    │   │   └ Knob  absolute top 0  20x20 circle  border 1 #D4D4D8  bg knob-gradient  shadow knob
    │   │        left = calc(center% - 10px) with center 10% / 30% / 50% / 70% / 90% for stage 1..5
    │   │        Live card only: inner dot 6x6 #0F8A57 centered
    │   └ Labels  grid 5 x 1fr gap 3  12/16 500 #5C616C centered; current stage ink 600
    │        "Booked" "Prep" "Surgery" "Recovery" "Home"
    ├ [Collapsible .cx.late "team"]  p pad 0 14 14  14/20 #5C616C  e.g. "Dr. Okafor · S. Tremblay"
    └ Toggle (.tg)  h=44  border-top 1 #EBEBED  bg transparent  center gap 6  Hanken 14/20 500 #5C616C
        "Show details" / "Hide details" + chevron-down 16 stroke #6B707B 2 (rotates 180 when open)
```

Collapsed card height (computed): 1+3+1 + 57 (header tray) + 6 + 44 (strip) + 66 (stepper) + 44 (toggle) + 1+3+1 = **227** (PNG 600 to 826).
Expanded adds: details 44 + 44 + 44 (+ procedure may wrap) and team 20 + 14 = 34.

Stepper segment fills:

| Segment state | Fill |
|---|---|
| Booked (always, as done) | step-done-hatch `repeating-linear-gradient(135deg, #DCDCE0 0 1px, #F0F0F1 1px 4px)` |
| Prep done | `#FCE3C6` |
| Surgery done/current | `#BFE3CF` |
| Recovery current | `#D3D9F7` |
| Not reached | `#EDEDEF` |

Per-card values:

| | Bella (live) | Milo (recovering) | Oscar (booked) |
|---|---|---|---|
| Photo asset | `f63f906a5bc4a66c06dcff6c8a1ad897.jpg` | `2eec70eb9cd772c622fc4f73b3b9f235.jpg` | `e659f4036439bd05174cc069cdba7928.jpg` |
| Name / alt | Bella / "Bella, an English bulldog" | Milo / "Milo, a domestic shorthair cat" | Oscar / "Oscar, a Maine coon cat" |
| Sub | English bulldog · 2 yrs | Domestic shorthair · 1 yr | Maine coon · 4 yrs |
| ASA badge | "ASA II" outline | "ASA I" outline | "ASA pending" dashed |
| Procedure | Ovariohysterectomy (spay) | Castration | Mass removal, left flank |
| Weight | 18.4 kg | 4.2 kg | 6.8 kg |
| Pet parent | M. Lavoie | J. Roy | L. Moreau |
| Strip bg / border / inset | `#D5ECE0` / `#B8DDC8` / .12 | `#E5E8F9` / `#CDD3F3` / .12 | booked-hatch / `#E0E0E3` / .1 |
| Strip dot | 8 solid #0F8A57 + pulse | 8 hollow, 1.5 border #5F78E2 | 8 hollow, 1.5 border #7E828B |
| Strip label (color) | ANESTHESIA (#005527) | IN RECOVERY (ink) | BOOKED (#5C616C) |
| Strip right | ECG 72x20 + "1:12" mono 20/24 | "0:34" mono 20/24 | "Not started" Hanken 14/20 500 ink |
| Stepper segments | hatch, #FCE3C6, #BFE3CF, #EDEDEF, #EDEDEF | hatch, #FCE3C6, #BFE3CF, #D3D9F7, #EDEDEF | hatch, #EDEDEF x4 |
| Knob center | 50% + green inner dot | 70% (empty) | 10% (empty) |
| Current label | Surgery | Recovery | Booked |
| aria-label | "Stage 3 of 5: surgery" | "Stage 4 of 5: recovery" | "Stage 1 of 5: booked" |
| Team | Dr. Okafor · S. Tremblay | Dr. Bergeron · A. Nguyen | Dr. Okafor · S. Tremblay |

ECG sparkline (live strip only): container 72x20 overflow hidden, relative. SVG `viewBox="0 0 150 40" preserveAspectRatio="none"` stretched to 72x20 (x scale .48, y scale .5), stroke `#0F8A57`, width 1.6 non-scaling, join round, no fill. Path (one beat = 75 units, repeated 4 times; only the first 2 beats fall inside the 150-unit viewBox):

```
M0 24 h16 q4 -6 8 0 h6 l3 3 l4 -21 l4 25 l3 -7 h9 q7 -8 14 0 h8   (repeat the part after M0 24 three more times)
```
Beat anatomy (y baseline 24): flat 16, P wave (quad, peak y 18) 8, flat 6, Q dip +3, R spike to y 6, S drop to y 31, return to y 24, flat 9, T wave (quad, peak y 16) 14, flat 8.
Overlaid `.sweep` eraser (shared sheet section 6). XAML: draw with `Path` + `Stretch="Fill"` or a Skia canvas; animate an opaque 20 px gradient rectangle across.

### 2.5 Bottom split

```
Grid  margin-top 48  columns 1.55fr | 1fr  gap 40  align start   -> 638.2 | 411.8
├ Due next section (w=638.2)
└ Where care time went section (w=411.8)  pad-left 40  border-left 1 #EBEBED   -> inner 370.8
```

#### Due next

```
├ Header  flex space-between align flex-end gap 16
│   ├ h2 "Due next" 20/28 600 + p margin-top 2 "5 tasks before 14:00 · 1 overdue" {data} 14/20 #5C616C
│   └ Link "Open agenda" + arrow-right
├ Column header  margin-top 16  grid 64 | 1fr | 148 | 96  gap 16  pad-bottom 8  12/16 600 #5C616C
│     "Due" | "Task" | "Status" | (empty)
└ Row (.row) x5  same grid, align center, min-h 72, pad 10 8, margin 0 -8 (bleeds 8 px each side), border-top 1 #EBEBED, radius 0, hover bg #F8F8F7
    ├ Time chip  56x46 radius 10 column centered
    │   Normal: bg #F3F3F2, tri-tone border; label "Due" 11/14 500 #5C616C; time Martian Mono 14/18 500 tabular ink
    │   Late:   bg #FBE6E2, border 1 #F1C9C2; label "Late" 11/14 600 #B3261E
    ├ Task column
    │   ├ title 15/20 600 ink
    │   └ meta 13/18 #5C616C nowrap ellipsis, flex gap 6: dot 6x6 (patient phase color) + patient name (ink 500) + " · …"
    ├ Status 14/20, flex gap 6
    │   overdue: 600 #B3261E + triangle-alert 16 stroke 2
    │   ready:   600 #005527 + check 16 stroke 2
    │   neutral: 400 #5C616C
    └ Action (right aligned): 36 px button (primary-m or secondary-m), pad 0 14 r10 13/20 600
```
1fr task column = 638.2 - 64 - 148 - 96 - 48 = 282.2 px.

| Due | Task | Meta (dot color) | Status | Action |
|---|---|---|---|---|
| Late 12:41 | Record observations | Bella · Anesthesia · 5-minute strip (#0F8A57) | Overdue 1:26 (error) | Record (primary) |
| Due 12:50 | Pain score | Milo · Recovery · every 15 min (#5F78E2) | In 8 min | Score (secondary) |
| Due 13:10 | Cefazolin redose | Bella · 4.05 mL · IV (#0F8A57) | In 28 min | Prepare (secondary) |
| Due 13:15 | Pre-op bloodwork | Oscar · CBC and chemistry panel (#7E828B) | Results ready (success) | Review (primary) |
| Due 14:00 | Discharge call | Pepper · Pet parent · K. Gagnon (#5C616C) | Scheduled | Call (secondary) |

All rows are `{data}`. Fixed labels: "Due", "Late", column headers.

#### Where care time went

```
├ Header  flex space-between align flex-start gap 16
│   ├ h2 "Where care time went" 20/28 600 + p margin-top 2 "7h 16m across 4 patients today" {data} 14/20 #5C616C
│   └ Icon button secondary 32x32 radius 10, arrow-up-right 16 stroke ink 2  (aria "Open the care time report")
├ Bars  margin-top 20  column
│   └ Row x4  grid 96 | 1fr | 64 | 40  gap 12  h=36  align center   (bar track 1fr = 370.8 - 200 - 36 = 134.8)
│       ├ label 14/20 ink
│       ├ track h=8 radius 4 bg #EDEDEF; fill absolute left 0, width = percent, radius 4
│       ├ duration Martian Mono 13/18 500 right tabular ink
│       └ percent 13/18 #5C616C right tabular
│     Anesthesia 58% #0F8A57 "4h 13m" · Prep 22% #C97A2E "1h 34m" · Recovery 17% #5F78E2 "1h 14m" · Waiting 3% #7E828B "0h 15m"
│     tooltip: "Anesthesia: 4h 13m, 58% of care time"
└ Insight card  margin-top 24  flex gap 12  pad 14 16  radius 14  bg #F3F3F2  tri-tone border
    ├ Tile 32x32 radius 10 bg #FCE3C6, lightbulb 16 stroke #7A4A1C 2
    └ Column gap 4, 14/20
        ├ "Prep ran 31 minutes on average today, 6 minutes over plan." {data}
        └ Link "See the prep checklist" + arrow-right (align-self start, 600)
```

---

## 3. Interaction and motion (page-specific use of the shared rules)

- Each patient card has independent open state (`bella`, `milo`, `oscar`, all false initially). Toggle flips it; label switches "Show details" / "Hide details"; `aria-expanded` and `aria-hidden` on both collapsibles.
- Expand: details rows grow above the status strip (0fr to 1fr over 300ms ease-standard), content fades in and slides from -8 px (240ms, 60ms delay); team line uses 160ms delay. Collapse: 200ms rows, 150ms ease-out fade. Chevron rotates 180 over 300ms (200ms back).
- Pulsing dots: sidebar "Monitoring on" and Bella's status strip.
- ECG sweep on Bella's strip, 2.6s linear loop.
- Rows: hover `#F8F8F7`. Card hover border (see quirk note in shared sheet).
- Reduced motion: no sweep, no pulse, instant expand.

---

## 4. Copy inventory

Fixed: "Good afternoon," (greeting prefix, time-of-day driven), "Search patients, owners, records", "⌘K", "Record", "under anesthesia", "5-minute strip", "Induced", "Last reading", "Next due", "Under care today", "Now", "Prep", "Anesthesia", "Recovery", "Waiting", "Planned", "Today's patients", "In clinic", "Signed out", "All", "All records", "Procedure", "Weight", "Pet parent", "Booked", "Surgery", "Home", "Show details", "Hide details", "Not started", "Due next", "Open agenda", "Due", "Task", "Status", "Late", "Where care time went", "See the prep checklist".

Data: "Dr. Okafor"; greeting summary "1 patient in surgery." / "Bella's reading is overdue · 1 recovering · 1 booked · 1 signed out today."; "In surgery · Bella"; "Ovariohysterectomy (spay) · OR 1"; "1:12"; "Reading overdue 1:26"; "11:30" "1h 12m ago"; "12:36" "HR 94 · SpO₂ 97%"; "12:41" "1:26 late"; "7h 16m"; "4 patients · 3 procedures · Wed 28 Oct"; "Now 12:42"; gantt names and segments; axis 08:00–15:00 (derived from day window); segment counts 3/1/4; all card values (table 2.4); "IN RECOVERY", "ANESTHESIA", "BOOKED" (state-driven labels); "0:34"; Due-next rows; "5 tasks before 14:00 · 1 overdue"; care-time rows; "7h 16m across 4 patients today"; insight sentence.

---

## 5. Assets

| File | Size | Content | Displayed |
|---|---|---|---|
| `assets/f63f906a5bc4a66c06dcff6c8a1ad897.jpg` | 808x404 (2:1) | Bella, English bulldog | Card thumb 88x44, radius 12, object-fit contain (2:1 so fills exactly), bg #E9E9EB, inset shadow overlay |
| `assets/2eec70eb9cd772c622fc4f73b3b9f235.jpg` | 808x404 | Milo, domestic shorthair (tabby) | same |
| `assets/e659f4036439bd05174cc069cdba7928.jpg` | 808x404 | Oscar, Maine coon | same |

XAML: `Image Stretch="Uniform"` inside a 88x44 `Border CornerRadius="12"` (clip), plus an inner-shadow overlay.

---

## 6. Ambiguities

- "⌘K" keycap is a macOS glyph; on Windows likely "Ctrl K".
- Gantt "Now" position and widths ending at "now" are computed from current time (12:42 in the mock).
- Hover states listed in the shared quirk note do not render in the prototype.
- Pepper appears in the gantt and Due next but has no card (filter "In clinic" excludes signed-out patients).
