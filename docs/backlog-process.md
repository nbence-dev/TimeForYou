# Opening the next wave

How the backlog gets built, one week at a time. Run by whoever owns `main` (Nicholas), at the end
of each week, and it takes about an hour.

If you only read one thing: **the invariants in §4 are the part that must not drift.** Everything
else is judgement.

---

## Why the backlog is deliberately incomplete

[Issue #3](https://github.com/nbence-dev/TimeForYou/issues/3) is the Phase 1 spec. It is broken
into **eight epics** — one per vertical (CONTRIBUTING §1) plus `release` — and each epic is broken
into the tickets you actually branch from.

**Only the current milestone's tickets exist.** Later waves are epics with no children, on purpose.

The reason is written in the project's own history: the plan reversed materially twice between
`grill-me-1.md` and `grill-me-2.md` — the C# port of `thenoim/webuntis` became a Node sidecar, and
four backend services collapsed to two plus libraries — both times because an estimate met
reality. Tickets written for week 4 on day 2 would have described work nobody ends up doing.

Epics are stable because the architecture is settled. Children are not. So: **epics all at once,
children one wave at a time, about a week ahead.**

An epic with no children is not a gap to fill. It is a wave that has not opened yet.

---

## 1. Retro before decomposing

Do this **before** writing a single ticket. It is the whole point of waving.

Ask: **what did last week teach that #3 got wrong?**

Concretely — did the sidecar's real response shape match what the schema assumed? Did the
two-variable `WEBUNTIS_BASE_URL` / `SIDECAR_UPSTREAM_URL` split survive contact with compose? Did
anything take three times as long as budgeted?

If something diverged, **amend #3 first.** Docs follow the spec, but the spec follows reality. A
spec nobody updates becomes another `PROJECT-BRIEF.md` — authoritative-looking and quietly wrong.
That cost a whole reconciliation PR (#20) to fix once already.

---

## 2. Close out the finished milestone

```bash
gh issue list --milestone "<last milestone>" --state open
```

Everything still open either **rolls into the new milestone** or is **cut out loud** — CONTRIBUTING
§8: any rule can be suspended for a deadline, out loud, not quietly. Silently dragging a ticket
forward three weeks in a row is how a five-week project discovers in week 5 that it is behind.

---

## 3. Create the new milestone

```bash
gh api --method POST /repos/nbence-dev/TimeForYou/milestones \
  -f title="Week N — <theme>" \
  -f due_on="<YYYY-MM-DD>T23:59:59Z" \
  -f description="<what this week unblocks>. Spec #3."
```

The milestone is what makes `gh issue list --milestone "…" --label ready-for-agent` — the command
CONTRIBUTING §7a tells teammates to run — mean "startable right now".

---

## 4. The invariants

**This section is the contract.** A wave opened without these is a wave that reads differently
from every wave before it.

### Ticket body template

```markdown
## Context
Part of #<epic>. Spec: #3 § <section>.

## Scope
<what this ticket changes, in behaviour terms>

## Acceptance criteria
- [ ] <externally observable outcome>

## Tests
Seam A: <scenarios> | Seam B: <cases> | None (config/docs only)

## Out of scope
<explicit non-goals, to keep the PR under ~400 lines>

## Branch
<type>/<this issue#>-<vertical>-<short-description>
```

### Rules

1. **Create tickets in a single ordered pass**, so issue numbers are predictable, then write the
   branch name into the body using the number the ticket is about to get. Verify after creating;
   fix with `gh issue edit` if the prediction slipped.
2. **Link every ticket as a sub-issue of its epic.** The epic already links to #3, so the root
   spec gets a live burndown for free.
   ```bash
   id=$(gh api /repos/nbence-dev/TimeForYou/issues/<child> --jq .id)
   gh api --method POST /repos/nbence-dev/TimeForYou/issues/<epic>/sub_issues -F sub_issue_id=$id
   ```
   Note it takes the child's numeric **`id`**, not its issue number.
3. **Label:** vertical (`api`, `worker`, `platform`, `onboarding`, `timetable`, `notify`,
   `webuntis`, `release`) + milestone + `ready-for-agent`.
4. **Anything not startable gets `blocked`, not `ready-for-agent`**, plus a banner at the top of
   the body naming what it waits for and pointing at the ticket that unblocks it. This is not
   optional: the first ticket ever claimed on this project was the one blocked by two others,
   because nothing distinguished it.
5. **No standalone testing tickets.** The seams are fixed by #3 — Seam A (HTTP-boundary
   integration in `TimeForYou.Tests`) and Seam B (pure domain tests over the diff algorithm).
   Each feature ticket carries its own scenarios in its acceptance criteria. The only exception is
   test *infrastructure*, like the Seam A harness.
6. **Numbered requirements are deferred, never deleted.** `PROJECT-BRIEF.md` §0 rule 1 says
   requirement IDs are stable. If Phase 1 will not implement `FR-nn`, mark it deferred in the
   brief; do not remove it.
7. **Keep a ticket to one concern, under ~400 lines, mergeable in two days** (CONTRIBUTING §2).
   If it cannot be, split it: schema → endpoint → screen.
8. **Assign before anyone branches.** CONTRIBUTING §1: assign owners before the first line of code.

### Sanity check

```bash
gh issue list --milestone "Week N — <theme>" --label ready-for-agent   # what a teammate sees
gh api /repos/nbence-dev/TimeForYou/issues/3/sub_issues --jq 'length'  # still 8 epics
gh api /repos/nbence-dev/TimeForYou/issues/<epic> --jq .sub_issues_summary
```

Then open one ticket and ask: **could someone unfamiliar start this without asking a question?**
If not, it is not `ready-for-agent`.

---

## 5. Announce

Post the new milestone in team chat with: what is in it, what got cut or deferred and why, and who
owns what. CONTRIBUTING §8 — out loud, not quietly.

---

## The remaining waves

From #3's timeline. Themes, not tickets — the tickets get written when the wave opens.

| Wave | Dates | Theme |
| --- | --- | --- |
| 1 | Sep 21–27 | OpenAPI contract · schema · compose up healthy · sidecar · mock · CI · Firebase |
| 2 | Sep 28–Oct 4 | **Seam A harness first.** API endpoints · `IWebUntisClient` facade · Hangfire poll job · diff engine · mock admin + `/nudge` · notes CRUD · ICS feed |
| 3 | Oct 5–11 | Expo screens · FCM Web + Android registration · first real end-to-end notification |
| 4 | Oct 12–18 | Cloudflare Tunnel · cross-device sync · ICS in Apple/Google Calendar · demo script |
| 5 | Oct 19–23 | **CD freeze Oct 21** · bug bash · rehearsals · MacBook prep · **showcase Oct 23** |

**Cut order if the team slips** (`grill-me-2.md` §6): demo-controller UI → Hangfire dashboard
polish → calendar-notes-in-description.

Wave 2 opens with the **Seam A harness** because every scenario ticket after it depends on that
harness existing. Build it before the tickets that need it, not alongside them.
