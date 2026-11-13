#!/bin/sh
# d.sh '<json>' : send one command to the capture driver (drv.js) on $DRV_PORT (default 8871)
curl -s -m 900 -X POST --data "$1" "http://127.0.0.1:${DRV_PORT:-8871}/"; echo
