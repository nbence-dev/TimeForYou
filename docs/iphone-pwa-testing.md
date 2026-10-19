# Testing the PWA on a real iPhone

How to run the web build on an iPhone as an installed Home Screen app, without Expo Go. Written
2026-10-08 while testing #67 (add-to-home-screen). Redo it any time from the top.

## Why this needs HTTPS and a tunnel

iOS only treats a site as an installable app, and only gives it camera and push, over HTTPS. The
phone also cannot reach `localhost`, so both the web app and the API need public HTTPS URLs.

## One-time setup

### 1. Route the API through the Cloudflare tunnel

The tunnel is managed remotely, so its hostnames live in the Cloudflare dashboard, not in the repo.

1. Cloudflare Zero Trust → Networks → Tunnels → the team tunnel → Edit → Public Hostname.
2. Add a hostname: subdomain `api`, domain `timeforyou.co.za`, path blank, service `HTTP`,
   URL `api:8080`.
   - Use `api:8080`, not `localhost:8080`: `cloudflared` runs in its own container.
   - Use a subdomain. The apex `timeforyou.co.za` already had a DNS record, and Cloudflare refuses
     to create a route over it ("A DNS record with this name already exists").
3. `CLOUDFLARE_TUNNEL_TOKEN` must be set in the root `.env`, and the stack started with
   `docker compose --profile tunnel up`.
4. Check it: `curl -i https://api.timeforyou.co.za/health` should return `200`.
   A Cloudflare "Not Found" page for every path means the hostname rule is missing.

CORS is enabled on the API (#97), so a web app on a different origin can call it.

### 2. Route the frontend through the same tunnel

Added the same day, replacing the throwaway quick tunnel.

1. Cloudflare DNS had an `A` record for `timeforyou.co.za` pointing at `169.239.180.4`, a
   HostAfrica placeholder (it only serves "Not Found"). Delete it, otherwise Cloudflare refuses
   to create the route. Email is unaffected (MX is a separate record).
2. Add a public hostname: subdomain blank, domain `timeforyou.co.za`, path blank, service `HTTP`,
   URL `host.docker.internal:3000`. The Mac serves `app/dist` on `:3000` with `npx serve`.
3. Set `PUBLIC_BASE_URL=https://api.timeforyou.co.za` in the root `.env` and recreate the API
   (`docker compose --profile tunnel up -d api`). `GET /calendar` builds the ICS feed URL from it,
   and behind `cloudflared` the API otherwise sees plain `http`.

| Hostname | Serves |
| --- | --- |
| `timeforyou.co.za` | the web app (static export) |
| `api.timeforyou.co.za` | the API: `/qr-scan`, `/timetable`, `/changes`, `/devices`, `/calendar`, `/notes`, `/ics/*` |
| `www.timeforyou.co.za` | not routed yet; reserved for the showcase demo site |

## Running it again later (after a reboot)

`docker compose --profile tunnel up` does **not** bring up the whole site. It starts the backend
(api, worker, mock, postgres, cloudflared), so `api.timeforyou.co.za` works. The web app on
`timeforyou.co.za` is served by `npx serve` in a terminal on the Mac. Close that terminal or reboot
and the apex returns a 502 while the API keeps working.

```bash
docker compose --profile tunnel up -d
cd app
npx expo export -p web      # only if app code or app/.env.local changed
npx serve dist -l 3000
```

Open follow-up, not done: add a `web` service to `docker-compose.yml` (nginx serving `app/dist`)
and change the apex route from `host.docker.internal:3000` to `web:80`, so one command starts
everything. It makes six containers where #3 specifies five, so #3 needs a note first.
Do this before the CD freeze on 2026-10-21 so showcase day does not depend on a terminal window.
The bundle still has to be rebuilt whenever app code changes, because `EXPO_PUBLIC_*` values are
compiled in.

## Every test run

1. Start the backend: `docker compose --profile tunnel up`.
2. Point the app at the API. In `app/.env.local` (gitignored):
   ```
   EXPO_PUBLIC_API_URL=https://api.timeforyou.co.za
   ```
   `EXPO_PUBLIC_*` values are compiled into the bundle, so rebuild after any change.
3. Build and serve the static web export:
   ```bash
   cd app
   npx expo export -p web
   npx serve dist -l 3000
   ```
   `app/dist` goes stale quickly. If the add-to-home-screen route or the `apple-mobile-web-app`
   tags are missing, you served an old build.
4. Give the web app a public HTTPS URL with a quick tunnel (new terminal):
   ```bash
   cloudflared tunnel --url http://localhost:3000
   ```
   It prints `https://<random-words>.trycloudflare.com`. The URL changes every time the tunnel
   restarts, and the tunnel dies when its terminal closes.
5. On the iPhone, open that URL in **Safari**. Chrome on iOS cannot install PWAs.
6. Get a QR code from the mock (Postman: reset the mock, then GET its QR endpoint on the Mac,
   `127.0.0.1:8000`). Scan it from the phone. The Postman call only produces the code. The phone
   sends what it scans to `https://api.timeforyou.co.za/qr-scan`.
7. Run the A2HS cases in `docs/manual-test-plan.md`.

## Gotchas we hit

| Symptom | Cause | Fix |
| --- | --- | --- |
| "Couldn't reach the server" on scan | The router's DNS cached `NXDOMAIN` for `api.timeforyou.co.za` from before the record existed. Public resolvers (1.1.1.1, 8.8.8.8) resolved it fine; the Mac and phone use the router. | Phone on mobile data, or set the phone's DNS to 1.1.1.1 and 8.8.8.8, or wait 15–30 min. Check with `dig api.timeforyou.co.za @1.1.1.1`. |
| `curl` to the API fails on the Mac, works elsewhere | Same stale router cache | `curl --resolve api.timeforyou.co.za:443:<cloudflare-ip> https://api.timeforyou.co.za/health` bypasses it |
| Scan screen shows again after Add to Home Screen | A Home Screen app has its own storage, separate from Safari, so the token is not shared | Scan again inside the installed app. Known review finding on #67 |
| Old icon / old build after rebuilding | iOS caches installed PWAs hard | Delete the Home Screen icon, clear Safari website data, re-add |
| Token skips the scan between runs | Token still in `localStorage` | Settings → Safari → Advanced → Website Data, or use a private tab |

## What this branch does not do yet

- No `manifest.json` or `apple-touch-icon`. iOS uses a screenshot as the icon and may withhold push.
- No `viewport-fit=cover`, so safe-area insets are 0 in standalone mode.
- **Nothing registers a push token.** No code calls Firebase `getToken` or `api.registerDevice`, so
  `devices.push_token` is always empty. Check with:
  ```bash
  docker exec code-db-1 psql -U timeforyou -d timeforyou -c \
    "select id, platform, push_token, created_at from devices order by created_at desc limit 5"
  ```
- To test FCM by hand you need a token from the installed Home Screen app (iOS gives no push
  permission in a Safari tab) plus the Web Push VAPID key from Firebase console → Project settings →
  Cloud Messaging → Web Push certificates. Then FCM console → Messaging → Send test message.
