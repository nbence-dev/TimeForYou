# Grill Session 2 — TimeForYou

**Date:** Monday 2026-09-21
**Deadline:** Thursday 2026-10-23 — showcase demo (~4.5 weeks out)
**Output:** [Issue #3 on GitHub](https://github.com/nbence-dev/TimeForYou/issues/3) — the full Phase 1 spec, `ready-for-agent`
**Companion:** [`grill-me-1.md`](./grill-me-1.md) held the pre-scaffold session; this holds the reasoning that shaped the actual build plan.

Read this if you're wondering _"why did we change that from grill-me-1?"_ — read the GitHub issue if you just need the spec.

---

## 1. Where we started (this session)

The plan brought into the session, after the scaffold was already committed:

- React Native (Expo). **iOS = PWA. Android = native.**
- Backend: PostgreSQL + **C#** (planned to rewrite the `thenoim/webuntis` npm package in C#).
- Mock service in Python (for demo day).
- Hosted on Nicholas's MacBook via Docker. External access via **Cloudflare Tunnel**.
- **Supabase dropped** since grill-me-1. **Redis dropped** since the last diagram.
- **Firebase Cloud Messaging "for emails"** (this contained a factual error — flagged in Round 1).
- QR-code sign-in, no login/registration, multiple devices per user.
- iCal calendar subscription.
- Phase 1: notes on a lesson. Reminder-time customisation only if time.

Six things ended up meaningfully changed by the end of the session.

---

## 2. Round-by-round

### Round 1 — foundational challenges

Six prereq-free questions. The point of Round 1 was to surface anything factually wrong or load-bearing-but-unjustified before building on top of it.

| Q | Topic | Recommendation | Answer |
|---|---|---|---|
| Q1 | iOS PWA vs unified Expo | Unified Expo | **PWA kept** — no Apple Developer account (student project, R50 domain is the only budget). Team is on iOS 27 so PWA push is viable. |
| Q2 | "FCM sends emails" | FCM sends push, not email — clarify | **Push only.** Factual slip corrected. |
| Q3 | C# rewrite of `webuntis` npm | Node sidecar | User set a 1-day budget → I estimated 3–5 days minimum → **sidecar** it is. |
| Q4 | QR encoding | WebUntis's own mobile-SSO QR | Confirmed. `{school, user, key, url}` stored server-side, AES256-encrypted. |
| Q5 | MacBook host reliability | Fine for prototype | **Prototype for a uni showcase.** Uptime isn't graded. |
| Q6 | Deadline | Need to know | **Oct 23, 2026.** ~4.5 weeks. |

**Big reversal:** the C# rewrite of the webuntis package. Grill-me-1 had it as a deliberate "give the Python-capable person a role, keep the rest C#" call. This session cut it because the effort estimate blew past the time-box; the sidecar keeps the same C# purity at the boundary without paying the reverse-engineering tax.

### Round 2 — architecture shape

Sub-agent tried to read the deliverable DOCX (blocked — no docx extractor in read-only mode) and confirm TheNoim's QR auth (blocked — no WebFetch). Proceeded on prior knowledge.

| Q | Topic | Answer |
|---|---|---|
| Q7 | Sidecar vs C# port | **Node sidecar** — a Docker container wrapping `thenoim/webuntis`. |
| Q8 | Where the WebUntis SSO secret lives | **Backend-persistent**, AES256-encrypted in Postgres. |
| Q9 | Service topology | **API + Worker** (auth/notification collapsed to libraries, not services). Total containers = 6 with sidecar + mock + Postgres + Cloudflare Tunnel. |
| Q10 | Polling / job state (no Redis) | Deferred — user wanted Hangfire explained; done in Round 3. |
| Q11 | Change detection semantics | **Material changes only** (start_ts, end_ts, room, status). Grouped per poll cycle → **one notification for N changes**. |
| Q12 | Mock scope | **Both** — protocol-faithful for the sidecar AND admin endpoints for demo-triggered changes. |
| Q13 | Team allocation | Only **~3 of 6** reliably deliver. Scope must be trimmed. |

**Big reversal:** the four-service backend from the original architecture diagram (API / Worker / Auth / Notification) collapsed to two services + shared libraries. Redis being gone made three of the four boundaries pointless.

### Round 3 — the Hangfire explainer + notes + iOS A2HS

Q15's clarification took a full round: the user thought Hangfire was an endpoint. It's a .NET library that gives you scheduling, retry, and a Postgres-backed dashboard. Once explained, the trade-off table made it a clear yes.

| Q | Topic | Answer |
|---|---|---|
| Q15 | In-memory timer vs Hangfire | **Hangfire.** Dashboard is showcase gold. |
| Q16 | Notes multi-device sync | **Server-authoritative**, pull on open + poll every 60s while app open. Optimistic local writes. |
| Q17 | iOS PWA A2HS fallback | **Soft-gate** — persistent dismissable banner "notifications off, install to home screen to enable". |

### Round 4 — surface & UX details

| Q | Topic | Answer |
|---|---|---|
| Q18 | AES256 key location | **`.env` on MacBook**, gitignored, never rotated. Fine for 6-user prototype. |
| Q19 | Cloudflare Tunnel scope | **Only API + ICS public.** Mock, Hangfire, Postgres LAN-only. |
| Q20 | ICS URL format | **Opaque 64-char token in path**: `app.<domain>/ics/<token>.ics`. Same trust model as Google Calendar's secret URLs. |
| Q21 | Notification body | **Lead change + "(+N more)"**. Title: "Timetable updated". Body: "Maths moved to Room 4B (+4 more changes)". |

### Round 5 — operational details

| Q | Topic | Answer |
|---|---|---|
| Q22 | Poll interval | **10 minutes.** 36 req/hr per user against WebUntis — safe. |
| Q23 | Mock admin endpoint auth | **None** — LAN-only is the security boundary. Demo operator `curl`s from a laptop on the same wifi. |
| Q24 | FCM ownership | **One member's Firebase project**, credentials in shared team password manager. |
| Q25 | Deployment | **Continuous deployment on every push to main.** Baked in a hard **CD freeze from Oct 21** (48h before showcase) as a compromise. |

---

## 3. What flipped since grill-me-1

Grill-me-1's brief was documentation-only; the code had not started. This session hardened a few things and reversed others once implementation reality landed:

- **Rewriting `thenoim/webuntis` in C# → Node sidecar.** Grill-me-1 accepted the rewrite because it kept the Python-capable person on the mock service and everything else C#. This session showed the effort estimate was 3–5 dev-days minimum, not the "≤1 day" the user had budgeted, and cut it.
- **Four backend services (Api / Worker / Auth / Notification) → two + shared libs.** Redis being dropped collapsed the reason for separate Auth and Notification services. The six existing C# projects survive, but Auth and Notification are libraries used by Api and Worker respectively rather than their own executables.
- **`TimeForYou.WebUntis` csproj repurposed.** Was going to be the C# port of the npm package; now it's a thin `HttpClient` wrapper that calls the Node sidecar. Same public interface for consumers.
- **"FCM for emails" corrected to "FCM for push."** FCM does not send email. If email is ever required by the rubric, that's new scope.
- **CD every push confirmed** — with a hard demo-week freeze added as a safety net.

---

## 4. What got killed

- **Redis** (already dropped before this session, confirmed here).
- **Supabase** (already dropped before this session, confirmed here).
- **The C# port of `thenoim/webuntis`.** Sidecar instead.
- **Standalone Auth and Notification services.** Now libraries.
- **A2HS hard-gate for iOS onboarding.** Soft-gate banner instead.
- **Frontend automated tests (Detox/Maestro).** Manual test plan only. Deliberate scope cut for the 4.5-week timeline with 3 committed devs.
- **Reminder-time customisation.** Deferred (already flagged as "if extra time" in the brief).
- **Realtime note push across devices.** Notes sync on app-open + poll only, no FCM on note edit.
- **Any auth on the Hangfire dashboard.** LAN-only is the security model; not exposed to the tunnel.
- **Any auth on the mock admin endpoints.** Same reason.

---

## 5. Testing seams

Two seams, both in `TimeForYou.Tests`:

- **HTTP boundary integration** — real Postgres + mock + sidecar + worker (Hangfire paused so tests drive the poll explicitly) + API. Stubbed FCM sender captures payloads. Covers auth, polling, diffing, notes CRUD, ICS output, multi-device linking. This is the highest seam and does most of the work.
- **Domain unit tests** — pure C# tests over the diff algorithm in `TimeForYou.Domain`. No I/O. Catches the subtle edge cases (teacher-name changes should NOT trigger a notification, cancellation ordering rule, etc.) fast.

Rejected: a third frontend E2E seam. Cost/benefit not there for the timeline.

---

## 6. Outstanding risks

- **Rubric unread.** The deliverable DOCX could not be extracted in read-only mode. This spec optimises for engineering quality, not marks. Verify FR/NFR/grading criteria against the DOCX before Week 1.
- **Team reliability.** ~3 of 6 reliably delivering. Reminder-customisation, dashboard polish, and calendar-notes-in-description are the first cuts if slippage appears.
- **iOS PWA A2HS UX friction.** Rehearse the A2HS step in the demo script. Users who skip it get no push.
- **MacBook host reliability during showcase.** Disable sleep, plug in ethernet, keep a hot-spare laptop with `docker compose up` ready.
- **CD in demo week.** Freeze `main → MacBook` auto-deploy on Oct 21.

---

## 7. Timeline (from the spec)

| Week | Dates | Goal |
|---|---|---|
| 1 | Sep 21–27 | OpenAPI contract. `docker compose up` all 6 healthy. Sidecar wraps `thenoim/webuntis`. Firebase project up. |
| 2 | Sep 28–Oct 4 | API endpoints live. Postgres schema. Worker polls one seeded user end-to-end vs mock. |
| 3 | Oct 5–11 | Frontend screens. FCM Web + Android registration. First real notification received end-to-end. |
| 4 | Oct 12–18 | Cloudflare Tunnel live. Real WebUntis via sidecar. Cross-device sync + ICS validated. Demo script drafted. |
| 5 | Oct 19–23 | **CD freeze Oct 21.** Bug bash, rehearsals, MacBook prep, **showcase Oct 23**. |

---

## 8. Where the decisions live

- **This file** — reasoning, reversals, what got killed and why.
- **[GitHub issue #3](https://github.com/nbence-dev/TimeForYou/issues/3)** — the spec itself (problem, solution, 29 user stories, implementation & testing decisions, out-of-scope).
- **[`PROJECT-BRIEF.md`](./PROJECT-BRIEF.md)** — team-facing single source of truth from grill-me-1. Update it once implementation starts diverging from the brief.
