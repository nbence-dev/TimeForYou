# Dev setup — a machine that can run every part of TimeForYou

**Owner of this document:** Nicholas · Last updated 2026-08-30
Companion to [`CONTRIBUTING.md`](../CONTRIBUTING.md) (how we work) and
[`repo-setup.md`](./repo-setup.md) (how GitHub is configured).

TimeForYou is four stacks in one repo — TypeScript/Expo, C#/.NET, Python/FastAPI, and Docker.
This sets up **all four**, so you can run the whole system and review anyone's PR.

**Budget ~90 minutes, most of it downloads.** You do not need all of it to start — §9 lists the
minimum per vertical. Do the full install anyway if you can: the value of six people who can
each `docker compose up` the whole stack is that a broken demo is never one person's problem.

Versions below are what the team is standardised on. Newer minor versions are fine; a different
major version is how "works on my machine" starts.

| Tool             | Version        | Needed for                               |
| ---------------- | -------------- | ---------------------------------------- |
| Git + GitHub CLI | any recent     | everything                               |
| Node.js          | **22 LTS**     | `app/` — Expo, React Native, TypeScript  |
| .NET SDK         | **10.0 (LTS)** | `server/` — API, Worker, WebUntis client |
| Python           | **3.12**       | `mock/` — FastAPI mock WebUntis          |
| Docker Desktop   | current        | Postgres + the whole compose stack       |
| JDK (Azul Zulu)  | **17**         | only for _local_ Android builds — see §4 |

---

## 1. macOS

Install [Homebrew](https://brew.sh) first if you don't have it, then:

```bash
# Core
brew install git gh
brew install --cask visual-studio-code

# Node 22 LTS via nvm — never install Node from brew directly,
# you will need to switch versions eventually
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.1/install.sh | bash
# restart the terminal, then:
nvm install 22 && nvm alias default 22

# .NET 10 SDK
brew install --cask dotnet-sdk

# Python 3.12
brew install pyenv
pyenv install 3.12 && pyenv global 3.12

# Docker Desktop
brew install --cask docker-desktop
```

If a cask name has moved (Homebrew renames these occasionally), `brew search <name>` rather
than guessing. For .NET, the [Microsoft installer](https://dotnet.microsoft.com/download/dotnet/10.0)
is the fallback that always works.

**Apple Silicon:** everything above is native ARM64. Docker images we build must be
multi-arch or `linux/amd64` with emulation — `docker compose` handles this, but if a container
fails to start with an `exec format error`, that is the cause.

## 2. Windows

Use [winget](https://learn.microsoft.com/windows/package-manager/) from PowerShell:

```powershell
winget install Git.Git
winget install GitHub.cli
winget install Microsoft.VisualStudioCode
winget install CoreyButler.NVMforWindows
winget install Microsoft.DotNet.SDK.10
winget install Python.Python.3.12
winget install Docker.DockerDesktop
```

Then restart the terminal and:

```powershell
nvm install 22
nvm use 22
```

**Docker Desktop needs WSL 2.** If the installer complains, run `wsl --install` from an
admin PowerShell and reboot. Expo also officially supports WSL 2 — but do **not** clone the
repo inside WSL and edit it from Windows (or vice versa). Pick one filesystem and stay there;
crossing the boundary is slow and produces line-ending chaos that `.gitattributes` cannot save
you from.

## 3. Everyone: git identity and the repo

```bash
git config --global user.name "Your Name"
git config --global user.email "your@email"
gh auth login                       # HTTPS, authenticate in browser

git clone https://github.com/nbence-dev/TimeForYou.git
cd TimeForYou
code .                              # VS Code prompts to install the team extensions
```

Say yes to that prompt — it installs everything in `.vscode/extensions.json`, including the
EditorConfig extension that makes `.editorconfig` actually take effect in VS Code. Rider and
Visual Studio read `.editorconfig` natively and need nothing.

## 4. Android — pick one path

**Path A — physical Android phone + EAS Build (recommended).** No Android Studio, no JDK, no
emulator, ~8 GB less disk. Expo builds the development client in the cloud, you install the APK
on your phone by scanning a link. This is the path for five of the six of us.

```bash
npm install -g eas-cli
eas login                          # free Expo account
```

**Path B — local builds.** Only if you want an emulator or you're debugging native code:

```bash
# macOS
brew install watchman
brew install --cask zulu@17
brew install --cask android-studio
```

```powershell
# Windows
winget install Microsoft.OpenJDK.17
winget install Google.AndroidStudio
```

Then open Android Studio once and let it install the SDK and an emulator image. That is the
**only** time you need to open it — everything after this is command line.

Add all four exports to `~/.zshrc` (macOS), then `source ~/.zshrc`:

```bash
export JAVA_HOME=/Library/Java/JavaVirtualMachines/zulu-17.jdk/Contents/Home
export ANDROID_HOME="$HOME/Library/Android/sdk"
export PATH="$PATH:$ANDROID_HOME/emulator"
export PATH="$PATH:$ANDROID_HOME/platform-tools"
```

On Windows, set `ANDROID_HOME` as a user variable and add `%ANDROID_HOME%\platform-tools` and
`%ANDROID_HOME%\emulator` to `Path`.

Check it took — if `adb` is not found, the rest of this section will not work:

```bash
adb version
emulator -list-avds        # your AVD name, e.g. Medium_Phone_API_36.1
```

**Running it.** You do not need to start the emulator yourself. From `app/`:

```bash
npm run android      # builds and installs the dev client (push works)
npm run android:go   # Expo Go, faster loop, no push - see the note below
```

Either boots an emulator if none is attached. Starting one by hand is only useful if you want it
up before the bundler, or want a specific AVD when you have several:

```bash
emulator -avd Medium_Phone_API_36.1 &
```

**If the app loads but says it cannot connect to the Expo CLI**, this is why. Expo defaults to
LAN mode and hands the emulator your Mac's LAN address (`192.168.x.x`). macOS ships with its
firewall on, which drops that inbound connection — so the app retries forever against an address
it cannot reach. `adb reverse tcp:8081 tcp:8081` is already in place but goes unused, because it
only serves requests to `localhost`.

`--localhost` is the fix, and it is why `android:go` carries that flag: the app dials
`localhost:8081`, `adb reverse` forwards it, and the firewall never sees it. Confirm what the app
is actually dialling with:

```bash
adb logcat -d | grep ReconnectingWebSocket | tail -3
```

**Do not use `--localhost` for a physical phone on Path A.** Your phone is a different machine, so
it needs the LAN address — allow incoming connections for `node` in System Settings → Network →
Firewall → Options, or use `npx expo start --tunnel`.

**JDK 17 specifically.** Newer JDKs break the Gradle version React Native pins. If Gradle can't
find the Homebrew JDK, `echo "java.home=$JAVA_HOME" >> ~/.gradle/gradle.properties`.

**A note on Expo Go.** You will see it recommended everywhere. It cannot be our main loop:
Expo Go dropped remote push notifications in SDK 53 (brief §11.2), and push is the product.
Expo Go is fine for pure UI work on screens with no push; anything touching notifications needs
the development build from Path A or B.

## 5. iPhone — nothing to install

The iOS target is a **PWA**, not a native app (brief §11.2). No Xcode, no Apple Developer
Program, no Mac required. You need Safari on an iPhone and the HTTPS URL from the Cloudflare
Tunnel — Web Push to a Home Screen web app needs no Apple account, which is the entire reason
this route was chosen.

## 6. Verify

Paste this whole block. Every line should print a version, and every version should match §0.

```bash
git --version
gh --version
node -v          # v22.x
npm -v
dotnet --version # 10.0.x
python3 --version # 3.12.x
docker --version
docker compose version
```

Then the real test — that Docker works, not just that it's installed:

```bash
docker run --rm hello-world
```

Once `docker-compose.yml` exists (vertical 6), the actual acceptance test for this document is:

```bash
docker compose up
```

from a clean clone, with nothing else installed. That is the same command the marker runs.

## 7. Per-language sanity checks

Useful when one stack misbehaves and you need to know whether it's your machine or the code.

```bash
# .NET — should print the SDKs you have; 10.0.x must be listed
dotnet --list-sdks

# Python — check the interpreter is the one you think it is
python3 -c "import sys; print(sys.executable, sys.version)"

# Node — nvm is per-shell; a new terminal can silently be on a different version
which node && node -v
```

## 8. Editors

Team default is **VS Code** — it's the only editor decent at all four stacks at once, it's free,
it's identical on Mac and Windows, and `.vscode/extensions.json` means all six of us get the
same tooling from one prompt.

**Rider** is a better .NET experience and worth it if you own vertical 4, 5 or 7 (WebUntis
client, Worker, API). Free for non-commercial use, and the JetBrains student pack covers us
regardless. It reads `.editorconfig` natively. Use it for `server/` and VS Code for the rest if
you like — the repo does not care which editor produced a diff, which is the point of
`.editorconfig`.

**Rider 2025.1 or newer**, though. The solution file is `TimeForYou.slnx`, the XML format that is
now the .NET 10 default — it is a plain list of projects rather than the old GUID soup, which is
what stops six people conflicting on it every time someone adds a project. Older Rider and
Visual Studio below 17.14 cannot open it. If yours refuses, update the IDE; do not convert the
file back.

**Visual Studio** is not a team option: VS for Mac was retired in 2024, so it cannot be a
standard when half the team is on macOS, and it has no React Native story. Fine as a personal
choice on Windows for the C# projects only.

## 9. Minimum per vertical

If 90 minutes isn't available today, install the row you own and come back for the rest.

| Vertical                                    | Minimum                               |
| ------------------------------------------- | ------------------------------------- |
| 1 Onboarding, 2 Timetable, 3 Notifications  | Node 22, Expo, Android path A, Docker |
| 4 WebUntis client, 5 Worker                 | .NET 10, Docker                       |
| 6 Mock / Docker / data                      | Python 3.12, Docker, .NET 10          |
| Anyone reviewing PRs outside their vertical | all of it                             |

Docker is on every row. It is the one non-negotiable install.

## 10. Not needed — don't install it

Recorded because these come up in every tutorial and each one costs an hour.

| Thing                          | Why not                                                                                                                                        |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| Xcode                          | The iOS target is a PWA. No native iOS build exists.                                                                                           |
| Apple Developer Program (paid) | Same reason. Web Push needs no membership.                                                                                                     |
| Supabase CLI                   | Postgres runs in a container. Brief §12.3.                                                                                                     |
| Redis                          | Dropped in planning — 6 users.                                                                                                                 |
| A local Postgres install       | It's a container. A second Postgres on port 5432 will collide with it and produce a confusing "connection refused" against the wrong database. |
| Global `expo-cli`              | Deprecated and replaced by the local `npx expo` command.                                                                                       |

## 11. Troubleshooting

| Symptom                                           | Cause                                                                     |
| ------------------------------------------------- | ------------------------------------------------------------------------- |
| `node: command not found` in a new terminal       | nvm is per-shell. `nvm alias default 22`, then reopen.                    |
| `dotnet` works, `dotnet --list-sdks` missing 10.0 | Two .NET installs (arm64 and x64) on macOS. Check `which -a dotnet`.      |
| Docker command hangs forever                      | Docker Desktop isn't running. It is an app, not a service.                |
| Port 5432 already in use                          | A locally installed Postgres. Stop it — §10.                              |
| Expo build fails on Gradle/JDK                    | Not JDK 17. See §4.                                                       |
| Whole file shows as modified after saving         | EditorConfig extension not installed in VS Code — §3.                     |
| `permission denied` on `docker` (Linux)           | Not in the `docker` group. `sudo usermod -aG docker $USER`, then log out. |

---

When something here is wrong or out of date, fix it in a PR rather than telling one person in
chat. This document is only worth having if it is the thing that is true.
