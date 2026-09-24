# TimeForYou

> _WebUntis tells you what your timetable is. TimeForYou tells you when it changes._

A mobile-first timetable companion for Eduvos students. It mirrors the institution's WebUntis
schedule, detects changes to it, and pushes those changes to the student's phone.

**Module:** ITMDA3-B34 · **Team:** 6 · **Repo:** `nbence-dev/TimeForYou`

**Just cloned this?** Go to [Running it (for a developer)](#running-it-for-a-developer) — six
steps, and step 6 tells you whether your machine is set up correctly before you write anything.

---

## ⚠️ Status: planning

**There are no features yet.** Deliverable 3 (2026-09-04) is documentation only. This repository
holds the project documents, the team's working agreement, repo configuration, and — as of
2026-08-31 — the scaffolded project tree: an Expo app, six C# projects, and a FastAPI mock, all
of which build and do nothing. The "Running it" section below describes the contract the build
must satisfy. `docker-compose.yml` is not written yet, so it is the specification, not yet a
working command.

The build plan lives in **[issue #3](https://github.com/nbence-dev/TimeForYou/issues/3)** and its
eight epics. Where this README and issue #3 disagree, **#3 wins** — it is newer.

---

## What it does

- **QR onboarding** — scan the WebUntis QR code, no signup form, no password to remember.
- **Today screen** — opens instantly from a local cache, online or off. "Where am I supposed to
  be right now" costs zero taps.
- **Change detection** — a server polls WebUntis and pushes a notification that _names_ the
  change: which lesson, cancelled or moved, and where to.
- **iCal subscription feed** — puts lectures in the calendar app you already use, and keeps them
  correct when they move.
- **Lesson Notes** and **Custom Reminder Rules** — the two read-write modules.

Android gets native push; iPhone runs as an installed PWA using Web Push. There is no native
iOS app and no paid Apple account involved.

---

## Running it (for a lecturer or marker)

**Prerequisite: Docker Desktop. Nothing else.** No Eduvos credentials, no network access to the
team's server, no Node or .NET installed.

```bash
git clone https://github.com/nbence-dev/TimeForYou.git
cd TimeForYou
cp .env.example .env          # then follow the CHANGEME comments inside
docker compose up
```

That starts five containers: the API, the Worker, PostgreSQL, a **mock WebUntis service** — the
system's only backend, since there is no reachable real WebUntis tenant to fall back to — and a
Cloudflare Tunnel (team instance only — the marker never needs it).

The demo path — the whole point of the mock — is:

1. Scan the demo QR code from the mock's seeded fixtures, using the app.
2. A seeded timetable appears.
3. Trigger a change against the mock from any machine on the same network:

   ```bash
   curl -X POST http://localhost:8000/mock/cancel-lesson/42
   curl -X POST http://localhost:8000/nudge     # skip the wait, poll now
   ```

4. Within one poll cycle a real push notification arrives. `/nudge` bounds that to a few seconds
   rather than up to ten minutes.

These endpoints are deliberately **unauthenticated**. They are never exposed through the tunnel;
the LAN is the security boundary.

The entire pipeline — auth, polling, change detection, notification delivery — runs offline on
one machine. See brief §12.4.

---

## Running it (for a developer)

1. Set your machine up: **[`docs/dev-setup.md`](docs/dev-setup.md)** — Node, .NET, Python,
   Docker, and the Android emulator, for macOS and Windows.
2. Read how the team works: **[`CONTRIBUTING.md`](CONTRIBUTING.md)** — branches, commits, PRs,
   and the ownership verticals. It is short and it is the actual agreement.
3. Make your env file:

```bash
cp .env.example .env
```

4. Handle secrets correctly: **[`docs/secrets.md`](docs/secrets.md)**. One page. Read it before
   your first PR.

5. Install each stack's dependencies. Nothing is vendored, so a fresh clone builds nothing until
   you have run all three:

```bash
# app — Node 22, check with `node -v` first; nvm is per-shell
cd app && npm install && cd ..

# server — restores NuGet for all six projects
cd server && dotnet restore && cd ..

# mock — the venv is git-ignored, so everyone makes their own
cd mock && python3 -m venv .venv && .venv/bin/pip install -r requirements.txt && cd ..
```

6. Confirm the baseline works before you branch. All three must pass on a clean clone:

```bash
cd app    && npx tsc --noEmit    # no output = clean
cd ../server && dotnet build     # 0 warnings, 0 errors — warnings are errors here
cd ../mock   && .venv/bin/python -c "import fastapi"
```

If `dotnet build` fails on a package advisory (`NU1903`), that is
`server/Directory.Build.props` doing its job — bump the package, don't suppress the warning.

---

## The documents

| Document                                   | What it is                                                                                                                           |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------ |
| [`PROJECT-BRIEF.md`](PROJECT-BRIEF.md)     | **Single source of truth.** Requirements, architecture, ERD, screens, decisions. Every deliverable is expanded from a section of it. |
| [`CONTRIBUTING.md`](CONTRIBUTING.md)       | How the six of us work without standing on each other.                                                                               |
| [`docs/dev-setup.md`](docs/dev-setup.md)   | Getting a machine that can run all four stacks.                                                                                      |
| [`docs/secrets.md`](docs/secrets.md)       | Where secrets live and how not to leak them.                                                                                         |
| [`docs/repo-setup.md`](docs/repo-setup.md) | How GitHub is configured, and how to check it when something looks broken.                                                           |
| [`docs/backlog-process.md`](docs/backlog-process.md) | How the backlog is built, one wave at a time. Read before creating any issue.                                  |
| [`docs/openapi.yaml`](docs/openapi.yaml)   | Hand-authored OpenAPI 3.1 contract for every Phase 1 endpoint.                                                                       |
| [`CLAUDE.md`](CLAUDE.md)                   | Orientation for AI agents working in this repo.                                                                                      |
| [`grill-me-1.md`](grill-me-1.md)           | Record of the planning session — why decisions went the way they did, and what was rejected.                                         |

---

## Layout

Source is organised by what the code does, not by who owns it. Ownership is by feature vertical so
each person delivers something demonstrable end-to-end, but nobody is fenced out of a directory —
full table in [`CONTRIBUTING.md`](CONTRIBUTING.md) §1.

```text
app/                      Expo / React Native — Android build + PWA web export
  src/app/                routes only. Every file here is a URL (Expo Router)
  src/features/           onboarding · timetable · changes · notes · reminders · settings
  src/shared/             api · components · hooks · push · storage · theme
  public/                 PWA: manifest.json, icons, service worker
server/                   one C# solution — TimeForYou.slnx
  TimeForYou.Api/         REST endpoints, iCal subscription feed  (+ Dockerfile)
  TimeForYou.Worker/      polling, change detection, notification dispatch  (+ Dockerfile)
  TimeForYou.WebUntis/    HttpClient facade over mock — mock is the only backend
  TimeForYou.Data/        schema and migrations
  TimeForYou.Domain/      shared domain types
  TimeForYou.Tests/       diff-engine tests, fed by the mock's fixtures
  Directory.Build.props   pins net10.0 and warnings-as-errors for all six projects
mock/                     Python / FastAPI — fake WebUntis + admin trigger endpoints
  app/ · fixtures/ · Dockerfile
docs/                     these documents
docker-compose.yml        API · Worker · mock · Postgres · cloudflared
```

The `Dockerfile`s and `docker-compose.yml` exist but are **placeholders** — each records what
belongs in it and nothing more, so the paths are fixed before anything references them.
`docker compose up` does not work yet.

**Two rules hold this together.** `src/app/` is routes and nothing else — Expo Router turns any
default-exporting file in there into a URL. And `features/*` import from `shared/*`, never from
each other; when two features need the same thing, it moves to `shared/`.

---

## Tech

React Native (Expo) · ASP.NET Core · PostgreSQL · Python (FastAPI) · Docker Compose ·
Firebase Cloud Messaging (native push + Web Push)

Built for ITMDA3-B34 at Eduvos.
