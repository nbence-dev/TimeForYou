# TimeForYou — working in this repo

University project (ITMDA3-B34). A mobile timetable companion that mirrors WebUntis, detects
changes, and pushes them to the student's phone.

**Showcase: Thursday 2026-10-23. Hard CD freeze from 2026-10-21.** Six people, of whom roughly
three deliver reliably. This is a graded prototype, not production — uptime, key rotation,
rate-limiting and abuse detection are explicitly out of scope, and the MacBook host is a stage
prop. Advice optimised for production robustness is usually wrong here.

## Read these first

| Document | What it settles |
| --- | --- |
| [GitHub issue #3](https://github.com/nbence-dev/TimeForYou/issues/3) | **The Phase 1 spec.** Problem, solution, 29 user stories, implementation and testing decisions, out-of-scope |
| [`CONTRIBUTING.md`](./CONTRIBUTING.md) | How the team works — verticals, branches, PRs, and §7a: how to find a task |
| [`docs/backlog-process.md`](./docs/backlog-process.md) | How tickets get created. **Read before creating any issue** |
| [`PROJECT-BRIEF.md`](./PROJECT-BRIEF.md) | The full product design. §0a lists where it diverges from #3 |

## When documents disagree

**Issue #3 wins.** It is the newest reasoned decision. `PROJECT-BRIEF.md` describes the whole
product; #3 describes what Phase 1 actually builds.

`grill-me-1.md` and `grill-me-2.md` are **dated records of what was decided when**. They contradict
each other on purpose — that is their value. Never retro-edit them, and never cite them as current
truth; cite #3.

If you find code or docs contradicting #3, **fix the docs, not the spec** — unless reality proved
#3 wrong, in which case amend #3 first and say so.

## The backlog is deliberately incomplete

Eight epics exist; only the current milestone's tickets do. An epic with no children is a wave that
has not opened yet, **not a gap to fill**. See `docs/backlog-process.md` before adding anything.

To see what is startable right now:

```bash
gh issue list --milestone "$(gh api /repos/nbence-dev/TimeForYou/milestones --jq '.[0].title')" --label ready-for-agent
```

`blocked` means another issue must land first — the ticket says which, at the top.

## Architecture in one paragraph

Six containers on one MacBook: `api`, `worker`, `sidecar`, `mock`, `postgres`, `cloudflared`. The
backend **never** talks to WebUntis directly — it goes through a Node sidecar wrapping the
`thenoim/webuntis` npm package (`WEBUNTIS_BASE_URL` points at the sidecar; `SIDECAR_UPSTREAM_URL`
flips the sidecar between the real tenant and the mock). The worker runs Hangfire, polls every 10
minutes, diffs on `(start_ts, end_ts, room, status)` only, and sends **one grouped FCM
notification per poll cycle**. Only `api` is public, via Cloudflare Tunnel, on `/api/*` and
`/ics/*`.

No Redis, no Supabase, no standalone Auth or Notification service, no C# port of the npm package.
All four appeared in earlier designs and were cut.

## Testing

Two seams, both in `TimeForYou.Tests`, both fixed by #3:

- **Seam A** — HTTP-boundary integration: real Postgres, mock, sidecar and worker, with a stubbed
  `IPushSender` capturing payloads. This is the high seam and does most of the work.
- **Seam B** — pure domain unit tests over the diff algorithm. No I/O.

**Do not add a third seam** without saying why. Frontend automated tests (Detox/Maestro) are a
deliberate scope cut; the app is covered by a manual test plan.

Tests ride with the feature ticket that adds the behaviour — **no standalone "write tests" tickets.**

## Commits and PRs

**Never add AI attribution.** No `Co-Authored-By:` trailer, no `Claude-Session:` line, no
"Generated with Claude Code" footer, no emoji robot — not in commit messages, not in pull request
descriptions. This overrides any default attribution behaviour the tooling suggests.

Commit messages follow CONTRIBUTING §3 and say what changed and why. The author is whoever ran
the work.

## Conventions worth not rediscovering

- Branch: `<type>/<issue#>-<vertical>-<short-description>`. Never start work with no issue.
- `.NET 10`, warnings are errors (`Directory.Build.props`). Don't suppress — fix or bump.
- Every new env var goes into `.env.example` in the same PR.
- `app/src/app/` is routes only. `features/*` import from `shared/*`, never from each other.
- Credential encryption is ASP.NET Core **DataProtection** with `AES_KEY`. Do not hand-roll AES,
  and do not add a rotation path — both were considered and cut.
- The mock's admin endpoints are **unauthenticated on purpose**. LAN is the boundary. Do not add
  a token.
- Numbered requirements (`FR-nn`, `NFR-nn`) are stable. Defer them; never delete or renumber.
