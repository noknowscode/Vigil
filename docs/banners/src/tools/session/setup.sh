#!/bin/sh
# setup.sh <ctx> : open the app at 1440x900 DPR 2, Reduce motion on, Load sample day, Today
D=$(dirname "$0")/../d.sh; P=${WORK:-/tmp/vigil-banners}/peek; C=$1
$D "{\"op\":\"open\",\"ctx\":\"$C\",\"url\":\"http://127.0.0.1:8870/\",\"w\":1440,\"h\":900,\"dpr\":2,\"scheme\":\"light\",\"clock\":true}"
$D '{"op":"wait","ms":15000}'
$D "{\"op\":\"click\",\"ctx\":\"$C\",\"x\":92,\"y\":761}"
$D '{"op":"wait","ms":45000}'
$D "{\"op\":\"click\",\"ctx\":\"$C\",\"x\":965,\"y\":492}"
$D '{"op":"wait","ms":4000}'
for i in 1 2 3 4 5 6 7 8 9 10; do $D "{\"op\":\"wheel\",\"ctx\":\"$C\",\"x\":700,\"y\":600,\"dy\":300}" >/dev/null; done
$D '{"op":"wait","ms":5000}'
$D "{\"op\":\"shot\",\"ctx\":\"$C\",\"path\":\"$P/$C-setup1.png\",\"css\":true}"
$D "{\"op\":\"click\",\"ctx\":\"$C\",\"x\":380,\"y\":730}"
$D '{"op":"wait","ms":10000}'
$D "{\"op\":\"shot\",\"ctx\":\"$C\",\"path\":\"$P/$C-setup2.png\",\"css\":true}"
$D "{\"op\":\"click\",\"ctx\":\"$C\",\"x\":612,\"y\":504}"
$D '{"op":"wait","ms":8000}'
$D "{\"op\":\"click\",\"ctx\":\"$C\",\"x\":78,\"y\":181}"
$D '{"op":"wait","ms":30000}'
$D "{\"op\":\"shot\",\"ctx\":\"$C\",\"path\":\"$P/$C-setup3.png\",\"css\":true}"
date +%T
echo SETUP-DONE
