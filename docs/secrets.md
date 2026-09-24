# Secrets — where things go, in one page

**One rule:** a secret lives in a file called `.env` that git ignores, and code reads it from the
environment. It never appears in source, and never in the repo.

---

## The flow

```text
.env.example  ──copy──▶  .env  ──read by──▶  docker compose  ──passed to──▶  your code
 committed              your real            reads .env in the             as environment
 fake values            values,              project root                  variables
                        git-ignored          automatically
```

Three steps, once per person:

```bash
cp .env.example .env       # 1. make your own copy
# 2. open .env, replace every CHANGEME with a real value
openssl rand -base64 32    # 3. generate the keys it asks for
```

`.env` is already in `.gitignore`. Never remove it from there. Never rename your `.env` to
something clever to "just commit it this once".

---

## Which file does a value belong in?

| If it is… | It goes in | Committed? |
| --- | --- | --- |
| A password, key, or token | `.env` | **No** |
| The *name* of that setting, with a fake value | `.env.example` | Yes |
| A URL, port, interval, feature flag | `.env` (and `.env.example` with the real default) | Template only |
| A credential file (Firebase JSON, `.pem`, keystore) | anywhere **outside** the repo; reference it by path | **No** |
| Your own personal WebUntis test login | `dotnet user-secrets` (see below) | **No** |

If you add a new setting, **add it to `.env.example` in the same PR.** Otherwise the next person
clones the repo and gets a runtime error with no clue what's missing. That's on the PR checklist.

---

## Reading a secret, per language

Same environment variables, three syntaxes.

**C# — API and Worker.** .NET reads environment variables into `IConfiguration` automatically.
A `__` (double underscore) in the variable name becomes a `:` nesting separator.

```csharp
// AES_KEY=... in .env
var key = builder.Configuration["AES_KEY"]
          ?? throw new InvalidOperationException("AES_KEY is not set");

// ConnectionStrings__TimeForYou=... in .env
var conn = builder.Configuration.GetConnectionString("TimeForYou");
```

Throw on missing, don't default. A silent fallback to an empty key is worse than a crash.

`AES_KEY` feeds the **ASP.NET Core DataProtection API**, which does the actual encryption of the
WebUntis SSO secret. Do not hand-roll AES — DataProtection already handles nonces, authentication
tags and key derivation, and getting those wrong is the usual way this goes badly.

**Python — the mock service.**

The mock holds **no secrets at all**. Its admin endpoints are unauthenticated on purpose: it is
never exposed through the tunnel, so the LAN is the security boundary. If you find yourself
reaching for `os.environ` to read a token in `mock/`, stop — that is the old design.

Plain configuration is still fine — the mock currently has none beyond what `uvicorn` takes on
the command line, but if that changes, read it the same way everything else does:

```python
import os
port = os.environ["MOCK_PORT"]   # KeyError if missing, which is what you want
```

**Expo app — it cannot hold secrets at all.**

```ts
// Fine — this is a public URL:
const api = process.env.EXPO_PUBLIC_API_URL;
```

Everything prefixed `EXPO_PUBLIC_` is **compiled into the JavaScript bundle** and readable by
anyone who downloads the app and unzips it. There is no secure storage in a shipped app bundle.

So: `AES_KEY` and the Firebase *server* credentials exist only on the API and the Worker. The app
talks to our API; our API talks to everything else. That is already how the architecture is drawn
(brief §12.1) — the rule is just "don't shortcut it".

The `EXPO_PUBLIC_FIREBASE_*` values are the exception that proves the rule: they ship in the
bundle, they are public by design, and they are not credentials. They let the PWA ask Firebase for
an FCM registration token. Nothing signs or authorises with them.

---

## `dotnet user-secrets` — for your machine only

Optional, and only useful for C#. It stores values in your home folder
(`~/.microsoft/usersecrets/…`), **completely outside the repo**, so they cannot be committed by
accident. Good for your personal WebUntis test login while developing the client.

```bash
cd server/TimeForYou.WebUntis
dotnet user-secrets init                        # adds a <UserSecretsId> GUID to the .csproj
dotnet user-secrets set "WebUntis:Password" "…"
dotnet user-secrets list
```

The `<UserSecretsId>` GUID that appears in the `.csproj` **is safe to commit** — it is a folder
name, not a secret. It works in Development only, and does nothing for Python, the app, or Docker.

---

## Protection

One layer. That's the lot.

**`.gitignore`** — it covers `.env`, `*.pem`, `*.p8`, `*.p12`, `*.keystore`,
`google-services.json`, `GoogleService-Info.plist`, `secrets.json`. Nothing else in this repo
stops a secret from being committed.

**What is deliberately *not* here:** there is no pre-commit hook — a hook lives in `.git/hooks`,
which is not shared by a clone, so it only works if every person runs a setup command, and one
person forgetting is exactly the case it was meant to cover. And GitHub's own secret scanning and
push protection are not available on a private repo on the free plan (checked — the API returns
*"Secret scanning is not available for this repository"*), so there is no server-side net either.
`.gitignore` and the habit are all there is, which is exactly why the habit matters more than the
tooling. **Read your own diff before you commit** — `git diff --cached --name-only`.

---

## If a secret does get committed

**Deleting it in the next commit does nothing.** It stays in the history, in every clone all six
of us have, and in GitHub's API. This repo is handed to a marker with its full history.

The only real fix is to **rotate the credential** — change the actual password or key so the
leaked one is worthless:

1. Say so in the team chat immediately. This is not embarrassing; hiding it is the problem.
2. Rotate it at the source — new Postgres password, new `openssl rand -base64 32` key, new
   Firebase key in the console.
3. Update `.env` on every machine and on the demo host.
4. For `AES_KEY` specifically there is **no rotation path**, by decision. Nothing re-encrypts
   stored records, because a six-user prototype does not justify building that. Changing the key
   makes every stored WebUntis secret undecryptable, so the recovery is to clear them and have
   the six users re-scan their QR codes. That takes about five minutes and is the honest trade —
   a rotation procedure nobody has tested is worth less than a re-onboarding that works.
5. Only then worry about scrubbing history, if it's worth it at all.

## Never

- Paste a secret into a GitHub issue, a PR comment, or a screenshot.
- Screenshot your **WebUntis QR code**. It is a live credential, and onboarding is QR-only, so
  the temptation is constant. Use a mock-generated QR for anything anyone else will see.
- Commit a `.env` "temporarily so the others can run it". Send the values another way.
- Put a real value in `.env.example`.
