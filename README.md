# TimeForYou

> _WebUntis tells you what your timetable is. TimeForYou tells you when it changes._

A mobile-first timetable companion for Eduvos students. It mirrors the institution's WebUntis
schedule, detects changes to it, and pushes those changes to the student's phone.

**Module:** ITMDA3-B34 · **Team:** 6 · **Repo:** `nbence-dev/TimeForYou`

---

## ⚠️ Status: planning

**There is no application code yet.** Deliverable 3 (2026-09-04) is documentation only. This
repository currently holds the project documents, the team's working agreement, and repo
configuration. The "Running it" section below describes the contract the build must satisfy —
it is the specification, not yet a working command.

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

That starts four containers: the API, the Worker, PostgreSQL, and a **mock WebUntis service**
that stands in for the real institution.

The demo path — the whole point of the mock — is:

1. Open the mock's admin page at `http://localhost:8000/admin`.
2. Scan the demo QR code it renders, using the app.
3. A seeded timetable appears.
4. Click **Cancel next lesson** on the admin page.
5. Within one poll cycle (~30 seconds in demo config) a real push notification arrives.

The entire pipeline — auth, polling, change detection, notification delivery — runs offline on
one machine. See brief §12.4.

---

## Running it (for a developer)

1. Set your machine up: **[`docs/dev-setup.md`](docs/dev-setup.md)** — Node, .NET, Python,
   Docker, and the Android emulator, for macOS and Windows.
2. Read how the team works: **[`CONTRIBUTING.md`](CONTRIBUTING.md)** — branches, commits, PRs,
   and the six ownership verticals. It is short and it is the actual agreement.
3. Make your env file:

```bash
cp .env.example .env
```

4. Handle secrets correctly: **[`docs/secrets.md`](docs/secrets.md)**. One page. Read it before
   your first PR.

---

## The documents

| Document                                   | What it is                                                                                                                           |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------ |
| [`PROJECT-BRIEF.md`](PROJECT-BRIEF.md)     | **Single source of truth.** Requirements, architecture, ERD, screens, decisions. Every deliverable is expanded from a section of it. |
| [`CONTRIBUTING.md`](CONTRIBUTING.md)       | How the six of us work without standing on each other.                                                                               |
| [`docs/dev-setup.md`](docs/dev-setup.md)   | Getting a machine that can run all four stacks.                                                                                      |
| [`docs/secrets.md`](docs/secrets.md)       | Where secrets live and how not to leak them.                                                                                         |
| [`docs/repo-setup.md`](docs/repo-setup.md) | How GitHub is configured, and how to check it when something looks broken.                                                           |
| [`grill-me-1.md`](grill-me-1.md)           | Record of the planning session — why decisions went the way they did, and what was rejected.                                         |

---

## Planned layout

Ownership is by feature vertical, not by language, so each person owns something demonstrable
end-to-end. Full table in [`CONTRIBUTING.md`](CONTRIBUTING.md) §1.

```text
app/                      Expo / React Native — Android build + PWA web export
  src/features/           onboarding · timetable · notifications · settings
  src/shared/             shared by everyone; additive changes only
server/                   one C# solution
  TimeForYou.WebUntis/    port of the webuntis npm package
  TimeForYou.Worker/      polling, change detection, notification dispatch
  TimeForYou.Data/        schema and migrations
  TimeForYou.Domain/      shared domain types
mock/                     Python / FastAPI — fake WebUntis + admin trigger page
docs/                     these documents
docker-compose.yml        API · Worker · mock · Postgres
```

---

## Tech

React Native (Expo) · ASP.NET Core · PostgreSQL · Python (FastAPI) · Docker Compose ·
Firebase Cloud Messaging (native push + Web Push)

Built for ITMDA3-B34 at Eduvos.
