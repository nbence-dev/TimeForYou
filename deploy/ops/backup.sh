#!/usr/bin/env bash
# Nightly Postgres dump for tfy-prod. Runs as root from tfy-backup.timer.
# Written to a .partial file, verified with gzip -t, then renamed, so a half-written dump is never mistaken for a good one.
# A dump on the same SSD is NOT a backup: copy /srv/tfy/backups off the laptop too.
set -euo pipefail

DIR="${BACKUP_DIR:-/srv/tfy/backups}"
KEEP_DAYS="${KEEP_DAYS:-7}"
CTR="${DB_CONTAINER:-tfy-core-db-1}"

mkdir -p "$DIR"
out="$DIR/tfy-$(date +%Y%m%d-%H%M%S).sql.gz"
# POSTGRES_USER / POSTGRES_DB already exist inside the container, so no secrets are read or passed here.
docker exec "$CTR" sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner' | gzip > "$out.partial"
gzip -t "$out.partial"
mv "$out.partial" "$out"
chown nick:runner "$out" && chmod 640 "$out"
find "$DIR" -name 'tfy-*.sql.gz' -mtime +"$KEEP_DAYS" -delete
echo "backup ok: $out ($(du -h "$out" | cut -f1))"
