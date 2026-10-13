# Triggering a push notification by hand

How to change a lesson in the mock and make the worker send the resulting push to a phone. Use it
for manual test PUSH-3 and for rehearsing the showcase demo.

Run everything from the repo root.

**Easiest:** open `http://localhost:8000/demo` (on the tfy-prod VM, tunnel first:
`ssh -L 8000:localhost:8000 nick@192.168.3.61`). It lists upcoming lessons with Cancel, Restore,
Change room and Move buttons, plus **Nudge now** and **Reset mock**. The steps below are the `curl`
equivalent, and `restore-lesson` is also available: `curl -X POST localhost:8000/mock/restore-lesson/<id>`.

## Before you start

- Stack is up: `docker compose --profile tunnel up -d`
- The PWA is installed on the phone with notifications allowed (PUSH-1).
- `.env` has `FIREBASE_SERVICE_ACCOUNT_PATH`, `GOOGLE_APPLICATION_CREDENTIALS`, `FIREBASE_PROJECT_ID`
  and `WEB_APP_URL` set. After pulling new code, rebuild the worker:
  `docker compose --profile tunnel up -d --build worker`.

## 1. Find a lesson ID

IDs look like `1007-2026-10-09`, not a bare number. List some upcoming scheduled lessons:

```bash
docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}" -c "select untis_lesson_id, subject, start_ts, status from lesson_snapshots where start_ts > now() and status = '"'"'scheduled'"'"' order by start_ts limit 5"'
```

## 2. Change it in the mock

```bash
curl -X POST localhost:8000/mock/cancel-lesson/1007-2026-10-09
```

Other changes:

```bash
curl -X POST localhost:8000/mock/change-room/1007-2026-10-09 \
  -H 'Content-Type: application/json' -d '{"room":"Z99"}'
```

`/mock/move-lesson/{id}` also exists; see `mock/app` for its request body. An unknown ID returns 404
and nothing happens.

## 3. Poll now

```bash
curl -X POST localhost:8081/nudge
```

Returns `{"job_id":"..."}` with status 202. Without it you wait for the next scheduled poll (up to
10 minutes). The worker port is `${WORKER_PORT:-8081}`.

## 4. Check the result

- Background the PWA or lock the phone. iOS may not show a banner while the app is in the
  foreground. A notification should arrive within seconds; tapping it should open the Changes tab.
- Follow the worker: `docker compose logs -f worker | grep -E "Push|FCM|fail"`
- See the stored change:

  ```bash
  docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}" -c "select change_type, detected_at from change_log order by detected_at desc limit 5"'
  ```

## 5. Reset for the next run

```bash
curl -X POST localhost:8000/mock/reset
```

## If nothing arrives

| Symptom | Cause | Fix |
| --- | --- | --- |
| `curl` to the mock returns 404 | Wrong lesson ID | Use an `untis_lesson_id` from step 1 |
| No new row in `change_log` | The lesson already had that status, so there is no diff | Reset the mock, or pick another lesson |
| Worker logs `Push (logged, not sent)` | Old worker image, still on the logging sender | Rebuild: `docker compose --profile tunnel up -d --build worker` |
| Worker logs "will be logged, not sent" at startup | Credentials path unset or the file is not mounted | Check `FIREBASE_SERVICE_ACCOUNT_PATH` and `GOOGLE_APPLICATION_CREDENTIALS` in `.env` |
| `fail: ... not a valid FCM registration token` for one device | A junk or stale token in `devices` (for example the `abc123` Android test row) | Expected. It is logged and the other devices still get their push |
| FCM accepted every send but the phone shows nothing | App in the foreground, notification permission lost after a reinstall, or a stale token | Background the app, re-run "Turn on notifications", and check the newest `web_push` row in `devices` |
