#!/bin/sh
D=$(dirname "$0")/../d.sh; C=${WORK:-/tmp/vigil-banners}/caps; P=$C/../peek; B=$(dirname "$0")
$D '{"op":"viewport","ctx":"s","w":1440,"h":900}'; sleep 30
$D '{"op":"click","ctx":"s","x":1200,"y":180}'; sleep 20; $B/waitready.sh s 300 photo 600 150 400 300; sleep 10; $D '{"op":"park","ctx":"s"}'; sleep 2
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/light-desk-milo@2x.png\"}"; echo "light desk milo $(date +%T)"
$D '{"op":"click","ctx":"s","x":1227,"y":528}'; $B/waitready.sh s 300 primary 830 640 200 30; sleep 8; $D '{"op":"park","ctx":"s"}'; sleep 2
$D "{\"op\":\"shot\",\"ctx\":\"s\",\"path\":\"$C/light-desk-recovery@2x.png\"}"; echo "light recovery $(date +%T)"
cd $C; convert light-desk-milo@2x.png light-desk-recovery@2x.png -resize 30% +append $P/m1.png
echo RUN-DONE
