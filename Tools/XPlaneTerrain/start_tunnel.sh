#!/bin/sh
# Foreground, loopback-only tunnel. The service never exposes installed scenery to the LAN.
set -eu
exec ssh -N -T -o ExitOnForwardFailure=yes -o ServerAliveInterval=15 -o ServerAliveCountMax=3 \
  -L 127.0.0.1:12679:127.0.0.1:8767 "${1:-4090}"
