#!/bin/sh
# Record one Knuckles reading in session s, then hold the page's clock 75 s later for the rest of the session
D=$(dirname "$0")/../d.sh; P=${WORK:-/tmp/vigil-banners}/peek; B=$P/../bin
$D '{"op":"click","ctx":"s","x":78,"y":181}'; $B/waitready.sh s 180 orange 800 290 560 120
$D '{"op":"click","ctx":"s","x":696,"y":226}'; $B/waitready.sh s 180 photo 600 150 400 300
$D '{"op":"wait","ms":4000}'
$D '{"op":"click","ctx":"s","x":1227,"y":528}'; $B/waitready.sh s 180 primary 830 85 580 55
$D '{"op":"wait","ms":3000}'
$D '{"op":"click","ctx":"s","x":1120,"y":113}'; echo "reading $(date +%T)"
$D '{"op":"fixtime","ctx":"s","plus":75000}'
$D '{"op":"wait","ms":8000}'
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$P/hold.png\",\"css\":true}"
echo RUN-DONE
