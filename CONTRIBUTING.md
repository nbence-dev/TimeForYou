# TimeForYou — Team Working Agreement

**6 devs · one repo · `nbence-dev/TimeForYou`** · Last updated 2026-08-30
What we're building: [`PROJECT-BRIEF.md`](./PROJECT-BRIEF.md). How we work on it: this file.

---

## The five rules

1. **One issue, one branch, one person.** Your vertical is what you deliver, not where you may type.
2. **Branch from `main`, merge back within 2 days.** Old branches are what cause conflicts.
3. **`git pull --rebase origin main` every morning.**
4. **Never push to `main`.** Everything goes through a PR.
5. **Never reformat a file you don't own.** A format-only diff breaks every open branch.

---

## 1. The verticals

One word per vertical, used for branch names, commit scopes and review routing.

| #   | Vertical                 | Tag          | Owner | Delivers                                              | Mostly works in                |
| --- | ------------------------ | ------------ | ----- | ----------------------------------------------------- | ------------------------------ |
| 1   | Onboarding & identity    | `onboarding` | _TBD_ | Screens 1–4, QR scan, device link                     | `app/`                         |
| 2   | Timetable UI & perf      | `timetable`  | _TBD_ | Screens 5, 6, 8, offline cache                        | `app/`                         |
| 3   | Notifications & settings | `notify`     | _TBD_ | Screens 7, 9, push registration, both transports      | `app/`                         |
| 4   | WebUntis client (C#)     | `webuntis`   | _TBD_ | Auth + timetable retrieval                            | `server/TimeForYou.WebUntis/`  |
| 5   | Worker & diff engine     | `worker`     | _TBD_ | Polling, change detection, dispatch                   | `server/TimeForYou.Worker/`    |
| 6   | Mock, Docker, data layer | `platform`   | _TBD_ | Mock WebUntis, compose, schema + migrations           | `mock/`, `server/TimeForYou.Data/` |
| 7   | API & iCal feed          | `api`        | _TBD_ | REST endpoints, iCal subscription feed                | `server/TimeForYou.Api/`       |

**Assign owners before the first line of code.** Seven areas, six people — the API is the seventh, and whoever takes it should already be in C# (V5 or V6 are the natural pairs).

**Nobody is fenced out of a directory.** The last column says where each person spends most of
their time, so reviews route to someone who knows the code — not who is allowed to edit it. Source
files are organised by what they do (see the tree in [`README.md`](./README.md)), never by who owns
them. If your PR needs a change in someone else's usual area, make it and put them on the review.

What that costs you is discipline elsewhere: keep a PR to one concern, keep it under ~400 lines,
and merge within two days. Two people in the same file for a week is what hurts, not two people in
the same folder for an afternoon.

`app/src/shared/` and `server/TimeForYou.Domain/` are the shared floor: announce changes in chat,
keep them additive, ship them in their own small PR the same day.

---

## 2. Branches

```text
<type>/<issue#>-<vertical>-<short-description>     all lowercase, hyphens

feat/23-onboarding-qr-scanner
fix/41-timetable-offline-crash
chore/platform-compose-postgres
spike/notify-ios-web-push
```

Types: `feat` `fix` `chore` `docs` `refactor` `test` `spike`.
Bad: `nicholas-branch` (who, not what) · `new-feature` (says nothing) · `feature/everything`.

**One branch, one person, max 2 days.** If it needs longer, split it: schema → endpoint → screen.

Sync daily:

```bash
git fetch origin && git rebase origin/main
git push --force-with-lease     # only after a rebase, never on main
```

---

## 3. Commits

```text
feat(onboarding): decrypt WebUntis secret on device link
fix(worker): stop re-notifying changes after a restart

Refs #57
```

Imperative mood, ≤72 chars, one logical change. Never commit `.env`, secrets, `node_modules/`,
`bin/`, `obj/`, `.expo/` — fix `.gitignore` instead of being careful by hand.

---

## 4. Avoiding conflicts

Conflicts come from two people editing the same lines with a long gap between them. So:
**ownership** (§1) · **PRs under ~400 lines** · **branches under 2 days** · **daily rebase**.

Files that attract collisions, and the rule for each:

| File                                 | Rule                                                                                     |
| ------------------------------------ | ---------------------------------------------------------------------------------------- |
| `package-lock.json`                  | Never hand-merge. `git checkout --theirs` it, run `npm install`, commit.                 |
| `package.json`                       | Additive only, dependencies sorted alphabetically.                                       |
| `TimeForYou.slnx`, `*.csproj`        | Only V6 adds projects — a broken solution file blocks all six of us. Framework and warning settings live in `server/Directory.Build.props`; change them there, once. |
| `TimeForYou.Data/Migrations/`        | Append-only, **one in flight at a time**, announce first. Never edit a merged migration. |
| `Program.cs` / routes                | Each vertical registers itself in one line (`services.AddWorkerModule()`).               |
| `docker-compose.yml`, `.env.example` | V6 owns. Request changes, don't edit in passing.                                         |
| `docs/*.md`                          | One owner per document. Markdown conflicts are unmergeable by hand.                      |

Formatting is enforced by tooling (Prettier/ESLint, `.editorconfig` + `dotnet format`, Ruff).
A formatting sweep goes in its own PR that changes nothing else.

Commit `.gitattributes` **before the second person clones** — mixed Mac/Windows line endings
otherwise show up as "every line changed":

```gitattributes
* text=auto eol=lf
*.png binary
package-lock.json -diff
```

---

## 5. Resolving a conflict

Nothing is broken and nothing is lost.

```bash
git rebase origin/main
# CONFLICT in app/src/shared/date.ts
git status                  # what's conflicted
```

1. Open the file. Between `<<<<<<< HEAD` (main's version) and `>>>>>>>` (yours) — the answer
   is usually **keep both**, not pick one.
2. Delete all three marker lines.
3. `git add <file>` → `git rebase --continue` → **run the app and tests** (a file with no
   markers still compiles fine while doing the wrong thing).
4. `git push --force-with-lease`.

Escape hatch: `git rebase --abort` puts everything back. Use it freely.
**If the conflict is in code you don't understand, call its owner.** Resolving a conflict by
deleting the other side is the most expensive mistake available to us.

Optional, per machine: `git config --global rerere.enabled true` — Git then replays your
conflict resolutions automatically. Not a team standard yet; see `docs/repo-setup.md` §6.

---

## 6. Pull requests

- Open a **draft PR when you push the branch**, not when you finish — it tells everyone what
  you're touching.
- Title = commit format. Body: what, why (`Closes #23`, `Implements FR-12`), how you tested,
  screenshot for UI.
- **1 approval** to merge. **2** for shared code, schema, or auth/crypto.
- **Review within 24 hours.** Prefix optional comments with `nit:` — a nit never blocks.
- **Squash and merge**, CI green, delete the branch. Merge in the morning, not at 23:00.

---

## 7. Repo setup (once, by the owner)

Protect `main`: require a PR + 1 approval · require status checks · require conversation
resolution · block force pushes · auto-delete merged branches · **apply to admins too**.
Don't enable "require branches up to date" — with six people it serialises every merge.

CI on every PR: `app` (lint, `tsc`, tests) · `server` (`dotnet build --warnaserror`, tests) ·
`mock` (ruff, pytest).

One GitHub Issue per unit of work, labelled by vertical, number in the branch name.
**Don't start work with no issue** — two people silently building the same thing is the one
conflict Git can't help with.

---

## 8. Rhythm

- **Daily:** 10-min standup (done/doing/blocked), rebase, merge whatever's ready. `main` moves every day.
- **Weekly:** everyone rebases, `docker compose up` from a clean clone, walk the demo path.
- **24h before a deadline:** code freeze — bug fixes only, 2 approvals each.

**Done** = merged via approved PR · CI green · tests for logic that can break · runs from a
clean clone via `docker compose up` · new env vars in `.env.example` · demoable by someone else.

Nicholas owns `main` and arbitrates conflicts; vertical owners are first reviewers; V6 owner
gatekeeps the schema. Any rule here can be suspended for a deadline — out loud, not quietly.

---

## 9. Cheatsheet

```bash
# start
git checkout main && git pull --rebase origin main
git checkout -b feat/23-onboarding-qr-scanner

# save
git add -p && git commit -m "feat(onboarding): parse QR payload"

# stay current (daily) + publish
git fetch origin && git rebase origin/main
git push -u origin feat/23-onboarding-qr-scanner

# after merge
git checkout main && git pull --rebase origin main
git branch -d feat/23-onboarding-qr-scanner && git remote prune origin

# panic
git stash push -m "wip"      # switch branches now
git rebase --abort           # undo the rebase
git diff main...HEAD --stat  # what did I actually change?
```

### `.github/CODEOWNERS` — highest-leverage file here; GitHub auto-requests the right reviewer

```text
# Routes a review to someone who knows the code. It grants no exclusive right
# to edit anything — see §1.

*                              @nbence-dev

/app/                          @handle-1 @handle-2 @handle-3
/server/TimeForYou.WebUntis/   @handle-4
/server/TimeForYou.Worker/     @handle-5
/server/TimeForYou.Api/        @handle-7
/server/TimeForYou.Data/       @handle-6
/mock/                         @handle-6
/docker-compose.yml            @handle-6
/.env.example                  @handle-6

# Shared floor — two approvals (§6)
/app/src/shared/               @nbence-dev @handle-2
/server/TimeForYou.Domain/     @nbence-dev @handle-5
/server/Directory.Build.props  @nbence-dev @handle-6
```

### `.github/pull_request_template.md`

```markdown
## What

## Why

Closes # Implements: FR-

## How I tested

- [ ] Runs via `docker compose up` - [ ] Tested against the mock - [ ] Tests added

## Checklist

- [ ] Rebased on `main` - [ ] No secrets or build output - [ ] One vertical - [ ] Env vars in `.env.example`
```

---

Rules being routinely ignored are wrong rules — open a PR against this file.
