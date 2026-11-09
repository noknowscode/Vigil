#!/bin/sh
D=$(dirname "$0")/../d.sh; C=${WORK:-/tmp/vigil-banners}/caps; P=$C/../peek; B=$(dirname "$0")
# Patients (desktop): photo tiles of Oscar, Knuckles, Milo cards; stage strip orange (Pepper's prep) present
DESK="photo 318 160 60 35 photo 690 160 60 35 photo 1062 160 60 35"
$D '{"op":"viewport","ctx":"s","w":1440,"h":900}'; sleep 30
$D '{"op":"click","ctx":"s","x":84,"y":223}'; sleep 20; $B/waitready.sh s 300 $DESK; sleep 8; $D '{"op":"park","ctx":"s"}'; sleep 2
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/dark-desk-patients@2x.png\"}"; echo "dark desk patients $(date +%T)"
$D '{"op":"viewport","ctx":"s","w":390,"h":844}'; $D '{"op":"park","ctx":"s"}'; sleep 30
$D '{"op":"scheme","ctx":"s","scheme":"light"}'; sleep 30; $B/waitready.sh s 300 photo 40 145 60 35; sleep 10
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/light-phone-patients@2x.png\"}"; echo "light phone patients $(date +%T)"
cd $C; convert dark-desk-patients@2x.png -resize 30% $P/p1.png; convert light-phone-patients@2x.png -resize 40% $P/p2.png
echo RUN-DONE
