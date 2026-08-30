# Repo setup — what was applied, and how to check it

**Repo:** `nbence-dev/TimeForYou` · **Owner of this document:** Nicholas
**Applied:** 2026-08-30 · Companion to [`CONTRIBUTING.md`](../CONTRIBUTING.md) §7 and [`dev-setup.md`](./dev-setup.md).

Every command below has already been run. This file exists so that when `main` behaves
unexpectedly — a push is rejected, a merge button is missing, a branch survives a merge —
you can diff _intended_ against _actual_ in about thirty seconds instead of clicking through
Settings.

Requires the GitHub CLI, authenticated as a repo admin: `gh auth status`.

---

## 1. Verify first, before changing anything

Run both. If the output matches, the repo is in its intended state and the problem is elsewhere.

```bash
gh api repos/nbence-dev/TimeForYou \
  --jq '{squash:.allow_squash_merge,merge:.allow_merge_commit,rebase:.allow_rebase_merge,autodelete:.delete_branch_on_merge,sq_title:.squash_merge_commit_title,sq_body:.squash_merge_commit_message}'
```

```json
{
  "autodelete": true,
  "merge": false,
  "rebase": false,
  "sq_body": "PR_BODY",
  "sq_title": "PR_TITLE",
  "squash": true
}
```

```bash
gh api repos/nbence-dev/TimeForYou/branches/main/protection \
  --jq '{approvals:.required_pull_request_reviews.required_approving_review_count,dismiss_stale:.required_pull_request_reviews.dismiss_stale_reviews,codeowners:.required_pull_request_reviews.require_code_owner_reviews,admins:.enforce_admins.enabled,conversations:.required_conversation_resolution.enabled,linear:.required_linear_history.enabled,force_push:.allow_force_pushes.enabled,deletions:.allow_deletions.enabled,checks:.required_status_checks}'
```

```json
{
  "admins": true,
  "approvals": 1,
  "checks": null,
  "codeowners": false,
  "conversations": true,
  "deletions": false,
  "dismiss_stale": true,
  "force_push": false,
  "linear": true
}
```

`404 Branch not protected` from the second command means protection was deleted. Re-run §3.

---

## 2. Merge hygiene

Squash is the only merge button; merged branches delete themselves; the squash commit inherits
the PR title and body rather than `Merge pull request #12 from ...`.

```bash
gh repo edit nbence-dev/TimeForYou \
  --enable-squash-merge \
  --enable-merge-commit=false \
  --enable-rebase-merge=false \
  --delete-branch-on-merge

gh api -X PATCH repos/nbence-dev/TimeForYou \
  -f squash_merge_commit_title=PR_TITLE \
  -f squash_merge_commit_message=PR_BODY
```

Safe to re-run at any time. `PR_TITLE` is why CONTRIBUTING §6 says the PR title must follow the
commit format — the PR title _becomes_ the commit on `main`.

---

## 3. Branch protection on `main`

```bash
gh api -X PUT repos/nbence-dev/TimeForYou/branches/main/protection --input - <<'EOF'
{
  "required_status_checks": null,
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "required_approving_review_count": 1,
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": false
  },
  "restrictions": null,
  "required_conversation_resolution": true,
  "required_linear_history": true,
  "allow_force_pushes": false,
  "allow_deletions": false
}
EOF
```

This is a `PUT` — it replaces the whole protection object. Any field left out is reset to its
default, so always send the full body, never a partial one.

| Field                              | Value       | Why                                                                                                                                                                 |
| ---------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `enforce_admins`                   | `true`      | CONTRIBUTING §7. Applies to Nicholas too — no exceptions to talk your way around at 23:00.                                                                          |
| `required_approving_review_count`  | `1`         | §6. The "2 approvals for shared code / schema / auth" rule is a human rule; GitHub cannot express it per-path.                                                      |
| `dismiss_stale_reviews`            | `true`      | An approval dies when more commits are pushed. Stops "approved at 40 lines, merged at 400".                                                                         |
| `require_code_owner_reviews`       | `false`     | **Flip to `true` once `.github/CODEOWNERS` exists.** Enabling it before that, with a `* @nbence-dev` line, makes Nicholas a required reviewer on all six verticals. |
| `required_status_checks`           | `null`      | There is no CI yet. A required check that does not exist blocks every PR forever. See §5.                                                                           |
| `required_conversation_resolution` | `true`      | §6. Every thread resolved before merge. `nit:` comments still need resolving, just not obeying.                                                                     |
| `required_linear_history`          | `true`      | Belt and braces with squash-only merges: no merge commits can reach `main`.                                                                                         |
| `allow_force_pushes`               | `false`     | `--force-with-lease` stays legal on your own branch, never on `main`.                                                                                               |
| Branches up to date                | **not set** | Deliberate. With six people it serialises every merge — each merge invalidates every other open PR.                                                                 |

---

## 4. Symptom → cause

| Symptom                                              | Cause                                                  | Fix                                                                                                                     |
| ---------------------------------------------------- | ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------- |
| `push declined ... protected branch` on `main`       | Working as designed, including for admins              | Branch and open a PR. CONTRIBUTING §9 cheatsheet.                                                                       |
| Only the repo owner can bypass, urgently             | Deliberate escape hatch, leaves a trace                | `gh pr merge --squash --admin`. Say it out loud in chat — CONTRIBUTING §8.                                              |
| PR says "Merging is blocked", no reason given        | Unresolved review conversation                         | Resolve every thread, including `nit:` ones.                                                                            |
| Approval vanished after a push                       | `dismiss_stale_reviews`                                | Re-request review. Intended behaviour.                                                                                  |
| Only "Squash and merge" is offered                   | §2                                                     | Intended.                                                                                                               |
| Merged branch still on the remote                    | Merged before §2 was applied, or merged via CLI push   | `git push origin --delete <branch>`, then `git remote prune origin` locally.                                            |
| Every PR blocked on a check that never runs          | A required check was added before the workflow existed | Re-run §3 with `"required_status_checks": null`, add the check back only once CI is green on a real PR.                 |
| Secrets or `bin/`, `obj/`, `node_modules/` in a diff | `.gitignore` gap                                       | Fix `.gitignore` — CONTRIBUTING §3, never fix it by hand per-commit.                                                    |
| Whole file shows as changed, no real edit            | Line endings or editor formatting                      | `.gitattributes` covers git; `.editorconfig` covers the editor. If `.editorconfig` is still missing, that is the cause. |

---

## 5. Not applied yet — with the reason

| Item                                        | Blocked on                                                                                                                               |
| ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `.github/CODEOWNERS`                        | The six verticals need real GitHub handles. Placeholder handles fail silently — GitHub requests no reviewer at all rather than erroring. |
| `require_code_owner_reviews: true`          | The file above existing first.                                                                                                           |
| `required_status_checks`                    | CI existing. Add `app`, `server`, `mock` per CONTRIBUTING §7, one at a time, each only after it has passed on a real PR.                 |
| Vertical labels, issue templates, milestone | Vertical ownership.                                                                                                                      |
| Two of six collaborators                    | Handles unknown. Currently on the repo: `nbence-dev`, `MardoSaayman`, `DogCE1`, `StephanEsterhuizen`.                                    |

---

## 6. Considered, not adopted

**`git config --global rerere.enabled true`** — reuse recorded resolution. Git records how you
resolve a conflict and replays it automatically the next time the identical conflict appears.
With daily rebases that is otherwise the same conflict every morning until the branch merges.

**Not adopted yet — decide after the first real conflict.** It is per-machine, not per-repo, so
it cannot be enforced from here anyway; anyone who wants it can turn it on for themselves. The
reservation worth taking seriously is that it replays silently: a resolution you got wrong comes
back wrong, and `git rerere forget <path>` is the undo. Revisit this once the team has felt a
repeated rebase conflict, not before.

---

## 7. Rollback

Only if protection is actively wrong and blocking the team. Prefer re-running §3 with a
corrected body over deleting.

```bash
gh api -X DELETE repos/nbence-dev/TimeForYou/branches/main/protection
```

Announce it in chat before and after — an unprotected `main` that nobody knows is unprotected
is how a force push takes the branch out.
