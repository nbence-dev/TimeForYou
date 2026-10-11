#!/usr/bin/env bash
# Zero-downtime blue/green deploy for tfy-prod. Runs ON the VM, from the self-hosted runner.
#
#   IMAGE_PREFIX=ghcr.io/nbence-dev/timeforyou deploy/deploy.sh <short-sha>
#
# The images must already be pushed (the build job does that) and the runner already logged in to GHCR.
#
# Order matters, and every step before the switch can fail without users noticing:
#   1 guard disk  2 pull  3 stop old worker  4 start new release (--wait)  5 smoke test
#   6 switch Caddy  7 verify via Caddy  8 retire old release  9 clean up
# If anything fails before step 7 completes, the new release is removed, the old worker is started
# again and Caddy still points at the old release. Volumes are never touched.
set -euo pipefail

SHA="${1:?usage: deploy.sh <short-sha>}"
: "${IMAGE_PREFIX:?set IMAGE_PREFIX, e.g. ghcr.io/nbence-dev/timeforyou}"
TFY_DIR="${TFY_DIR:-/srv/tfy}"
CADDY_CTR="${CADDY_CTR:-tfy-core-caddy-1}"
MIN_FREE_GB="${MIN_FREE_GB:-5}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export SHA IMAGE_PREFIX

log() { printf '\n==> %s\n' "$*"; }
rel() { docker compose -p "tfy-$SHA" -f "$HERE/release.compose.yml" --env-file "$TFY_DIR/.env" "$@"; }
# project_containers <project> [extra docker ps filters...]: ids of all containers (any state) in a project
project_containers() { local p="$1"; shift; docker ps -aq --filter "label=com.docker.compose.project=$p" "$@"; }
# probe <url>: HTTP GET from inside the Caddy container, i.e. over the same network Caddy will use
probe() {
  local url="$1" _
  for _ in 1 2 3 4 5; do
    docker exec "$CADDY_CTR" wget -q -O /dev/null -T 5 "$url" && return 0
    sleep 2
  done
  echo "probe failed: $url" >&2
  return 1
}

OLD=""
[ -f "$TFY_DIR/live-sha" ] && OLD="$(cat "$TFY_DIR/live-sha")"
SWITCHED=""

rollback() {
  log "FAILED: removing release $SHA; the previous release keeps serving"
  rel logs --tail 40 2>&1 || true
  project_containers "tfy-$SHA" | xargs -r docker rm -f >/dev/null || true
  if [ -n "$OLD" ] && [ "$OLD" != "$SHA" ]; then
    project_containers "tfy-$OLD" --filter "label=com.docker.compose.service=worker" | xargs -r docker start >/dev/null || true
  fi
}
# shellcheck disable=SC2154  # rc is assigned inside the trap string itself
trap 'rc=$?; if [ "$rc" -ne 0 ] && [ -z "$SWITCHED" ]; then rollback; fi; exit "$rc"' EXIT

# --- 1. guard ---------------------------------------------------------------------------------
log "1/9 guard: core stack up, disk space"
[ "$(docker inspect -f '{{.State.Running}}' "$CADDY_CTR" 2>/dev/null)" = "true" ] \
  || { echo "Caddy ($CADDY_CTR) is not running; start the core stack first" >&2; exit 1; }
free_gb() { df --output=avail -BG /var/lib/docker | tail -1 | tr -dc '0-9'; }
if [ "$(free_gb)" -lt "$MIN_FREE_GB" ]; then
  echo "only $(free_gb)G free on /var/lib/docker; pruning unused images first"
  docker image prune -a -f --filter "until=72h" >/dev/null
  docker builder prune -f --filter "until=24h" >/dev/null || true
  [ "$(free_gb)" -ge "$MIN_FREE_GB" ] || { echo "still only $(free_gb)G free; aborting" >&2; exit 1; }
fi

if [ "$OLD" = "$SHA" ]; then
  log "release $SHA is already live; making sure it is up, nothing else to do"
  rel up -d --wait --wait-timeout 180
  SWITCHED=1
  exit 0
fi

# --- 2. pull ----------------------------------------------------------------------------------
log "2/9 pull images for $SHA"
rel pull --quiet

# --- 3. stop the old worker -------------------------------------------------------------------
# Two workers polling at once could send duplicate notifications; with a 10 minute poll the gap is invisible.
if [ -n "$OLD" ]; then
  log "3/9 stop old worker ($OLD)"
  project_containers "tfy-$OLD" --filter "label=com.docker.compose.service=worker" | xargs -r docker stop -t 30 >/dev/null
fi

# --- 4. start the new release -----------------------------------------------------------------
# The API migrates the database before it reports healthy, and the worker waits for that.
log "4/9 start release $SHA and wait for healthchecks"
rel up -d --wait --wait-timeout 180

# --- 5. smoke test ----------------------------------------------------------------------------
log "5/9 smoke test over the edge network"
probe "http://api-$SHA:8080/health"
probe "http://worker-$SHA:8081/healthz"
probe "http://web-$SHA/healthz"

# --- 6. switch --------------------------------------------------------------------------------
log "6/9 switch Caddy to $SHA"
cd "$TFY_DIR/caddy"
cat > live.caddy.next <<SNIPPETS
(live_api) {
	reverse_proxy api-$SHA:8080
}
(live_web) {
	reverse_proxy web-$SHA:80
}
(live_mock) {
	reverse_proxy mock-$SHA:8000
}
(live_worker) {
	reverse_proxy worker-$SHA:8081
}
SNIPPETS
cp -p live.caddy live.caddy.prev
mv -f live.caddy.next live.caddy   # rename over the old file; Caddy sees the directory, not the inode
restore_routes() {
  mv -f live.caddy.prev live.caddy
  docker exec "$CADDY_CTR" caddy reload --config /etc/caddy/Caddyfile >/dev/null 2>&1 || true
}
if ! docker exec "$CADDY_CTR" caddy validate --config /etc/caddy/Caddyfile >/dev/null 2>&1 \
   || ! docker exec "$CADDY_CTR" caddy reload --config /etc/caddy/Caddyfile >/dev/null 2>&1; then
  echo "Caddy rejected the new routes; restoring the previous ones" >&2
  restore_routes
  exit 1
fi

# --- 7. verify through Caddy ------------------------------------------------------------------
log "7/9 verify the live path through Caddy"
if ! probe "http://127.0.0.1:8080/health"; then
  echo "new release not answering through Caddy; restoring previous routes" >&2
  restore_routes
  exit 1
fi
SWITCHED=1
echo "$SHA" > "$TFY_DIR/live-sha"
rm -f live.caddy.prev

# --- 8. retire the old release ----------------------------------------------------------------
if [ -n "$OLD" ]; then
  log "8/9 retire release $OLD"
  project_containers "tfy-$OLD" | xargs -r docker stop -t 30 >/dev/null
  project_containers "tfy-$OLD" | xargs -r docker rm >/dev/null
fi
# Sweep leftovers of any earlier failed deploy: every tfy-* project except core and this one.
docker ps -a --format '{{.Label "com.docker.compose.project"}}' | sort -u | grep '^tfy-' \
  | grep -vxE "tfy-core|tfy-$SHA" | while read -r p; do project_containers "$p" | xargs -r docker rm -f >/dev/null; done || true

# --- 9. clean up ------------------------------------------------------------------------------
# Keep this release and the previous one (rollback = re-point Caddy), drop older release images.
# NB: `until=` filters on image CREATION time, so it is no good for "keep the last N releases".
log "9/9 clean up old images"
docker images --format '{{.Repository}}:{{.Tag}}' | grep "^$IMAGE_PREFIX/" | grep -vE ":($SHA${OLD:+|$OLD})\$" \
  | xargs -r docker rmi >/dev/null 2>&1 || true
docker image prune -f >/dev/null           # dangling layers only
docker builder prune -f --filter "until=24h" >/dev/null 2>&1 || true
# Never `--volumes`: db-data lives in a named volume.

log "done: $SHA is live"
docker system df
