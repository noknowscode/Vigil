#!/bin/sh
# waitready.sh <ctx> <maxsec> <kind> <x> <y> <w> <h> [<kind> <x> <y> <w> <h>] : poll CSS clips until every check passes
D=$(dirname "$0")/../d.sh; B=$(dirname "$0"); T=${WORK:-/tmp/vigil-banners}/peek/wr.png
CTX=$1; MAX=$2; shift 2
start=$(date +%s)
while :; do
  ok=1; set -- "$@"; args="$*"; set -- $args
  while [ $# -ge 5 ]; do
    $D "{\"op\":\"shot\",\"ctx\":\"$CTX\",\"path\":\"$T\",\"css\":true,\"clip\":{\"x\":$2,\"y\":$3,\"width\":$4,\"height\":$5}}" >/dev/null
    python3 $B/ready.py $T $1 >/dev/null || ok=0
    shift 5
  done
  set -- $args
  [ $ok = 1 ] && { echo "ready after $(( $(date +%s) - start ))s"; exit 0; }
  [ $(( $(date +%s) - start )) -ge $MAX ] && { echo "NOT READY after ${MAX}s"; exit 1; }
  sleep 3
done
