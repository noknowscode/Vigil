# Vigil gallery banners

Banners for the Uno Platform Studio samples gallery, made from real captures of the Vigil sample (`vigil`, branch
`studio`, Uno.Sdk 7.0.0-dev.132) running as a Release WebAssembly build in headless Chromium. Nothing in them is
redrawn: the UI, data and copy are the app's own sample day (Settings → Load sample day), driven by clicks and keys.
Styling is only around and on top of the captures: device frames drawn in CSS, crop, perspective, blur, light, a faint
five-minute grid (the paper anesthesia sheet Vigil replaces) and the backdrop.

## Finals

All in `finals/`: every banner at 1x (1200 × 630) and 2x (2400 × 1260), and two heroes at 1920 × 1080.

| Banner | Family | What it shows | Source captures (state · theme · viewport · DPR) |
|---|---|---|---|
| `showcase-desk` | showcase | Today (Light) in a desktop window between Today at phone width (Dark) and Milo's pet-parent view (Light), on the morning wash | Today · Light · 1440×900 · 2 ; Today · Dark · 390×844 · 2 ; pet-parent view (Milo) · Light · 390×844 · 2 |
| `showcase-phones` | showcase | Three phones: Today (Light), Milo's pet-parent view (Dark, centre, raised), Patients (Light), on the sage wash | Today, Patients · Light · 390×844 · 2 ; pet-parent view · Dark · 390×844 · 2 |
| `showcase-daynight` | showcase | Two tablets: Today (Dark, ink bezel) behind Milo's record in recovery (Light, porcelain bezel), on a wash from morning cream to night | Today · Dark · 1440×900 · 2 ; Milo's record · Light · 1440×900 · 2 |
| `focus-surgery` (+ hero) | focused | Today's hero, lifted out of the page: Knuckles in surgery, 1:33 under anesthesia, next reading in 3:44, and the day's timeline with its Now line; the rest of Today blurred behind | Today · Light · 1440×900 · 2, the panel cropped on its own edge (1090 × 347 CSS px) |
| `focus-stages` | focused | The stage board: the four patient cards in care order (Oscar booked, Knuckles in surgery, Milo in recovery, Pepper home), so the stage rails read as one journey | Patients · Dark · 1440×900 · 2, four cards cropped on their borders (347 × 229 CSS px each) |
| `focus-vitals` | focused | Knuckles' monitoring row: the anesthesia record on its 5-minute strip, cefazolin and its redose, Circulation (HR 94), Respiration (SpO₂ 97 %); the record's hero blurred above | Knuckles' record scrolled to the row · Dark · 1440×900 · 2, four cards on their borders ; record top · Dark (blurred) |
| `focus-dose` | focused | The weight-based dose: Knuckles 18.4 kg × cefazolin 22 mg/kg = 4.05 mL, with the formula and reference range, beside the formulary with Cefazolin selected | Administer dose drawer · Light · 1440×900 · 2, the list (286 × 695) and the Draw up card (285 × 416) on their borders |
| `stylized-split` (+ hero) | stylized | Knuckles' record, Light and Dark, cut on one rising diagonal through backdrop and window | Knuckles' record · Light, then Dark about 25 min later in the same session, at the same held clock · 1440×900 · 2 |
| `stylized-stack` | stylized | Isometric stack of the afternoon: Today (Light) on top, Knuckles' monitoring row (Dark), Milo in recovery (Light), the Agenda (Dark), Milo's sign-off (Light) | Today, Milo's record, Sign off · Light ; record scrolled, Agenda · Dark · all 1440×900 · 2 |
| `stylized-mosaic` | stylized | Sixteen states as a tilted checkerboard of Light and Dark screens: Today, both records, observations pad, dose drawer, recovery, sign-off, Patients, Agenda | all sixteen desktop captures · 1440×900 · 2 |
| `focus-surgery-titled` | titled (**optional**) | `focus-surgery` with "Vigil" in Newsreader, the app's display serif (Today's greeting, patient names) | as `focus-surgery` |

Strongest: **`focus-surgery`** and **`stylized-split`** (both have heroes).
- `focus-surgery` is the app's whole promise in one panel: who is in surgery, for how long, when the next reading is
  due, and where every patient is in the day. It reads at thumbnail size, the figures are Vigil's own Martian Mono, and
  the lavender-to-sage wash is the hero's own.
- `stylized-split` is the most arresting frame: the bulldog portrait gives it an instant subject, the record shows the
  anesthesia timer and the Record observations action, and one diagonal shows both themes of the same moment.

## Captures and session

All 21 captures in `src/cap/` come from **one** browser session (context `s`) of the Release WebAssembly build, at
DPR 2, so data and clocks agree across every banner:

1. Settings: Reduce motion on (stops the live pulse so frames are stable), then Load sample day → Replace records.
2. Knuckles' record → Record observations → Record reading with the pad's pre-filled values (HR 94, SpO₂ 97 %…). The
   sample day otherwise opens with Knuckles' reading 6 minutes overdue, a red alert state.
3. The capture driver then **held the page's `Date` at 17:20:16** (a Date-only wrapper injected at load; the runtime's
   timers kept running). Every frame therefore shows 17:20: 1:33 under anesthesia, last reading 17:19, next due 17:24,
   next reading in 3:44. This is the app's own rendering at one moment, as Cargo used a design clock.
4. Screens were reached by clicks and keys and captured after the late parts had drawn (photos, timeline bars), checked
   by pixel probes, not by fixed waits. The dose drawer shows cefazolin and 22 mg/kg typed in but not given; the
   sign-off shows the review box ticked but not signed.

## Palette

Backdrops, glows, bezels, the grid and the seam use the app's own colours, listed in `src/frames.css`:
- `Vigil/Styles/ColorPaletteOverride.xaml`, Light: Primary #244936, PrimaryVariantDark #1C3A2B, PrimaryVariantLight
  #B8DDC8, PrimaryContainer #D5ECE0, SecondaryContainer #E5E8F9, Background #F3F3F2, Surface #FCFBF9, SurfaceVariant
  #EDEDEF, OutlineVariant #E4E4E6, OnSurface #1B212F (also the phone bezels).
- Dark (Default): Primary #8FD1AE (the split seam, the night grid), PrimaryContainer #17372A (the forest glow),
  SecondaryContainer #262B45, Background #111315, Surface #1A1D21, SurfaceVariant #2A2D33, OutlineVariant #2F3238.
- `Vigil/App.xaml` (no role): AnesthesiaBrush #4FC28E Dark (the vitals glow), HomeBrush #DCE7E0 Light (the sage wash).

## Rebuild

`src/*.html` are the compositions (one per banner, heroes as their own files), `src/cap/` the captures they use,
`src/frames.css` the shared frames and palette, `src/fonts/` the app's NewsreaderT20 file for the titled variant.
- Render everything: `NODE_PATH=<dir with playwright> src/tools/build.sh`. One banner:
  `node src/tools/render.js <html> <out.png> <W> <H> <scale>`. The 2x is rendered with CSS zoom at device scale 1:
  headless Chromium rasterizes perspective-transformed layers soft at `deviceScaleFactor: 2`, while a zoomed page is
  laid out and rasterized at full size.
- Capture: `src/tools/drv.js` is a long-lived Playwright driver taking JSON commands over HTTP (`src/tools/d.sh`), so a
  session keeps one store and one clock across navigation, theme and viewport changes. `src/tools/session/` holds the
  scripts that drove the session (setup, the held clock, Today, Patients, Milo, the pet-parent view) and the pixel
  probes (`waitready.sh`, `ready.py`); the dark record, observations, dose and sign-off steps were run by hand with the
  same commands.

## Rules kept

- **Real pixels.** Every screen is a full-viewport capture or a crop on the element's own border; nothing is retouched.
- **No enlarging.** Captures hold 2 device px per CSS px. In the 2x exports the largest display is 2.0 device px per
  capture px at the hinge edges of `focus-dose` and about 1.9 at the near corner of `focus-stages`; the heroes show
  1.5 (`focus-surgery`) and 1.2 (`stylized-split`).
- **Clean states.** Seeded data, Reduce motion on, pointer parked off the content, scrollbars hidden, no focus ring,
  spinner or overdue alert in any frame.
- **Generic frames.** Window bars and bezels are plain CSS: no notch, camera or logo.
- **Safe area.** Key content sits inside the central 90 %.

## Uncertain

- **The wordmark reads "clover".** The app's in-product name is Clover (`docs/design/CLOVER.md`), shown in the sidebar
  and phone headers; the gallery name is Vigil. It appears in the showcase, split, stack and mosaic frames (it cannot be
  avoided on desktop screens); the four focused banners do not show it. "Clover" is also a name used by unrelated
  companies: worth a coordinator decision.
- Milo's recovery timer reads 0:55 on Today and his record but 0:54 on the Patients cards at the same held clock: two
  views of the app rounding differently. `focus-stages` shows 0:54; no single frame shows both.
- The showcase, stack and mosaic frames show whole screens at a third to a half of their size, so their UI text is
  below 10 px at 1x: it reads as texture, as in the house-style examples. The focused banners keep text at 10 px or more.
- The patient photos are the app's bundled, fictional AI-generated portraits (`Vigil/Assets/Pets/README.txt`);
  clinician and owner names are the sample day's invented names. No real people.
- The titled variant uses Newsreader (the app's display serif, from its own font file) rather than Hanken Grotesk.
