#!/bin/sh
# build.sh : render every banner at 1x and 2x and the two heroes into ../finals (run from src/; NODE_PATH must reach playwright)
cd "$(dirname "$0")/.." || exit 1
set -- showcase-desk showcase-phones showcase-daynight focus-surgery focus-stages focus-vitals focus-dose \
       stylized-split stylized-stack stylized-mosaic focus-surgery-titled
args=""
for n in "$@"; do args="$args $n.html ../finals/$n@1x.png 1200 630 1 $n.html ../finals/$n@2x.png 1200 630 2"; done
args="$args focus-surgery-hero.html ../finals/focus-surgery-hero.png 1920 1080 1 stylized-split-hero.html ../finals/stylized-split-hero.png 1920 1080 1"
node tools/render.js $args
