#!/bin/sh
D=$(dirname "$0")/../d.sh; C=${WORK:-/tmp/vigil-banners}/caps; P=$C/../peek; B=$(dirname "$0")
DESK="orange 800 290 560 120 photo 318 615 60 35 photo 690 615 60 35"
PHONE="orange 40 720 330 110"
$D '{"op":"key","ctx":"s","key":"Escape"}'; sleep 15
$D '{"op":"click","ctx":"s","x":78,"y":181}'; sleep 20; $B/waitready.sh s 300 $DESK; sleep 8; $D '{"op":"park","ctx":"s"}'; sleep 2
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/dark-desk-today@2x.png\"}"; echo "dark desk $(date +%T)"
$D '{"op":"scheme","ctx":"s","scheme":"light"}'; sleep 20; $B/waitready.sh s 300 $DESK; sleep 8
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/light-desk-today@2x.png\"}"; echo "light desk $(date +%T)"
$D '{"op":"viewport","ctx":"s","w":390,"h":844}'; $D '{"op":"park","ctx":"s"}'; sleep 30; $B/waitready.sh s 300 $PHONE; sleep 10
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/light-phone-today@2x.png\"}"; echo "light phone $(date +%T)"
$D '{"op":"scheme","ctx":"s","scheme":"dark"}'; sleep 30; $B/waitready.sh s 300 $PHONE; sleep 10
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/dark-phone-today@2x.png\"}"; echo "dark phone $(date +%T)"
cd $C; convert dark-desk-today@2x.png light-desk-today@2x.png -resize 30% +append $P/t1.png; convert light-phone-today@2x.png dark-phone-today@2x.png -resize 40% +append $P/t2.png
echo RUN-DONE
