#!/bin/sh
D=$(dirname "$0")/../d.sh; C=${WORK:-/tmp/vigil-banners}/caps; P=$C/../peek; B=$(dirname "$0")
$D '{"op":"click","ctx":"s","x":431,"y":32}'; sleep 20; $B/waitready.sh s 300 photo 600 150 400 300; sleep 5
$D '{"op":"click","ctx":"s","x":985,"y":523}'; sleep 30; $B/waitready.sh s 300 photo 640 170 400 220; sleep 5
$D '{"op":"viewport","ctx":"s","w":390,"h":844}'; $D '{"op":"park","ctx":"s"}'; sleep 30; $B/waitready.sh s 300 photo 60 230 270 200; sleep 10
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/dark-phone-parent@2x.png\"}"; echo "dark phone parent $(date +%T)"
$D '{"op":"scheme","ctx":"s","scheme":"light"}'; sleep 30; $B/waitready.sh s 300 photo 60 230 270 200; sleep 10
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/light-phone-parent@2x.png\"}"; echo "light phone parent $(date +%T)"
$D '{"op":"viewport","ctx":"s","w":1440,"h":900}'; sleep 30
$D '{"op":"click","ctx":"s","x":82,"y":265}'; sleep 60; $D '{"op":"park","ctx":"s"}'; sleep 20
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/light-desk-agenda@2x.png\"}"; echo "light agenda $(date +%T)"
cd $C; convert dark-phone-parent@2x.png light-phone-parent@2x.png -resize 40% +append $P/pa1.png; convert light-desk-agenda@2x.png -resize 30% $P/ag1.png
echo RUN-DONE
