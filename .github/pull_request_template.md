## What

<!-- One or two sentences. The reviewer reads this before the diff. -->

## Why

Closes #
Implements: FR- / NFR-

## How I tested

<!-- Actual steps you ran, not "tested locally". Screenshot or screen recording for any UI change. -->

- [ ] Runs from a clean clone via `docker compose up`
- [ ] Tested against the mock WebUntis service
- [ ] Tests added for logic that can break

## Checklist

- [ ] Rebased on `main` today
- [ ] One vertical (if it touches three, it should be three PRs — CONTRIBUTING §1)
- [ ] No secrets, `.env`, or build output in the diff
- [ ] New env vars added to `.env.example`
- [ ] Title follows the commit format — it becomes the commit message on `main`

<!--
Reviewers: CONTRIBUTING §6 — review within 24 hours, prefix optional comments with `nit:`.
1 approval to merge; 2 for shared code, schema, or auth/crypto.
-->
