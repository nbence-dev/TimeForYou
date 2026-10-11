# deploy/ — the shared TimeForYou instance (tfy-prod)

Owned by V6 (platform). This folder is for the always-on instance on the team's Proxmox VM.
It is separate from the root `docker-compose.yml`, which stays the "clone, `docker compose up`" file for markers.

## How a deploy works

`.github/workflows/deploy.yml` runs on every push to `main`:

1. **build** (GitHub-hosted): build and push `api`, `worker`, `mock`, `web` to GHCR, tagged with the first 12 characters of the commit SHA. `web` is the Expo PWA export, so the five `EXPO_PUBLIC_*` values come from repository **variables** (they are public by design).
2. **deploy** (self-hosted runner on the VM): `deploy.sh` starts the new release next to the old one, health-checks it, switches Caddy to it, then removes the old one. If anything fails before the switch completes, the old release keeps serving and the new one is removed.

| File | What it is |
|---|---|
| `core.compose.yml` | Long-lived: Postgres, Caddy, cloudflared (profile `tunnel`). A deploy never touches it. |
| `release.compose.yml` | One release (api, worker, mock, web), run as Compose project `tfy-<sha>`. |
| `caddy/` | The edge proxy's static `Caddyfile` and the placeholder `live.caddy` that each deploy rewrites. |
| `web/` | Image for the PWA (Caddy serving `app/dist`). |
| `deploy.sh` | The nine-step blue/green deploy. |
| `ops/` | systemd timers on the VM: nightly `pg_dump`, weekly Docker prune, disk-space check. |

## Rules that come with this

- **Migrations must be additive for one release.** The API applies EF migrations at startup, and during a deploy the OLD API keeps serving against the already-migrated schema. Add columns/tables first; drop or rename in a later release. Rolling the code back does not roll the schema back.
- The mock's state is in memory, so every deploy resets it.
- Nothing in a release addresses a bare service name (`mock`, `api`): two releases share a network for a few seconds. Use the `<service>-<sha>` aliases.
- Never run `docker ... prune --volumes` on the VM: `db-data` is a named volume.
