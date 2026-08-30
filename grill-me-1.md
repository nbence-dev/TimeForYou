# Grill Session 1 — TimeForYou

**Date:** Saturday 2026-08-29 · **Duration:** ~75 min (approx. 10:26–11:41)
**Deliverable 3 due:** Friday 2026-09-04 — documents only, no code
**Output:** [`PROJECT-BRIEF.md`](./PROJECT-BRIEF.md) — the single source of truth for the team

This is the _record of how we got there_. The brief holds the decisions; this holds the
reasoning, the reversals, and what got killed along the way. Read this if you're wondering
_"why did we do it that way?"_ — read the brief if you just need the spec.

---

## 1. Where we started

The plan brought into the session:

- Mobile app fixing WebUntis/Eduvos gaps (no iCal export, no notifications)
- React Native, architecture per the Gemini-generated PNG in this repo
- **Split the backend C# / Python** so the one Python-capable teammate has a role
- Hosting on Supabase or Docker on a local Mac, behind a purchased domain
- Firebase for notifications; rewrite the `webuntis` npm package in C#
- Redis cache already dropped
- **Worry:** iPhone notifications need a paid Apple account; maybe build a web app too,
  or fall back to the 7-day sideload demo

By the end, four of those had changed materially.

---

## 2. Round-by-round

### Round 1 — foundations

Established the real problem, verified feasibility, sized the project.

- **Problem sharpened.** "WebUntis lacks iCal export" is a _feature gap_, not a problem — and
  invites the objection _"so email IT."_ Reframed around four defects Eduvos cannot toggle:
  not mobile-optimised, slow, no onboarding, changes are silent. iCal export stays a
  _feature_, not the thesis.
- **Feasibility confirmed.** Nicholas had already authenticated against the live Eduvos
  WebUntis using the npm package and the SSO URL from his QR code. This de-risked the entire
  project — the QR scanner is core, not decorative.
- **Scale settled.** 6 users, all team members. Kills every scale concern and confirms Redis
  was correctly dropped.
- **"Not production-ready, must look amazing for showcase"** became an explicit design
  constraint — with a second constraint: _a lecturer must be able to run it locally without
  access to the team's endpoints._

### Round 2 — architecture and demo strategy

- **Mock WebUntis service** proposed and accepted. Turned "notifications won't work for the
  marker" into the most impressive thing in the submission.
- **Feature boundary over language boundary** for splitting six people's work.
- **iCal subscription** preferred over one-off export.
- Deadline revealed: **documents due Friday**, which set the pace for everything after.

### Round 3 — the correction round

Nicholas asked _"are you 100% sure?"_ about the iOS notification claim. He was right to.
See §4 — this round reversed a recommendation.

### Round 4 — product definition

Screens, notification semantics, offline behaviour, iCal auth, name. Several pushbacks landed
(notification specificity, subscription-over-export); one was overruled and correctly so
(QR-only onboarding, no manual fallback — the institution only exposes QR).

### Round 5 — the rubric gap

Nicholas supplied the implementation rubric. It exposed a real problem: **the app as designed
was read-only**, but the rubric requires _two complete modules with full create/read/update/
delete_ and _input fields with validation_. A pure WebUntis mirror has neither.

Resolved by adding two modules that stay inside the product's scope:
**Lesson Notes** and **Custom Reminder Rules**. Personal (non-lecture) events were proposed,
rejected as scope creep, and parked as Phase 2.

---

## 3. Decisions

| Area             | Decision                                                                                  | Why                                                                                       |
| ---------------- | ----------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Name             | **TimeForYou**                                                                            | Short, non-institutional, reads as a product not an Eduvos system                         |
| Problem framing  | Slow / no onboarding / not mobile-optimised / silent changes                              | Untis product defects, not Eduvos config toggles — survives the "just email IT" objection |
| Android push     | Expo dev build + FCM                                                                      | Expo Go dropped remote push in SDK 53                                                     |
| iOS push         | **PWA + Web Push** (VAPID)                                                                | Only free route to server push on iPhone; no Apple account needed                         |
| iOS fallback     | Local notifications only, documented                                                      | If the PWA route fails the week-1 spike                                                   |
| Codebase         | One Expo/RN codebase, two output targets                                                  | Native Android build + web export as PWA                                                  |
| Backend          | Single C# solution; Worker as `BackgroundService`                                         | Language split bought a network boundary and a second pipeline for nothing at 6 users     |
| Python role      | **Mock WebUntis service (FastAPI)**                                                       | Genuinely independent, and the marker demo depends on it                                  |
| Team split       | Six **feature verticals**, not language lanes                                             | Everyone owns something demoable; nobody blocked on a foreign language                    |
| Hosting          | Docker Compose (incl. Postgres) on the Mac, Cloudflare Tunnel + domain                    | Marker runs the identical compose file; no environment divergence                         |
| Database         | **Postgres in a container — not Supabase**                                                | A cloud dependency the marker doesn't have is what breaks a marking session               |
| Identity         | Device-bound; `User` (keyed on WebUntis username) → many `Devices`                        | Accounts are _more_ work than device-bound and reintroduce onboarding friction            |
| Auth (rubric)    | QR = login, Unlink = logout, AES-GCM = credential handling, + biometric/PIN lock          | Satisfies rubric item 2 without a signup form                                             |
| Credentials      | AES-256-GCM, per-record nonce, key from env, key_version for rotation                     | Secret must be _recoverable_ to poll while the user is offline — hashing impossible       |
| Onboarding       | QR only, no manual entry                                                                  | Institution exposes access via QR only                                                    |
| Change detection | Compare subject/start/end/room/status, today → +14 days                                   | Diffing raw payloads produces false positives that destroy trust                          |
| Notifications    | ≤1 push per poll; name the change when there's one, summarise when many                   | Generic "your timetable changed" forces the user to open the app to learn anything        |
| Quiet hours      | 21:00–06:00 default, queued for morning                                                   |                                                                                           |
| Offline          | Offline-first, cached, `last synced` timestamp, never a spinner                           | The cache is the _speed_ work; offline is free once it exists                             |
| Calendar         | **Subscription feed**, not export                                                         | Export is a snapshot — stale the moment a class moves, plus an unsolvable dedupe problem  |
| CRUD modules     | Lesson Notes + Custom Reminder Rules                                                      | Rubric item 4 requires two; a pure mirror has none                                        |
| Home screen      | **Today**, not Week                                                                       | "Where am I supposed to be right now" should cost zero taps                               |
| Visual           | Near-black on off-white, one accent; red/amber/green semantic, always paired with an icon | Colour is never the sole carrier of meaning (NFR-13)                                      |

---

## 4. Reversals and corrections

Worth recording, because two of these were assumptions that would have cost real time.

**The iOS push correction — my error, caught by Nicholas.**
I initially claimed push worked in Expo Go on iOS without a paid account, based on a search
summary, and recommended **killing the PWA**. Nicholas pushed back with _"are you 100% sure?"_
Reading Expo's primary docs showed the opposite: Expo Go dropped push support in SDK 53
(_"In SDK 52 and earlier, Expo Go used Expo's credentials…"_ — past tense). Both platforms need
a development build, and iOS APNs credentials require the paid programme.

Then Apple's own WebKit documentation settled it: **Web Push to a Home Screen web app needs no
Apple Developer Program membership.** So the PWA — Nicholas's idea in his opening message —
is the only free route to server push on iPhone. The recommendation to drop it was wrong and
was reversed. _Lesson: verify against primary sources before recommending a plan reversal._

**Export → subscription feed.**
The instinct that a one-time `.ics` export would be "less overhead" was backwards. Export
requires solving duplicate/stale-event cleanup (Nicholas spotted this himself: _"we must just
think of a way to delete the old one"_). A subscription feed deletes that whole problem class
and is ~60 lines of C#.

**Accounts → device-bound.**
"Real accounts sound way easier" — but the `Device` table exists either way (one user with an
Android build _and_ an iPhone PWA is two push subscriptions). Accounts _add_ screens on top,
they don't replace anything.

**Read-only → two CRUD modules.**
The rubric caught what the design missed. Found in planning rather than week 10.

---

## 5. Overruled — and right to be

- **QR-only onboarding, no manual fallback.** I flagged this as a single point of failure
  (if camera access fails in the iOS PWA, iPhone users cannot onboard at all). Overruled:
  the institution only exposes QR. The risk is recorded in §11.2 of the brief and the spike
  is open item 7 — it remains genuinely load-bearing.
- **Two devices required for onboarding.** Accepted as a documented assumption from the
  original thesis, not a defect.

---

## 6. Killed

Redis · Supabase · separate Python worker microservice · email/password accounts ·
manual credential entry · one-time `.ics` export · native iOS app with APNs ·
Expo Go for push · generic "your timetable changed" notification · diffing whole payloads ·
personal (non-lecture) events · lecturer/admin roles · homework, grades, attendance ·
social features · multi-institution support

Each is in §16 of the brief with its rejection reason — a marker asking _"why didn't you…"_
should find the answer already written down.

---

## 7. Open items

Full list in §15 of the brief. The ones that actually matter:

| Priority | Item                                                                                  | Owner    |
| -------- | ------------------------------------------------------------------------------------- | -------- |
| **High** | Spike: QR camera scanning inside the iOS PWA — blocks iOS onboarding entirely         | —        |
| **High** | `TODO-VERIFY` Are WebUntis lesson IDs stable across an edit? Sets the ERD primary key | Nicholas |
| **High** | 3 × `TODO-MEASURE`: WebUntis load time, tap count, screenshots (~1 hour total)        | —        |
| Medium   | Spike: Expo web export via react-native-web                                           | —        |
| Medium   | Spike: Web Push service worker + FCM JS SDK on iOS Home Screen app                    | —        |
| Medium   | Student survey on missed/changed classes — PRD evidence                               | —        |
| Medium   | Email Eduvos IT re: iCal export; keep the reply                                       | —        |
| Low      | Assign names to the six verticals (§14)                                               | —        |

The three `TODO-MEASURE` items are the cheapest marks available: one hour turns NFR-1 and
NFR-3 from asserted into evidenced, and supplies the "before" half of the showcase.

---

## 8. Produced

| Artefact                        | Location                                                             |
| ------------------------------- | -------------------------------------------------------------------- |
| Project brief (source of truth) | `PROJECT-BRIEF.md`                                                   |
| Brief as a shareable page       | https://claude.ai/code/artifact/68720589-a493-4121-98fe-9116a2514aed |
| This session record             | `grill-me-1.md`                                                      |

Superseded: `Gemini_Generated_Image_azahreazahreazah.png` — the architecture in §12 of the
brief replaces it (no Redis, no Cloudflare-tunnel-to-Redis, single C# solution, mock service
added).

---

## 9. For session 2

Likely topics once the spikes report back:

- Polling cadence in production vs. demo config
- Wireframe production — tooling and who draws which of the 12 screens
- API surface: endpoint list and request/response shapes
- ERD finalisation once lesson-ID stability is known
- Test strategy for the diff engine using mock fixtures
- Showcase script — the exact sequence of taps for the live demo
