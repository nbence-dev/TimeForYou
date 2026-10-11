#!/usr/bin/env bash
# Fails (so `systemctl --failed` shows it) and logs to the journal when a disk passes the threshold.
# Look at it with: journalctl -t tfy-diskwatch
set -u
LIMIT="${LIMIT:-80}"
fail=0
for mp in / /var/lib/docker; do
  use=$(df --output=pcent "$mp" | tail -1 | tr -dc '0-9')
  if [ "$use" -ge "$LIMIT" ]; then
    logger -t tfy-diskwatch -p user.crit "$mp is ${use}% full (limit ${LIMIT}%)"
    fail=1
  fi
done
exit "$fail"
